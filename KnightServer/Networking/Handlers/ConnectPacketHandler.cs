using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;

namespace KnightOnline.Server.Networking.Handlers;

/// <summary>
/// Transport handshake. Rejects clients whose protocol version is outside the
/// supported range (B10) and marks the connection as handshaken so the
/// dispatcher accepts other packets.
/// </summary>
public sealed class ConnectPacketHandler(
    IAccountIdentityProvider identities,
    bool developmentBypassEnabled,
    int minimumProtocolVersion = PacketProtocol.Version,
    int maximumProtocolVersion = PacketProtocol.Version) : IPacketHandler
{
    public const string VersionMismatchMessage =
        "Phiên bản game không còn được hỗ trợ. Vui lòng cập nhật để tiếp tục.";

    public PacketType PacketType => PacketType.ConnectRequest;
    public PacketAccessLevel RequiredAccess => PacketAccessLevel.Anonymous;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        ConnectRequestPacket? request;
        try
        {
            request = JsonSerializer.Deserialize<ConnectRequestPacket>(payload);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request == null)
            return;

        if (request.ProtocolVersion < minimumProtocolVersion ||
            request.ProtocolVersion > maximumProtocolVersion)
        {
            Console.WriteLine(
                "[Network][Info] Rejected protocol version " +
                $"{request.ProtocolVersion} from {connection.RemoteAddress} " +
                $"(supported {minimumProtocolVersion}-{maximumProtocolVersion}).");
            await connection.SendAsync(
                PacketType.ConnectResponse,
                new ConnectResponsePacket(
                    ConnectResult.VersionMismatch,
                    VersionMismatchMessage),
                cancellationToken);
            await connection.CloseAfterFlushAsync();
            return;
        }

        connection.MarkHandshakeCompleted();

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
