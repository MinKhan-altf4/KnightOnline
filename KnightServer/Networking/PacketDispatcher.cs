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

        await handler.HandleAsync(
            connection,
            envelope.Payload,
            cancellationToken);
    }
}
