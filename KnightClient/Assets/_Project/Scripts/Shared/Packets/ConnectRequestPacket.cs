namespace KnightOnline.Client.Shared.Packets
{
    public sealed class ConnectRequestPacket
    {
        public string ClientVersion { get; }

        public ConnectRequestPacket(string clientVersion)
        {
            ClientVersion = clientVersion;
        }
    }
}