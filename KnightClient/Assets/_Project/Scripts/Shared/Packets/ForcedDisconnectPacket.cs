namespace KnightOnline.Client.Shared.Packets
{
    public enum ForcedDisconnectReason : byte
    {
        DuplicateAccountSession = 1,
        AccountTemporarilyLocked = 2,
        ServerShutdown = 3,
    }

    public sealed class ForcedDisconnectPacket
    {
        public ForcedDisconnectReason Reason { get; }
        public string Message { get; }

        public ForcedDisconnectPacket(
            ForcedDisconnectReason reason,
            string message)
        {
            Reason = reason;
            Message = message;
        }
    }
}
