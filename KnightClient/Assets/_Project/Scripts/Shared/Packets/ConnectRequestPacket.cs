namespace KnightOnline.Client.Shared.Packets
{
    public sealed class ConnectRequestPacket
    {
        /// <summary>Build/application version, informational only.</summary>
        public string ClientVersion { get; }

        /// <summary>
        /// <see cref="PacketProtocol.Version"/> of the sender. Clients from
        /// before protocol 2 omit it and deserialize as 0.
        /// </summary>
        public int ProtocolVersion { get; }

        public ConnectRequestPacket(string clientVersion, int protocolVersion)
        {
            ClientVersion = clientVersion;
            ProtocolVersion = protocolVersion;
        }
    }
}
