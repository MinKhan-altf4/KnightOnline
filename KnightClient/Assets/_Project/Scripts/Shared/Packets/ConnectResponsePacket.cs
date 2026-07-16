namespace KnightOnline.Client.Shared.Packets
{
    public enum ConnectResult : byte
    {
        Success = 0,
        VersionMismatch = 1,
        ServerFull = 2,
    }

    public sealed class ConnectResponsePacket
    {
        public required ConnectResult Result { get; set; }
        public required string Message { get; set; }
    }
}