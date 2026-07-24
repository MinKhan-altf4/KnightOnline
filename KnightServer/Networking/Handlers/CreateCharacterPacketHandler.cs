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
        CreateCharacterRequestPacket? request =
            JsonSerializer.Deserialize<CreateCharacterRequestPacket>(payload);

        if (request == null)
            return;

        string name = request.CharacterName?.Trim() ?? string.Empty;
        CreateCharacterResponsePacket response;

        if (name.Length == 0)
        {
            response = new CreateCharacterResponsePacket(
                CreateCharacterResult.NameEmpty,
                "Character name cannot be empty.");
        }
        else if (name.Length > 20)
        {
            response = new CreateCharacterResponsePacket(
                CreateCharacterResult.NameTooLong,
                "Character name is limited to 20 characters.");
        }
        else
        {
            response = await characterRepository.CreateAsync(name);
        }

        await connection.SendAsync(
            PacketType.CreateCharacterResponse,
            response,
            cancellationToken);
    }
}
