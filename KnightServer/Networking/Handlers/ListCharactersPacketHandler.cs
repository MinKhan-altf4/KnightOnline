using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Persistence;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class ListCharactersPacketHandler(
    CharacterRepository characterRepository) : IPacketHandler
{
    public PacketType PacketType => PacketType.ListCharactersRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CharacterSummaryPacket> characters =
            await characterRepository.ListAsync();

        await connection.SendAsync(
            PacketType.ListCharactersResponse,
            new ListCharactersResponsePacket(characters),
            cancellationToken);
    }
}
