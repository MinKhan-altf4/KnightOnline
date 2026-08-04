using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Time;
using KnightOnline.Server.World;
using KnightOnline.Server.Tutorials;
using KnightOnline.Server.Configuration;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class EnterWorldPacketHandler(
    IServerClock clock,
    IWorldMovementResolver movementResolver,
    StarterTutorialService tutorialService,
    TutorialDefinitionOptions tutorialDefinition) : IPacketHandler
{
    public PacketType PacketType => PacketType.EnterWorldRequest;
    public PacketAccessLevel RequiredAccess =>
        PacketAccessLevel.CharacterSelected;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        EnterWorldRequestPacket? request;
        try
        {
            request = JsonSerializer.Deserialize<EnterWorldRequestPacket>(
                payload);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request == null || request.GameplaySessionId == Guid.Empty)
        {
            await SendFailure(
                connection,
                EnterWorldResult.MalformedRequest,
                "The enter-world request is malformed.",
                cancellationToken);
            return;
        }

        var session = connection.PlayerSession;
        if (session == null)
        {
            await SendFailure(
                connection,
                EnterWorldResult.NoGameplaySession,
                "A gameplay session must be selected first.",
                cancellationToken);
            return;
        }

        if (session.SessionId != request.GameplaySessionId)
        {
            await SendFailure(
                connection,
                EnterWorldResult.SessionMismatch,
                "The gameplay session is stale or belongs to another request.",
                cancellationToken);
            return;
        }

        DateTime utcNow = clock.UtcNow;
        session.AdvancePosition(utcNow, movementResolver);
        var snapshot = new EnterWorldSnapshotPacket(
            session.SessionId,
            snapshotVersion: 1,
            utcNow,
            GameplaySessionPacketMapper.ToCharacterPacket(session));
        await connection.SendAsync(
            PacketType.EnterWorldResponse,
            new EnterWorldResponsePacket(
                EnterWorldResult.Success,
                "World snapshot created.",
                snapshot),
            cancellationToken);
        TutorialCommandResult tutorial = await tutorialService.GetCurrentAsync(
            session.CharacterId, cancellationToken);
        await InteractNpcPacketHandler.SendProgressAsync(connection,
            tutorialDefinition, tutorial.Progress, cancellationToken);
        await connection.SendAsync(PacketType.InventorySnapshot,
            new InventorySnapshotPacket(tutorial.Inventory.Select(value =>
                new InventoryItemPacket(value.Id, value.ItemDefinitionId,
                    value.Quantity)).ToArray()), cancellationToken);
    }

    private static Task SendFailure(
        ClientConnection connection,
        EnterWorldResult result,
        string message,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            PacketType.EnterWorldResponse,
            new EnterWorldResponsePacket(result, message),
            cancellationToken);
}
