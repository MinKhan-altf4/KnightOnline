using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server.Networking;

public sealed class PacketDispatcher
{
    private readonly IReadOnlyDictionary<PacketType, IPacketHandler> _handlers;

    public PacketDispatcher(IEnumerable<IPacketHandler> handlers)
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

        if (!HasRequiredAccess(connection, handler.RequiredAccess))
        {
            Console.WriteLine(
                $"[Security][Warning] Rejected packet {envelope.Type} from " +
                $"{connection.RemoteAddress}: required access is " +
                $"{handler.RequiredAccess}.");
            return;
        }

        await handler.HandleAsync(
            connection,
            envelope.Payload,
            cancellationToken);
    }

    private static bool HasRequiredAccess(
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
