namespace KnightOnline.Client.Shared.Packets
{
    public sealed class PacketEnvelope
    {
        public PacketType Type { get; }
        public string Payload { get; }

        public PacketEnvelope(PacketType type, string payload)
        {
            Type = type;
            Payload = payload;
        }
    }
}