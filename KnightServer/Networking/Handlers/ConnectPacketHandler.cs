using System.Text.Json;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class ConnectPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.ConnectRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        ConnectRequestPacket? request =
            JsonSerializer.Deserialize<ConnectRequestPacket>(payload);

        if (request == null)
            return;

        await connection.SendAsync(
            PacketType.ConnectResponse,
            new ConnectResponsePacket(
                ConnectResult.Success,
                "Welcome to KnightOnline!"),
            cancellationToken);
    }
}
