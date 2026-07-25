using System;

namespace KnightOnline.Client.Network
{
    public sealed class NetworkSettings
    {
        public NetworkSettings(string host, int port, int maximumPacketBytes)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Server host is required.", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (maximumPacketBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumPacketBytes));

            Host = host.Trim();
            Port = port;
            MaximumPacketBytes = maximumPacketBytes;
        }

        public string Host { get; }
        public int Port { get; }
        public int MaximumPacketBytes { get; }
    }
}
