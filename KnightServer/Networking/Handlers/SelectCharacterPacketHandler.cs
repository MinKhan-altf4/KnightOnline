using System.Numerics;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Players;
using KnightOnline.Server.Progression;
using KnightOnline.Server.Time;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class SelectCharacterPacketHandler(
    CharacterRepository characters,
    ActivePlayerRegistry activePlayers,
    IActiveAccountLeaseStore accountSessions,
    CharacterOptions characterOptions,
    WorldOptions worldOptions,
    CharacterStatsPipeline statsPipeline,
    IExperienceCurve experienceCurve,
    IServerClock clock) : IPacketHandler
{
    public PacketType PacketType => PacketType.SelectCharacterRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        SelectCharacterRequestPacket? request;
        try
        {
            request = JsonSerializer.Deserialize<SelectCharacterRequestPacket>(
                payload);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request == null)
        {
            await SendFailure(
                connection,
                SelectCharacterResult.MalformedRequest,
                "The character selection request is malformed.",
                cancellationToken);
            return;
        }

        if (connection.AccountKey == null)
        {
            await SendFailure(
                connection,
                SelectCharacterResult.Unauthorized,
                "Authentication is required.",
                cancellationToken);
            return;
        }

        if (connection.PlayerSession != null)
        {
            if (connection.PlayerSession.CharacterId == request.CharacterId)
            {
                await SendSuccess(
                    connection,
                    connection.PlayerSession,
                    cancellationToken);
                return;
            }

            await SendFailure(
                connection,
                SelectCharacterResult.AlreadySelected,
                "A character is already selected for this connection.",
                cancellationToken);
            return;
        }

        CharacterSummaryPacket? character =
            await characters.FindOwnedAsync(
                connection.AccountKey,
                request.CharacterId);

        if (character == null)
        {
            await SendFailure(
                connection,
                SelectCharacterResult.CharacterNotFound,
                "Character was not found for this account.",
                cancellationToken);
            return;
        }

        if (!await accountSessions.IsOwnerAsync(
                connection.AccountKey,
                connection.ConnectionId,
                connection.AccountSessionGeneration,
                clock.UtcNow,
                cancellationToken))
        {
            await SendFailure(
                connection,
                SelectCharacterResult.Unauthorized,
                "The active account lease is not owned by this connection.",
                cancellationToken);
            return;
        }

        if (!activePlayers.TryClaim(character.CharacterId, connection))
        {
            await SendFailure(
                connection,
                SelectCharacterResult.CharacterAlreadyOnline,
                "This character is already online.",
                cancellationToken);
            return;
        }

        DateTime utcNow = clock.UtcNow;
        var spawnPosition = new Vector2(
            character.PositionX,
            character.PositionY);
        var profile = new PlayerSessionProfile(
            character.CharacterId,
            character.CharacterName,
            character.Level,
            character.SlotIndex,
            character.ClassDefinitionId,
            character.BodyTypeDefinitionId,
            character.CurrentMapDefinitionId,
            character.CurrentSpawnPointId,
            character.AppearanceSelections.Select(value =>
                new PlayerAppearanceSelection(
                    value.SlotDefinitionId,
                    value.OptionDefinitionId)).ToArray());
        CharacterStats stats = statsPipeline.Calculate(
            character.ClassDefinitionId,
            character.Level);
        long levelFloor = experienceCurve
            .GetTotalExperienceRequiredForLevel(character.Level);
        long normalizedTotalExperience = Math.Max(
            character.TotalExperience,
            levelFloor);
        var session = new PlayerSession(
            profile,
            stats.MaximumHealth,
            stats.MaximumHealth,
            characterOptions.MoveSpeed,
            spawnPosition,
            stats.Attack,
            TimeSpan.FromMilliseconds(
                worldOptions.MaximumMovementDeltaMilliseconds),
            utcNow,
            normalizedTotalExperience,
            normalizedTotalExperience - levelFloor,
            experienceCurve.GetExperienceRequiredToAdvance(character.Level),
            stats.MaximumMana,
            stats.Defense);

        if (!connection.TryAttachPlayerSession(session))
        {
            activePlayers.Release(character.CharacterId, connection);
            await SendFailure(
                connection,
                SelectCharacterResult.AlreadySelected,
                "A character is already selected for this connection.",
                cancellationToken);
            return;
        }

        await SendSuccess(connection, session, cancellationToken);
    }

    private static Task SendSuccess(
        ClientConnection connection,
        PlayerSession session,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            PacketType.SelectCharacterResponse,
            new SelectCharacterResponsePacket(
                SelectCharacterResult.Success,
                "Gameplay session created.",
                GameplaySessionPacketMapper.ToCharacterPacket(session),
                session.SessionId),
            cancellationToken);

    private static Task SendFailure(
        ClientConnection connection,
        SelectCharacterResult result,
        string message,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            PacketType.SelectCharacterResponse,
            new SelectCharacterResponsePacket(result, message),
            cancellationToken);
}
