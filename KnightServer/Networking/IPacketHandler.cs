using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server.Networking;

public interface IPacketHandler
{
    PacketType PacketType { get; }

    Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken);
}
