using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Client.Network.Handlers
{
    public interface IClientPacketHandler
    {
        PacketType PacketType { get; }
        void Handle(string payload);
    }
}
