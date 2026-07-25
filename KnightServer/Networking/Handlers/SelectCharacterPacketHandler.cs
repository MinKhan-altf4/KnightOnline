using System.Numerics;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Players;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class SelectCharacterPacketHandler(
    CharacterRepository characters,
    ActivePlayerRegistry activePlayers,
    CharacterOptions characterOptions,
    CombatOptions combatOptions,
    WorldOptions worldOptions) : IPacketHandler
{
    public PacketType PacketType => PacketType.SelectCharacterRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        var request =
            JsonSerializer.Deserialize<SelectCharacterRequestPacket>(payload);

        if (request == null)
            return;

        if (connection.PlayerSession != null)
        {
            await SendFailure(
                connection,
                SelectCharacterResult.AlreadySelected,
                "A character is already selected for this connection.",
                cancellationToken);
            return;
        }

        CharacterSummaryPacket? character =
            await characters.FindOwnedAsync(request.CharacterId);

        if (character == null)
        {
            await SendFailure(
                connection,
                SelectCharacterResult.CharacterNotFound,
                "Character was not found for this account.",
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

        DateTime utcNow = DateTime.UtcNow;
        var spawnPosition = new Vector2(
            characterOptions.SpawnPositionX,
            characterOptions.SpawnPositionY);
        var session = new PlayerSession(
            character.CharacterId,
            character.CharacterName,
            character.Level,
            characterOptions.InitialMaximumHealth,
            characterOptions.InitialMaximumHealth,
            characterOptions.MoveSpeed,
            spawnPosition,
            combatOptions.BaseAttackDamage,
            TimeSpan.FromMilliseconds(
                worldOptions.MaximumMovementDeltaMilliseconds),
            utcNow);

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

        await connection.SendAsync(
            PacketType.SelectCharacterResponse,
            new SelectCharacterResponsePacket(
                SelectCharacterResult.Success,
                "Character selected.",
                new SelectedCharacterPacket(
                    session.CharacterId,
                    session.CharacterName,
                    session.Level,
                    session.CurrentHealth,
                    session.MaximumHealth,
                    session.MoveSpeed,
                    session.Position.X,
                    session.Position.Y)),
            cancellationToken);
    }

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
