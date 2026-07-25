using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class ConnectPacketHandler(
    IAccountIdentityProvider identities,
    bool developmentBypassEnabled) : IPacketHandler
{
    public PacketType PacketType => PacketType.ConnectRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.Anonymous;

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

        await connection.SendAsync(
            PacketType.ConnectResponse,
            new ConnectResponsePacket(
                ConnectResult.Success,
                "Welcome to KnightOnline!"),
            cancellationToken);
    }
}
