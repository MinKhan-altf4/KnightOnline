using System.Collections.Concurrent;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server.Networking;

public readonly record struct ConnectionCapacitySnapshot(
    int ConnectionCount,
    int MaximumTransportConnections);

public sealed class ConcurrentCapacityGate
{
    private readonly int _maximum;
    private int _count;

    public ConcurrentCapacityGate(int maximum)
    {
        if (maximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        _maximum = maximum;
    }

    public bool TryEnter()
    {
        while (true)
        {
            int current = Volatile.Read(ref _count);
            if (current >= _maximum)
                return false;
            if (Interlocked.CompareExchange(
                    ref _count,
                    current + 1,
                    current) == current)
                return true;
        }
    }

    public void Exit()
    {
        int result = Interlocked.Decrement(ref _count);
        if (result < 0)
        {
            Interlocked.Increment(ref _count);
            throw new InvalidOperationException(
                "Capacity gate was released without ownership.");
        }
    }

    public int Count => Volatile.Read(ref _count);
    public int Maximum => _maximum;
}

public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<ClientConnection, byte> _connections =
        new();
    private readonly int _maximumConnections;
    private readonly ConcurrentCapacityGate _capacity;

    public ConnectionRegistry(int maximumConnections)
    {
        if (maximumConnections <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumConnections));
        _maximumConnections = maximumConnections;
        _capacity = new ConcurrentCapacityGate(maximumConnections);
    }

    public bool TryAdd(ClientConnection connection)
    {
        if (!_capacity.TryEnter())
            return false;

        if (_connections.TryAdd(connection, 0))
            return true;

        _capacity.Exit();
        return false;
    }

    public void Remove(ClientConnection connection)
    {
        if (_connections.TryRemove(connection, out _))
            _capacity.Exit();
    }

    public ConnectionCapacitySnapshot GetCapacity() =>
        new(
            _capacity.Count,
            _maximumConnections);

    /// <summary>
    /// Non-blocking: queues on every connection; slow consumers are closed by
    /// <see cref="ClientConnection.TryEnqueue{T}"/>. Returns the number queued.
    /// </summary>
    public int Broadcast<T>(PacketType packetType, T payload)
    {
        int delivered = 0;
        foreach (ClientConnection connection in _connections.Keys)
        {
            if (connection.TryEnqueue(packetType, payload))
                delivered++;
        }
        return delivered;
    }

    /// <summary>Non-blocking broadcast to players on one map.</summary>
    public int BroadcastToMap<T>(
        string mapDefinitionId,
        PacketType packetType,
        T payload)
    {
        int delivered = 0;
        foreach (ClientConnection connection in _connections.Keys)
        {
            if (!string.Equals(
                    connection.PlayerSession?.MapDefinitionId,
                    mapDefinitionId,
                    StringComparison.Ordinal))
                continue;

            if (connection.TryEnqueue(packetType, payload))
                delivered++;
        }
        return delivered;
    }
}
