using System;

namespace KnightOnline.Client.Data.Models
{
    [Serializable]
    public sealed class StoredAccountSession
    {
        public string AccountKey;
        public string DisplayName;
        public bool IsGuest;
        public string RefreshToken;
        public string DeviceId;
        public long ExpiresAtUtcTicks;

        public DateTime ExpiresAtUtc =>
            new DateTime(ExpiresAtUtcTicks, DateTimeKind.Utc);
    }
}
