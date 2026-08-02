#nullable enable

using System;

namespace KnightOnline.Client.Shared.Packets
{
    public enum AuthenticationResultCode : byte
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

    public sealed class CreateGuestRequestPacket
    {
        public string DeviceId { get; }
        public CreateGuestRequestPacket(string deviceId) => DeviceId = deviceId;
    }

    public sealed class ResumeAccountRequestPacket
    {
        public string RefreshToken { get; }
        public string DeviceId { get; }

        public ResumeAccountRequestPacket(
            string refreshToken,
            string deviceId)
        {
            RefreshToken = refreshToken;
            DeviceId = deviceId;
        }
    }

    public sealed class LoginRequestPacket
    {
        public string Username { get; }
        public string Password { get; }
        public string DeviceId { get; }
        public string? GuestRefreshToken { get; }

        public LoginRequestPacket(
            string username,
            string password,
            string deviceId,
            string? guestRefreshToken)
        {
            Username = username;
            Password = password;
            DeviceId = deviceId;
            GuestRefreshToken = guestRefreshToken;
        }
    }

    public sealed class AuthenticationResponsePacket
    {
        public AuthenticationResultCode Result { get; }
        public string Message { get; }
        public string? AccountKey { get; }
        public string? DisplayName { get; }
        public bool IsGuest { get; }
        public string? RefreshToken { get; }
        public DateTime RefreshTokenExpiresAtUtc { get; }
        public Guid SessionGeneration { get; }
        public DateTime SessionLeaseExpiresAtUtc { get; }
        public int HeartbeatIntervalSeconds { get; }

        public AuthenticationResponsePacket(
            AuthenticationResultCode result,
            string message,
            string? accountKey = null,
            bool isGuest = false,
            string? refreshToken = null,
            DateTime refreshTokenExpiresAtUtc = default,
            string? displayName = null,
            Guid sessionGeneration = default,
            DateTime sessionLeaseExpiresAtUtc = default,
            int heartbeatIntervalSeconds = 0)
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

    public sealed class AccountSessionHeartbeatRequestPacket
    {
        public Guid SessionGeneration { get; }

        public AccountSessionHeartbeatRequestPacket(Guid sessionGeneration) =>
            SessionGeneration = sessionGeneration;
    }

    public sealed class AccountSessionHeartbeatResponsePacket
    {
        public bool Renewed { get; }
        public DateTime LeaseExpiresAtUtc { get; }

        public AccountSessionHeartbeatResponsePacket(
            bool renewed,
            DateTime leaseExpiresAtUtc)
        {
            Renewed = renewed;
            LeaseExpiresAtUtc = leaseExpiresAtUtc;
        }
    }

    public sealed class LeaveAccountSessionRequestPacket
    {
    }

    public sealed class LeaveAccountSessionResponsePacket
    {
        public bool Success { get; }
        public string Message { get; }

        public LeaveAccountSessionResponsePacket(
            bool success,
            string message)
        {
            Success = success;
            Message = message;
        }
    }

    public sealed class BeginRegistrationRequestPacket
    {
        public Guid RequestId { get; }
        public string GuestRefreshToken { get; }
        public string DeviceId { get; }
        public string PkceChallenge { get; }

        public BeginRegistrationRequestPacket(
            Guid requestId,
            string guestRefreshToken,
            string deviceId,
            string pkceChallenge)
        {
            RequestId = requestId;
            GuestRefreshToken = guestRefreshToken;
            DeviceId = deviceId;
            PkceChallenge = pkceChallenge;
        }
    }

    public sealed class BeginRegistrationResponsePacket
    {
        public bool Success { get; }
        public string Message { get; }
        public Guid TransactionId { get; }
        public string? RegistrationUrl { get; }
        public string? DevelopmentAuthorizationCode { get; }
        public DateTime ExpiresAtUtc { get; }

        public BeginRegistrationResponsePacket(
            bool success,
            string message,
            Guid transactionId = default,
            string? registrationUrl = null,
            string? developmentAuthorizationCode = null,
            DateTime expiresAtUtc = default)
        {
            Success = success;
            Message = message;
            TransactionId = transactionId;
            RegistrationUrl = registrationUrl;
            DevelopmentAuthorizationCode =
                developmentAuthorizationCode;
            ExpiresAtUtc = expiresAtUtc;
        }
    }

    public sealed class CompleteDevelopmentRegistrationRequestPacket
    {
        public Guid TransactionId { get; }
        public string AuthorizationCode { get; }
        public string PkceVerifier { get; }
        public string Username { get; }
        public string Password { get; }

        public CompleteDevelopmentRegistrationRequestPacket(
            Guid transactionId,
            string authorizationCode,
            string pkceVerifier,
            string username,
            string password)
        {
            TransactionId = transactionId;
            AuthorizationCode = authorizationCode;
            PkceVerifier = pkceVerifier;
            Username = username;
            Password = password;
        }
    }
}
