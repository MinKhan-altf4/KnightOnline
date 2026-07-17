namespace KnightOnline.Client.Shared.Packets
{
    public enum ConnectResult : byte
    {
        Success = 0,
        VersionMismatch = 1,
        ServerFull = 2,
        NetworkError = 3,
    }

    public sealed class ConnectResponsePacket
    {
        public ConnectResult Result { get; }
        public string Message { get; }

        public ConnectResponsePacket(ConnectResult result, string message)
        {
            Result = result;
            Message = message;
        }
    }
}