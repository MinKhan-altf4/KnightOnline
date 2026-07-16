namespace KnightOnline.Client.Shared.Packets
{
    public sealed class PacketEnvelope
    {
        public required PacketType Type { get; set; }
        public required string Payload { get; set; }
    }
}