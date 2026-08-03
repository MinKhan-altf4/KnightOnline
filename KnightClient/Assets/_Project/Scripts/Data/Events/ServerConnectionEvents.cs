using System;
using KnightOnline.Client.Core.Events;

namespace KnightOnline.Client.Data.Events
{
    public enum ConnectionOutcome : byte
    {
        Success = 0,
        VersionMismatch = 1,
        ServerFull = 2,
        NetworkError = 3,
    }

    public enum AuthenticationOutcome : byte
    {
        Success = 0,
        InvalidCredentials = 1,
        InvalidOrExpiredToken = 2,
        UsernameUnavailable = 3,
        GuestNotFound = 4,
        AlreadyAuthenticated = 5,
        InvalidRequest = 6,
        SessionConflict = 7,
        RateLimited = 8,
        AccountActive = 9,
        ServerFull = 10,
    }

    // Đổi từ IGameEvent thành IStickyGameEvent - đây là event đại diện
    // TRẠNG THÁI kết nối hiện tại, cần được EventBus lưu lại và phát ngay
    // cho subscriber đăng ký muộn (giải quyết race condition Network vs UI).
    public readonly struct ServerConnectionResultEvent : IStickyGameEvent
    {
        public readonly ConnectionOutcome Result;
        public readonly string Message;

        public ServerConnectionResultEvent(
            ConnectionOutcome result,
            string message)
        {
            Result = result;
            Message = message;
        }
    }

    // Giữ nguyên IGameEvent thường - đây là 1 sự kiện tức thời
    // ("vừa mất kết nối"), không nên phát lại mãi cho subscriber tới sau.
    public readonly struct ServerDisconnectedEvent : IGameEvent
    {
        public readonly string Message;
        public readonly bool IsForced;

        public ServerDisconnectedEvent(string message = null, bool isForced = false)
        {
            Message = message;
            IsForced = isForced;
        }
    }

    public readonly struct AuthenticationResultEvent : IGameEvent
    {
        public readonly AuthenticationOutcome Result;
        public readonly string Message;
        public readonly string AccountKey;
        public readonly string DisplayName;
        public readonly bool IsGuest;
        public readonly string RefreshToken;
        public readonly DateTime RefreshTokenExpiresAtUtc;
        public readonly Guid SessionGeneration;
        public readonly DateTime SessionLeaseExpiresAtUtc;
        public readonly int HeartbeatIntervalSeconds;

        public AuthenticationResultEvent(
            AuthenticationOutcome result,
            string message,
            string accountKey,
            bool isGuest,
            string refreshToken,
            DateTime refreshTokenExpiresAtUtc,
            string displayName,
            Guid sessionGeneration,
            DateTime sessionLeaseExpiresAtUtc,
            int heartbeatIntervalSeconds)
        {
            Result = result;
            Message = message;
            AccountKey = accountKey;
            DisplayName = displayName;
            IsGuest = isGuest;
            RefreshToken = refreshToken;
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
            SessionGeneration = sessionGeneration;
            SessionLeaseExpiresAtUtc = sessionLeaseExpiresAtUtc;
            HeartbeatIntervalSeconds = heartbeatIntervalSeconds;
        }
    }

    public readonly struct AccountSessionHeartbeatEvent : IGameEvent
    {
        public readonly bool Renewed;
        public readonly DateTime LeaseExpiresAtUtc;

        public AccountSessionHeartbeatEvent(
            bool renewed,
            DateTime leaseExpiresAtUtc)
        {
            Renewed = renewed;
            LeaseExpiresAtUtc = leaseExpiresAtUtc;
        }
    }

    public readonly struct PlayerPositionSnapshotEvent : IGameEvent
    {
        public readonly long ServerSequence;
        public readonly long AcknowledgedSequence;
        public readonly bool InputAccepted;
        public readonly float PositionX;
        public readonly float PositionY;

        public PlayerPositionSnapshotEvent(
            long serverSequence,
            long acknowledgedSequence,
            bool inputAccepted,
            float positionX,
            float positionY)
        {
            ServerSequence = serverSequence;
            AcknowledgedSequence = acknowledgedSequence;
            InputAccepted = inputAccepted;
            PositionX = positionX;
            PositionY = positionY;
        }
    }
}
