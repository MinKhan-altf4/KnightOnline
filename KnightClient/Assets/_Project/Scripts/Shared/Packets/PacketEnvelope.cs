namespace KnightOnline.Client.Shared.Packets
{
    public sealed class PacketEnvelope
    {
        public PacketType Type { get; set; }
        public string Payload { get; set; }
    }
}