namespace KnightOnline.Client.Shared.Packets
{
    /// <summary>
    /// Wire protocol version shared by client, server and bots (B10).
    /// Bump on any breaking packet change and raise the server's
    /// network.minimumProtocolVersion once old clients are no longer supported.
    ///
    /// History:
    ///   1 - implicit (ConnectRequest without ProtocolVersion).
    ///   2 - P1: handshake required, WorldEntitiesSnapshot, tick snapshots.
    ///   3 - Dead reckoning: remote player Speed; Moved sent only on change.
    /// </summary>
    public static class PacketProtocol
    {
        public const int Version = 3;
    }
}
