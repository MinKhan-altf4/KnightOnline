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
        public ConnectResult Result { get; set; }
        public string Message { get; set; }
    }
}