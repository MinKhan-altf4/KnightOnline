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
            !await OwnsLiveAccountLeaseAsync(
                connection,
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

    private async ValueTask<bool> OwnsLiveAccountLeaseAsync(
        ClientConnection connection,
        CancellationToken cancellationToken) =>
        connection.AccountKey != null &&
        connection.AccountSessionGeneration != Guid.Empty &&
        await _accountSessions.IsOwnerAsync(
            connection.AccountKey,
            connection.ConnectionId,
            connection.AccountSessionGeneration,
            _clock.UtcNow,
            cancellationToken);

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
