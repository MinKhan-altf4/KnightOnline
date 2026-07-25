using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Time;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class CreateGuestPacketHandler(
    AccountAuthenticationService authentication,
    AccountSessionRegistry accountSessions,
    AuthenticationRateLimiter rateLimiter,
    IServerClock clock) : IPacketHandler
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
            cancellationToken);
    }
}

public sealed class ResumeAccountPacketHandler(
    AccountAuthenticationService authentication,
    AccountSessionRegistry accountSessions,
    AuthenticationRateLimiter rateLimiter,
    IServerClock clock) : IPacketHandler
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
            cancellationToken);
    }
}

public sealed class LoginPacketHandler(
    AccountAuthenticationService authentication,
    AccountSessionRegistry accountSessions,
    AuthenticationRateLimiter rateLimiter,
    IServerClock clock) : IPacketHandler
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
            cancellationToken);
    }
}

internal static class AuthenticationPacketHandlerSupport
{
    public static async Task CompleteAsync(
        ClientConnection connection,
        AuthenticationResult result,
        PacketType responseType,
        AccountSessionRegistry accountSessions,
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

        if (connection.AccountKey != null ||
            !connection.TryAttachAccount(
                result.Account.AccountKey,
                result.Account.IsGuest))
        {
            await connection.SendAsync(
                responseType,
                new AuthenticationResponsePacket(
                    AuthenticationResultCode.AlreadyAuthenticated,
                    "This connection is already authenticated."),
                cancellationToken);
            return;
        }

        ClientConnection? conflict = accountSessions.Register(
            result.Account.AccountKey,
            connection);
        if (conflict == null)
        {
            await connection.SendAsync(
                responseType,
                ToSuccessResponse(result.Account),
                cancellationToken);
            return;
        }

        // Deliver the rotated token without marking the session ready. The
        // client stores it and retries after the forced-disconnect backoff.
        await connection.SendAsync(
            responseType,
            ToSessionConflictResponse(result.Account),
            cancellationToken);

        const string message =
            "Duplicate account session detected. Both connections were closed.";
        await Task.WhenAll(
            conflict.ForceDisconnectAsync(
                ForcedDisconnectReason.DuplicateAccountSession,
                message),
            connection.ForceDisconnectAsync(
                ForcedDisconnectReason.DuplicateAccountSession,
                message));
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

    private static AuthenticationResponsePacket ToSuccessResponse(
        AuthenticatedAccount account) =>
        new(
            AuthenticationResultCode.Success,
            "Authentication successful.",
            account.AccountKey,
            account.IsGuest,
            account.RefreshToken,
            account.RefreshTokenExpiresAtUtc);

    private static AuthenticationResponsePacket ToSessionConflictResponse(
        AuthenticatedAccount account) =>
        new(
            AuthenticationResultCode.SessionConflict,
            "Another device is active. Retry is required.",
            account.AccountKey,
            account.IsGuest,
            account.RefreshToken,
            account.RefreshTokenExpiresAtUtc);

    private static AuthenticationResponsePacket ToFailureResponse(
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
