using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Persistence;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class CreateCharacterPacketHandler(
    CharacterRepository characterRepository) : IPacketHandler
{
    public PacketType PacketType => PacketType.CreateCharacterRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        CreateCharacterRequestPacket? request;
        try
        {
            request = JsonSerializer.Deserialize<CreateCharacterRequestPacket>(
                payload);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request == null)
        {
            await connection.SendAsync(
                PacketType.CreateCharacterResponse,
                new CreateCharacterResponsePacket(
                    CreateCharacterResult.MalformedRequest,
                    "The character creation request is malformed."),
                cancellationToken);
            return;
        }

        if (connection.AccountKey == null)
        {
            await connection.SendAsync(
                PacketType.CreateCharacterResponse,
                new CreateCharacterResponsePacket(
                    CreateCharacterResult.Unauthorized,
                    "Authentication is required."),
                cancellationToken);
            return;
        }

        CreateCharacterResponsePacket response =
            await characterRepository.CreateAsync(
                connection.AccountKey,
                request,
                cancellationToken);

        await connection.SendAsync(
            PacketType.CreateCharacterResponse,
            response,
            cancellationToken);
    }
}
