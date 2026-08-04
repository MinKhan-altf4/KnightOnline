using System.Numerics;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Players;
using KnightOnline.Server.Progression;
using KnightOnline.Server.Time;
using KnightOnline.Server.Tutorials;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class ListNpcsPacketHandler(IReadOnlyList<NpcDefinitionOptions> npcs)
    : IPacketHandler
{
    public PacketType PacketType => PacketType.ListNpcsRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.CharacterSelected;
    public Task HandleAsync(ClientConnection connection, string payload,
        CancellationToken cancellationToken)
    {
        PlayerSession session = connection.PlayerSession!;
        return connection.SendAsync(PacketType.ListNpcsResponse,
            new ListNpcsResponsePacket(npcs.Where(value => string.Equals(
                value.MapDefinitionId, session.MapDefinitionId,
                StringComparison.OrdinalIgnoreCase)).Select(value =>
                    new NpcSnapshotPacket(value.DefinitionId, value.DisplayName,
                        value.PositionX, value.PositionY)).ToArray()),
            cancellationToken);
    }
}

public sealed class InteractNpcPacketHandler(
    IReadOnlyList<NpcDefinitionOptions> npcs,
    TutorialDefinitionOptions tutorial,
    StarterTutorialService service,
    IExperienceCurve experienceCurve,
    CharacterStatsPipeline statsPipeline,
    IServerClock clock) : IPacketHandler
{
    public PacketType PacketType => PacketType.InteractNpcRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.CharacterSelected;
    public async Task HandleAsync(ClientConnection connection, string payload,
        CancellationToken cancellationToken)
    {
        InteractNpcRequestPacket? request;
        try { request = JsonSerializer.Deserialize<InteractNpcRequestPacket>(payload); }
        catch (JsonException) { request = null; }
        PlayerSession session = connection.PlayerSession!;
        if (request == null || request.RequestId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.NpcDefinitionId))
        {
            await Respond(NpcInteractionResult.InvalidRequest, Guid.Empty, "");
            return;
        }
        NpcDefinitionOptions? npc = npcs.SingleOrDefault(value => string.Equals(
            value.DefinitionId, request.NpcDefinitionId,
            StringComparison.OrdinalIgnoreCase));
        if (npc == null) { await Respond(NpcInteractionResult.NpcNotFound, request.RequestId, ""); return; }
        if (!string.Equals(npc.MapDefinitionId, session.MapDefinitionId,
                StringComparison.OrdinalIgnoreCase))
        { await Respond(NpcInteractionResult.WrongMap, request.RequestId, ""); return; }
        if (Vector2.Distance(session.Position,
                new Vector2(npc.PositionX, npc.PositionY)) > npc.InteractionRange)
        { await Respond(NpcInteractionResult.OutOfRange, request.RequestId, ""); return; }

        TutorialCommandResult result = await service.InteractWithQuestNpcAsync(
            request.RequestId, session.CharacterId, cancellationToken);
        string dialogue = result.Outcome switch
        {
            StarterTutorialOutcome.QuestAccepted => npc.InitialDialogue,
            StarterTutorialOutcome.QuestCompleted =>
                $"{npc.CompletionDialogue}\n{npc.FarewellDialogue}",
            _ => npc.ProgressDialogue,
        };
        NpcInteractionResult response = result.Status switch
        {
            TutorialCommandStatus.Applied => NpcInteractionResult.Success,
            TutorialCommandStatus.AlreadyApplied => NpcInteractionResult.AlreadyProcessed,
            TutorialCommandStatus.NoChange => NpcInteractionResult.InvalidQuestState,
            TutorialCommandStatus.InvalidRequest => NpcInteractionResult.InvalidRequest,
            _ => NpcInteractionResult.InternalError,
        };
        await Respond(response, request.RequestId, dialogue);
        await SendProgressAsync(connection, tutorial, result.Progress,
            cancellationToken);
        if (result.Outcome == StarterTutorialOutcome.QuestCompleted)
        {
            long levelFloor = experienceCurve.GetTotalExperienceRequiredForLevel(
                result.Level);
            CharacterStats stats = statsPipeline.Calculate(
                session.Profile.ClassDefinitionId, result.Level);
            PlayerVitalsState vitals = session.ApplyProgression(result.Level,
                result.TotalExperience,
                result.TotalExperience - levelFloor,
                experienceCurve.GetExperienceRequiredToAdvance(result.Level),
                stats);
            await connection.SendAsync(PacketType.CharacterProgressionChanged,
                new CharacterProgressionChangedPacket(request.RequestId,
                    tutorial.ExperienceReward, result.TotalExperience,
                    result.Level, result.TotalExperience - levelFloor,
                    experienceCurve.GetExperienceRequiredToAdvance(result.Level),
                    session.CurrentHealth, session.MaximumHealth,
                    session.CurrentMana, session.MaximumMana,
                    session.BaseAttack, session.Defense), cancellationToken);
            await connection.SendAsync(PacketType.CharacterVitalsSnapshot,
                GameplaySessionPacketMapper.ToVitalsPacket(vitals, clock.UtcNow),
                cancellationToken);
            await connection.SendAsync(PacketType.InventorySnapshot,
                new InventorySnapshotPacket(result.Inventory.Select(value =>
                    new InventoryItemPacket(value.Id, value.ItemDefinitionId,
                        value.Quantity)).ToArray()), cancellationToken);
        }

        Task Respond(NpcInteractionResult code, Guid id, string text) =>
            connection.SendAsync(PacketType.InteractNpcResponse,
                new InteractNpcResponsePacket(id, code, text), cancellationToken);
    }

    internal static Task SendProgressAsync(ClientConnection connection,
        TutorialDefinitionOptions tutorial, StarterTutorialSnapshot progress,
        CancellationToken token) => connection.SendAsync(
            PacketType.TutorialProgressSnapshot,
            new TutorialProgressSnapshotPacket(tutorial.DefinitionId,
                progress.CurrentStepDefinitionId, (byte)progress.State,
                progress.ObjectiveProgress, tutorial.RequiredKillCount), token);

    internal static async Task SendTransitionAsync(ClientConnection connection,
        PlayerSession session, IMapCatalog maps, string mapId, string spawnId,
        DateTime now, CancellationToken token)
    {
        if (!maps.TryResolveSpawn(mapId, spawnId, out MapSpawnPoint spawn))
            throw new InvalidOperationException($"Missing spawn {mapId}/{spawnId}.");
        PlayerPositionState state = session.Teleport(mapId, spawnId,
            new Vector2(spawn.Position.X, spawn.Position.Y), now);
        await connection.SendAsync(PacketType.MapTransitionSnapshot,
            new MapTransitionSnapshotPacket(mapId, spawnId, state.Position.X,
                state.Position.Y, now), token);
    }
}

public sealed class ListPortalsPacketHandler(
    IReadOnlyList<PortalDefinitionOptions> portals,
    IReadOnlyList<MapDefinitionOptions> maps) : IPacketHandler
{
    public PacketType PacketType => PacketType.ListPortalsRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.CharacterSelected;
    public Task HandleAsync(ClientConnection connection, string payload,
        CancellationToken cancellationToken)
    {
        PlayerSession session = connection.PlayerSession!;
        MapDefinitionOptions map = maps.Single(value => string.Equals(
            value.DefinitionId, session.MapDefinitionId,
            StringComparison.OrdinalIgnoreCase));
        return connection.SendAsync(PacketType.ListPortalsResponse,
            new ListPortalsResponsePacket(portals.Where(value => string.Equals(
                value.SourceMapDefinitionId, session.MapDefinitionId,
                StringComparison.OrdinalIgnoreCase)).Select(value =>
                    new PortalSnapshotPacket(value.DefinitionId,
                        value.DisplayName, value.PositionX, value.PositionY))
                .ToArray(), map.MinimumX, map.MaximumX,
                map.MinimumY, map.MaximumY), cancellationToken);
    }
}

public sealed class UsePortalPacketHandler(
    IReadOnlyList<PortalDefinitionOptions> portals,
    StarterTutorialService tutorialService,
    IMapCatalog maps,
    IServerClock clock) : IPacketHandler
{
    public PacketType PacketType => PacketType.UsePortalRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.CharacterSelected;
    public async Task HandleAsync(ClientConnection connection, string payload,
        CancellationToken cancellationToken)
    {
        UsePortalRequestPacket? request;
        try { request = JsonSerializer.Deserialize<UsePortalRequestPacket>(payload); }
        catch (JsonException) { request = null; }
        PlayerSession session = connection.PlayerSession!;
        PortalUseResult status = PortalUseResult.InvalidRequest;
        PortalDefinitionOptions? portal = request == null ? null :
            portals.SingleOrDefault(value => string.Equals(value.DefinitionId,
                request.PortalDefinitionId, StringComparison.OrdinalIgnoreCase));
        if (request != null && request.RequestId != Guid.Empty)
        {
            if (portal == null) status = PortalUseResult.PortalNotFound;
            else if (!string.Equals(portal.SourceMapDefinitionId,
                         session.MapDefinitionId,
                         StringComparison.OrdinalIgnoreCase))
                status = PortalUseResult.WrongMap;
            else if (Vector2.Distance(session.Position,
                         new Vector2(portal.PositionX, portal.PositionY)) >
                     portal.InteractionRange)
                status = PortalUseResult.OutOfRange;
            else if (session.Level < portal.MinimumLevel)
                status = PortalUseResult.LevelRequired;
            else if (!string.IsNullOrWhiteSpace(
                         portal.RequiredTutorialStepDefinitionId) &&
                     !await tutorialService.IsAtStepAsync(session.CharacterId,
                         portal.RequiredTutorialStepDefinitionId,
                         cancellationToken))
                status = PortalUseResult.QuestStateDenied;
            else status = PortalUseResult.Success;
        }
        await connection.SendAsync(PacketType.UsePortalResponse,
            new UsePortalResponsePacket(request?.RequestId ?? Guid.Empty, status),
            cancellationToken);
        if (status != PortalUseResult.Success || portal == null) return;
        await InteractNpcPacketHandler.SendTransitionAsync(connection, session,
            maps, portal.DestinationMapDefinitionId,
            portal.DestinationSpawnPointId, clock.UtcNow, cancellationToken);
        await tutorialService.PersistPortalTransitionAsync(session.CharacterId,
            portal.DestinationMapDefinitionId, portal.DestinationSpawnPointId,
            session.Position.X, session.Position.Y, cancellationToken);
    }
}
