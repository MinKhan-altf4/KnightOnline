using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server.Networking;

public enum PacketAccessLevel : byte
{
    Anonymous = 0,
    Authenticated = 1,
    CharacterSelected = 2,
}

public interface IPacketHandler
{
    PacketType PacketType { get; }
    PacketAccessLevel RequiredAccess =>
        PacketAccessLevel.Authenticated;

    Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken);
}
