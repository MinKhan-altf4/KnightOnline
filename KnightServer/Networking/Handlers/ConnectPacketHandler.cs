using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class ConnectPacketHandler(
    IAccountIdentityProvider identities,
    AccountSessionRegistry accountSessions,
    bool developmentBypassEnabled) : IPacketHandler
{
    public PacketType PacketType => PacketType.ConnectRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        ConnectRequestPacket? request =
            JsonSerializer.Deserialize<ConnectRequestPacket>(payload);

        if (request == null)
            return;

        if (!developmentBypassEnabled)
        {
            await connection.SendAsync(
                PacketType.ConnectResponse,
                new ConnectResponsePacket(
                    ConnectResult.Success,
                    "Transport connected. Authentication is required."),
                cancellationToken);
            return;
        }

        string accountKey =
            identities.ResolveAccountKey(connection, request);

        if (connection.AccountKey == null &&
            !connection.TryAttachAccount(accountKey))
            return;

        ClientConnection? conflictingConnection =
            accountSessions.Register(accountKey, connection);

        if (conflictingConnection != null)
        {
            const string message =
                "Duplicate account session detected. Both connections were closed.";
            Console.WriteLine(
                $"[Security] Duplicate session for account '{accountKey}'.");

            await Task.WhenAll(
                conflictingConnection.ForceDisconnectAsync(
                    ForcedDisconnectReason.DuplicateAccountSession,
                    message),
                connection.ForceDisconnectAsync(
                    ForcedDisconnectReason.DuplicateAccountSession,
                    message));
            return;
        }

        await connection.SendAsync(
            PacketType.ConnectResponse,
            new ConnectResponsePacket(
                ConnectResult.Success,
                "Welcome to KnightOnline!"),
            cancellationToken);
    }
}
