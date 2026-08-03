using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Time;

namespace KnightOnline.Server.Networking;

public sealed class PacketDispatcher
{
    private readonly IReadOnlyDictionary<PacketType, IPacketHandler> _handlers;
    private readonly IActiveAccountLeaseStore _accountSessions;
    private readonly IServerClock _clock;

    public PacketDispatcher(
        IEnumerable<IPacketHandler> handlers,
        IActiveAccountLeaseStore accountSessions,
        IServerClock clock)
    {
        var handlerMap = new Dictionary<PacketType, IPacketHandler>();

        foreach (IPacketHandler handler in handlers)
        {
            if (!handlerMap.TryAdd(handler.PacketType, handler))
            {
                throw new InvalidOperationException(
                    $"A handler is already registered for packet type {handler.PacketType}.");
            }
        }

        _handlers = handlerMap;
        _accountSessions = accountSessions;
        _clock = clock;
    }

    public async Task DispatchAsync(
        ClientConnection connection,
        PacketEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(envelope.Type, out IPacketHandler? handler))
        {
            Console.WriteLine(
                $"[Network] Ignored packet without handler: {envelope.Type}.");
            return;
        }

        if (!HasRequiredConnectionState(
                connection,
                handler.RequiredAccess))
        {
            Console.WriteLine(
                $"[Security][Warning] Rejected packet {envelope.Type} from " +
                $"{connection.RemoteAddress}: required access is " +
                $"{handler.RequiredAccess}.");
            return;
        }

        if (handler.RequiredAccess != PacketAccessLevel.Anonymous &&
            !await MaintainLiveAccountLeaseAsync(
                connection,
                envelope.Type,
                cancellationToken))
        {
            Console.WriteLine(
                $"[Security][Warning] Rejected packet {envelope.Type}: " +
                "account session lease is expired or stale.");
            await connection.ForceDisconnectAsync(
                ForcedDisconnectReason.SessionLeaseExpired,
                "Account session lease expired.");
            return;
        }

        await handler.HandleAsync(
            connection,
            envelope.Payload,
            cancellationToken);
    }

    private async ValueTask<bool> MaintainLiveAccountLeaseAsync(
        ClientConnection connection,
        PacketType packetType,
        CancellationToken cancellationToken)
    {
        if (connection.AccountKey == null ||
            connection.AccountSessionGeneration == Guid.Empty)
            return false;

        // Any authenticated traffic proves that this connection is alive.
        // Renewing here prevents an actively moving/fighting player from
        // losing the lease because a heartbeat is delayed by a frame stall.
        // The heartbeat handler performs its own renewal so it can return the
        // authoritative expiry timestamp to the client.
        if (packetType == PacketType.AccountSessionHeartbeatRequest)
        {
            return await _accountSessions.IsOwnerAsync(
                connection.AccountKey,
                connection.ConnectionId,
                connection.AccountSessionGeneration,
                _clock.UtcNow,
                cancellationToken);
        }

        ActiveAccountLeaseRenewal renewal =
            await _accountSessions.RenewAsync(
                connection.AccountKey,
                connection.ConnectionId,
                connection.AccountSessionGeneration,
                _clock.UtcNow,
                cancellationToken);
        return renewal.Renewed;
    }

    private static bool HasRequiredConnectionState(
        ClientConnection connection,
        PacketAccessLevel requiredAccess) =>
        requiredAccess switch
        {
            PacketAccessLevel.Anonymous => true,
            PacketAccessLevel.Authenticated =>
                connection.AccountKey != null,
            PacketAccessLevel.CharacterSelected =>
                connection.AccountKey != null &&
                connection.PlayerSession != null,
            _ => false,
        };
}
