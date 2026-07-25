using System.Collections.Concurrent;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server.Networking;

public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<ClientConnection, byte> _connections = new();

    public void Add(ClientConnection connection) =>
        _connections.TryAdd(connection, 0);

    public void Remove(ClientConnection connection) =>
        _connections.TryRemove(connection, out _);

    public async Task BroadcastAsync<T>(PacketType packetType, T payload)
    {
        foreach (ClientConnection connection in _connections.Keys)
        {
            try
            {
                await connection.SendAsync(packetType, payload);
            }
            catch (Exception exception)
            {
                // The connection's receive loop owns disposal/removal.
                Console.WriteLine(
                    $"[Network][Warning] Broadcast delivery failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
