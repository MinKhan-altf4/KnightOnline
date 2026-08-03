using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Players;
using KnightOnline.Server.Time;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class CreateGuestPacketHandler(
    AccountAuthenticationService authentication,
    IActiveAccountLeaseStore accountSessions,
    AuthenticationRateLimiter rateLimiter,
    IServerClock clock,
    AuthenticationOptions options) : IPacketHandler
{
    public PacketType PacketType => PacketType.CreateGuestRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.Anonymous;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        if (!rateLimiter.TryAcquire(
                $"{connection.RemoteAddress}|create-guest",
                clock.UtcNow))
        {
            await AuthenticationPacketHandlerSupport.SendRateLimitedAsync(
                connection,
                PacketType.CreateGuestResponse,
                cancellationToken);
            return;
        }

        CreateGuestRequestPacket? request =
            JsonSerializer.Deserialize<CreateGuestRequestPacket>(payload);
        if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            await AuthenticationPacketHandlerSupport.SendInvalidRequestAsync(
                connection,
                PacketType.CreateGuestResponse,
                cancellationToken);
            return;
        }

        AuthenticationResult result = await authentication.CreateGuestAsync(
            request.DeviceId,
            cancellationToken);
        await AuthenticationPacketHandlerSupport.CompleteAsync(
            connection,
            result,
            PacketType.CreateGuestResponse,
            accountSessions,
            clock.UtcNow,
            options.HeartbeatIntervalSeconds,
            cancellationToken);
    }
}

public sealed class ResumeAccountPacketHandler(
    AccountAuthenticationService authentication,
    IActiveAccountLeaseStore accountSessions,
    AuthenticationRateLimiter rateLimiter,
    IServerClock clock,
    AuthenticationOptions options) : IPacketHandler
{
    public PacketType PacketType => PacketType.ResumeAccountRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.Anonymous;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        if (!rateLimiter.TryAcquire(
                $"{connection.RemoteAddress}|resume",
                clock.UtcNow))
        {
            await AuthenticationPacketHandlerSupport.SendRateLimitedAsync(
                connection,
                PacketType.ResumeAccountResponse,
                cancellationToken);
            return;
        }

        ResumeAccountRequestPacket? request =
            JsonSerializer.Deserialize<ResumeAccountRequestPacket>(payload);
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RefreshToken) ||
            string.IsNullOrWhiteSpace(request.DeviceId))
        {
            await AuthenticationPacketHandlerSupport.SendInvalidRequestAsync(
                connection,
                PacketType.ResumeAccountResponse,
                cancellationToken);
            return;
        }

        AuthenticationResult result = await authentication.ResumeAsync(
            request.RefreshToken,
            request.DeviceId,
            cancellationToken);
        await AuthenticationPacketHandlerSupport.CompleteAsync(
            connection,
            result,
            PacketType.ResumeAccountResponse,
            accountSessions,
            clock.UtcNow,
            options.HeartbeatIntervalSeconds,
            cancellationToken);
    }
}

public sealed class LoginPacketHandler(
    AccountAuthenticationService authentication,
    IActiveAccountLeaseStore accountSessions,
    AuthenticationRateLimiter rateLimiter,
    IServerClock clock,
    AuthenticationOptions options) : IPacketHandler
{
    public PacketType PacketType => PacketType.LoginRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.Anonymous;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        if (!rateLimiter.TryAcquire(
                $"{connection.RemoteAddress}|login",
                clock.UtcNow))
        {
            await AuthenticationPacketHandlerSupport.SendRateLimitedAsync(
                connection,
                PacketType.LoginResponse,
                cancellationToken);
            return;
        }

        LoginRequestPacket? request =
            JsonSerializer.Deserialize<LoginRequestPacket>(payload);
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.DeviceId))
        {
            await AuthenticationPacketHandlerSupport.SendInvalidRequestAsync(
                connection,
                PacketType.LoginResponse,
                cancellationToken);
            return;
        }

        AuthenticationResult result = await authentication.LoginAsync(
            request.Username,
            request.Password,
            request.DeviceId,
            request.GuestRefreshToken,
            cancellationToken);
        await AuthenticationPacketHandlerSupport.CompleteAsync(
            connection,
            result,
            PacketType.LoginResponse,
            accountSessions,
            clock.UtcNow,
            options.HeartbeatIntervalSeconds,
            cancellationToken);
    }
}

public sealed class LeaveAccountSessionPacketHandler(
    IActiveAccountLeaseStore accountSessions,
    ActivePlayerRegistry activePlayers,
    IServerClock clock) : IPacketHandler
{
    public PacketType PacketType =>
        PacketType.LeaveAccountSessionRequest;
    public PacketAccessLevel RequiredAccess =>
        PacketAccessLevel.Authenticated;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        if (connection.PlayerSession != null)
        {
            activePlayers.Release(connection);
            if (!connection.TryDetachPlayerSession(out _))
            {
                await SendResponseAsync(
                    connection,
                    false,
                    "The gameplay session could not be released.",
                    cancellationToken);
                return;
            }
        }

        await accountSessions.BeginDisconnectGraceAsync(
            connection.AccountKey!,
            connection.ConnectionId,
            connection.AccountSessionGeneration,
            clock.UtcNow,
            cancellationToken);
        if (!connection.TryDetachAccount())
        {
            await SendResponseAsync(
                connection,
                false,
                "The account session could not be released.",
                cancellationToken);
            return;
        }

        await SendResponseAsync(
            connection,
            true,
            "Choose Play New or sign in.",
            cancellationToken);
    }

    private static Task SendResponseAsync(
        ClientConnection connection,
        bool success,
        string message,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            PacketType.LeaveAccountSessionResponse,
            new LeaveAccountSessionResponsePacket(success, message),
            cancellationToken);
}

public sealed class AccountSessionHeartbeatPacketHandler(
    IActiveAccountLeaseStore accountSessions,
    IServerClock clock) : IPacketHandler
{
    public PacketType PacketType =>
        PacketType.AccountSessionHeartbeatRequest;
    public PacketAccessLevel RequiredAccess =>
        PacketAccessLevel.Authenticated;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        AccountSessionHeartbeatRequestPacket? request =
            JsonSerializer.Deserialize<AccountSessionHeartbeatRequestPacket>(
                payload);
        if (request == null ||
            request.SessionGeneration == Guid.Empty ||
            request.SessionGeneration != connection.AccountSessionGeneration)
        {
            await connection.ForceDisconnectAsync(
                ForcedDisconnectReason.SessionLeaseExpired,
                "Account session lease is invalid.");
            return;
        }

        ActiveAccountLeaseRenewal renewal =
            await accountSessions.RenewAsync(
                connection.AccountKey!,
                connection.ConnectionId,
                request.SessionGeneration,
                clock.UtcNow,
                cancellationToken);
        if (!renewal.Renewed)
        {
            await connection.ForceDisconnectAsync(
                ForcedDisconnectReason.SessionLeaseExpired,
                "Account session lease expired.");
            return;
        }

        await connection.SendAsync(
            PacketType.AccountSessionHeartbeatResponse,
            new AccountSessionHeartbeatResponsePacket(
                true,
                renewal.ExpiresAtUtc),
            cancellationToken);
    }
}

internal static class AuthenticationPacketHandlerSupport
{
    public static async Task CompleteAsync(
        ClientConnection connection,
        AuthenticationResult result,
        PacketType responseType,
        IActiveAccountLeaseStore accountSessions,
        DateTime utcNow,
        int heartbeatIntervalSeconds,
        CancellationToken cancellationToken)
    {
        if (!result.IsSuccess || result.Account == null)
        {
            await connection.SendAsync(
                responseType,
                ToFailureResponse(result.Failure),
                cancellationToken);
            return;
        }

        if (connection.AccountKey != null)
        {
            await connection.SendAsync(
                responseType,
                new AuthenticationResponsePacket(
                    AuthenticationResultCode.AlreadyAuthenticated,
                    "This connection is already authenticated."),
                cancellationToken);
            return;
        }

        ActiveAccountLeaseClaim claim =
            await accountSessions.TryClaimAsync(
                result.Account.AccountKey,
                connection.ConnectionId,
                utcNow,
                cancellationToken);
        if (claim.Status == ActiveAccountLeaseClaimStatus.ActiveElsewhere)
        {
            await connection.SendAsync(
                responseType,
                ToAccountActiveResponse(result.Account),
                cancellationToken);
            return;
        }
        if (claim.Status == ActiveAccountLeaseClaimStatus.CoolingDown)
        {
            int retryAfterSeconds = Math.Max(
                1,
                (int)Math.Ceiling((claim.ExpiresAtUtc - utcNow).TotalSeconds));
            await connection.SendAsync(
                responseType,
                ToSessionConflictResponse(
                    result.Account,
                    retryAfterSeconds),
                cancellationToken);
            return;
        }
        if (claim.Status == ActiveAccountLeaseClaimStatus.CapacityReached)
        {
            Console.WriteLine(
                "[Capacity][Warning] Rejected active account claim: " +
                "server active-account capacity reached.");
            await connection.SendAsync(
                responseType,
                ToServerFullResponse(result.Account),
                cancellationToken);
            return;
        }

        if (!connection.TryAttachAccount(
                result.Account.AccountKey,
                claim.Generation,
                result.Account.IsGuest))
        {
            await accountSessions.ReleaseAsync(
                result.Account.AccountKey,
                connection.ConnectionId,
                claim.Generation,
                cancellationToken);
            await connection.SendAsync(
                responseType,
                new AuthenticationResponsePacket(
                    AuthenticationResultCode.AlreadyAuthenticated,
                    "This connection is already authenticated."),
                cancellationToken);
            return;
        }

        await connection.SendAsync(
            responseType,
            ToSuccessResponse(
                result.Account,
                claim.Generation,
                claim.ExpiresAtUtc,
                heartbeatIntervalSeconds),
            cancellationToken);
    }

    public static Task SendInvalidRequestAsync(
        ClientConnection connection,
        PacketType responseType,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            responseType,
            new AuthenticationResponsePacket(
                AuthenticationResultCode.InvalidRequest,
                "The authentication request is invalid."),
            cancellationToken);

    public static Task SendRateLimitedAsync(
        ClientConnection connection,
        PacketType responseType,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            responseType,
            new AuthenticationResponsePacket(
                AuthenticationResultCode.RateLimited,
                "Too many authentication attempts. Try again later."),
            cancellationToken);

    internal static AuthenticationResponsePacket ToSuccessResponse(
        AuthenticatedAccount account,
        Guid generation,
        DateTime leaseExpiresAtUtc,
        int heartbeatIntervalSeconds) =>
        new(
            AuthenticationResultCode.Success,
            "Authentication successful.",
            account.AccountKey,
            account.IsGuest,
            account.RefreshToken,
            account.RefreshTokenExpiresAtUtc,
            account.DisplayName,
            generation,
            leaseExpiresAtUtc,
            heartbeatIntervalSeconds);

    private static AuthenticationResponsePacket ToSessionConflictResponse(
        AuthenticatedAccount account,
        int retryAfterSeconds) =>
        new(
            AuthenticationResultCode.SessionConflict,
            "Another device is active. Retry is required.",
            account.AccountKey,
            account.IsGuest,
            account.RefreshToken,
            account.RefreshTokenExpiresAtUtc,
            account.DisplayName,
            retryAfterSeconds: retryAfterSeconds);

    private static AuthenticationResponsePacket ToAccountActiveResponse(
        AuthenticatedAccount account) =>
        new(
            AuthenticationResultCode.AccountActive,
            "Tài khoản đang được đăng nhập ở nơi khác.",
            account.AccountKey,
            account.IsGuest,
            account.RefreshToken,
            account.RefreshTokenExpiresAtUtc,
            account.DisplayName);

    private static AuthenticationResponsePacket ToServerFullResponse(
        AuthenticatedAccount account) =>
        new(
            AuthenticationResultCode.ServerFull,
            "Máy chủ hiện đã đầy. Vui lòng thử lại sau.",
            account.AccountKey,
            account.IsGuest,
            account.RefreshToken,
            account.RefreshTokenExpiresAtUtc,
            account.DisplayName);

    internal static AuthenticationResponsePacket ToFailureResponse(
        AuthenticationFailure failure) =>
        failure switch
        {
            AuthenticationFailure.InvalidCredentials => new(
                AuthenticationResultCode.InvalidCredentials,
                "Invalid username or password."),
            AuthenticationFailure.InvalidOrExpiredToken => new(
                AuthenticationResultCode.InvalidOrExpiredToken,
                "The saved session is invalid or expired."),
            AuthenticationFailure.UsernameUnavailable => new(
                AuthenticationResultCode.UsernameUnavailable,
                "That username is unavailable."),
            AuthenticationFailure.GuestNotFound => new(
                AuthenticationResultCode.GuestNotFound,
                "The guest session was not found."),
            AuthenticationFailure.InvalidRequest => new(
                AuthenticationResultCode.InvalidRequest,
                "The authentication request is invalid."),
            _ => new(
                AuthenticationResultCode.InvalidRequest,
                "Authentication failed."),
        };
}
