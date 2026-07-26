using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Characters;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Persistence;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class GetCharacterCreationCatalogPacketHandler(
    ICharacterCreationCatalog catalog,
    CharacterOptions options) : IPacketHandler
{
    public PacketType PacketType =>
        PacketType.GetCharacterCreationCatalogRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        GetCharacterCreationCatalogRequestPacket? request =
            JsonSerializer.Deserialize<
                GetCharacterCreationCatalogRequestPacket>(payload);
        string serverId = string.IsNullOrWhiteSpace(request?.ServerId)
            ? options.ServerId
            : request.ServerId;

        if (!string.Equals(
                serverId,
                options.ServerId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await connection.SendAsync(
            PacketType.GetCharacterCreationCatalogResponse,
            catalog.GetSnapshot(serverId),
            cancellationToken);
    }
}

public sealed class CheckCharacterNamePacketHandler(
    CharacterRepository characters,
    CharacterNamePolicy namePolicy,
    CharacterOptions options) : IPacketHandler
{
    public PacketType PacketType => PacketType.CheckCharacterNameRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        CheckCharacterNameRequestPacket? request =
            JsonSerializer.Deserialize<CheckCharacterNameRequestPacket>(payload);
        CharacterNameValidationResult validation =
            namePolicy.Validate(request?.CharacterName);
        bool validServer = string.Equals(
            request?.ServerId,
            options.ServerId,
            StringComparison.OrdinalIgnoreCase);
        bool available = validation.IsValid &&
            validServer &&
            await characters.IsNameAvailableAsync(validation.NormalizedName);

        string message = !validServer
            ? "The selected server is not available."
            : !validation.IsValid
                ? validation.Message
                : available
                    ? "Character name is available."
                    : "That character name already exists.";

        await connection.SendAsync(
            PacketType.CheckCharacterNameResponse,
            new CheckCharacterNameResponsePacket(
                available,
                validation.NormalizedName,
                message),
            cancellationToken);
    }
}
