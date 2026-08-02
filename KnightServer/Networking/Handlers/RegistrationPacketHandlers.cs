using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Time;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class BeginRegistrationPacketHandler(
    RegistrationFlowService registration) : IPacketHandler
{
    public PacketType PacketType => PacketType.BeginRegistrationRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        BeginRegistrationRequestPacket? request =
            JsonSerializer.Deserialize<BeginRegistrationRequestPacket>(
                payload);
        if (request == null || !connection.IsGuest)
        {
            await SendFailureAsync(
                connection,
                "A valid guest session is required.",
                cancellationToken);
            return;
        }

        RegistrationStartResult result = await registration.BeginAsync(
            request.RequestId,
            request.GuestRefreshToken,
            request.DeviceId,
            request.PkceChallenge,
            cancellationToken);

        await connection.SendAsync(
            PacketType.BeginRegistrationResponse,
            new BeginRegistrationResponsePacket(
                result.IsSuccess,
                result.IsSuccess
                    ? "Registration transaction created."
                    : "Registration request is invalid.",
                result.TransactionId ?? Guid.Empty,
                result.RegistrationUrl,
                result.DevelopmentAuthorizationCode,
                result.ExpiresAtUtc),
            cancellationToken);
    }

    private static Task SendFailureAsync(
        ClientConnection connection,
        string message,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            PacketType.BeginRegistrationResponse,
            new BeginRegistrationResponsePacket(false, message),
            cancellationToken);
}

/// <summary>
/// Local-only stand-in for the registration website callback. Production
/// must disable this handler and let the web account service complete the
/// transaction without sending a password through the game transport.
/// </summary>
public sealed class CompleteDevelopmentRegistrationPacketHandler(
    RegistrationFlowService registration,
    bool enabled,
    IActiveAccountLeaseStore accountSessions,
    IServerClock clock,
    AuthenticationOptions authenticationOptions) : IPacketHandler
{
    public PacketType PacketType =>
        PacketType.CompleteDevelopmentRegistrationRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        if (!enabled || !connection.IsGuest)
        {
            await SendFailureAsync(
                connection,
                AuthenticationFailure.InvalidRequest,
                cancellationToken);
            return;
        }

        CompleteDevelopmentRegistrationRequestPacket? request =
            JsonSerializer.Deserialize<
                CompleteDevelopmentRegistrationRequestPacket>(payload);
        if (request == null)
        {
            await SendFailureAsync(
                connection,
                AuthenticationFailure.InvalidRequest,
                cancellationToken);
            return;
        }

        RegistrationCompletionResult result =
            await registration.CompleteDevelopmentAsync(
                request.TransactionId,
                request.AuthorizationCode,
                request.PkceVerifier,
                request.Username,
                request.Password,
                cancellationToken);

        if (result.Authentication.IsSuccess &&
            result.Authentication.Account is { } account)
        {
            connection.MarkAccountRegistered();
            ActiveAccountLeaseRenewal renewal =
                await accountSessions.RenewAsync(
                    connection.AccountKey!,
                    connection.ConnectionId,
                    connection.AccountSessionGeneration,
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
                PacketType.CompleteDevelopmentRegistrationResponse,
                AuthenticationPacketHandlerSupport.ToSuccessResponse(
                    account,
                    connection.AccountSessionGeneration,
                    renewal.ExpiresAtUtc,
                    authenticationOptions.HeartbeatIntervalSeconds),
                cancellationToken);
            return;
        }

        await SendFailureAsync(
            connection,
            result.Authentication.Failure,
            cancellationToken);
    }

    private static Task SendFailureAsync(
        ClientConnection connection,
        AuthenticationFailure failure,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            PacketType.CompleteDevelopmentRegistrationResponse,
            AuthenticationPacketHandlerSupport.ToFailureResponse(failure),
            cancellationToken);
}
