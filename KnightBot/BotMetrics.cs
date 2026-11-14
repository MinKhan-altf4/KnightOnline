using System.Collections.Concurrent;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Bot;

/// <summary>Thread-safe counters and latency samples shared by all bots.</summary>
public sealed class BotMetrics
{
    private readonly ConcurrentBag<double> _readyMs = new();
    private readonly ConcurrentBag<double> _moveAckMs = new();
    private readonly ConcurrentDictionary<PacketType, ConcurrentBag<double>> _requestMs = new();
    private readonly ConcurrentDictionary<string, int> _failures = new();
    private readonly ConcurrentDictionary<PacketType, long> _packets = new();
    private long _bytesReceived;
    private long _entitySnapshots;
    private long _spawned;
    private long _moved;
    private long _despawned;
    private int _inWorld;

    public int InWorld => Volatile.Read(ref _inWorld);

    public void RecordReady(TimeSpan elapsed)
    {
        _readyMs.Add(elapsed.TotalMilliseconds);
        Interlocked.Increment(ref _inWorld);
    }

    public void RecordRequest(PacketType type, TimeSpan elapsed) =>
        _requestMs.GetOrAdd(type, _ => new ConcurrentBag<double>())
            .Add(elapsed.TotalMilliseconds);

    public void RecordMoveAck(TimeSpan elapsed) => _moveAckMs.Add(elapsed.TotalMilliseconds);

    public void RecordReceived(PacketType type, int bytes)
    {
        _packets.AddOrUpdate(type, 1, (_, count) => count + 1);
        Interlocked.Add(ref _bytesReceived, bytes);
    }

    public void RecordEntities(int spawned, int moved, int despawned)
    {
        Interlocked.Increment(ref _entitySnapshots);
        Interlocked.Add(ref _spawned, spawned);
        Interlocked.Add(ref _moved, moved);
        Interlocked.Add(ref _despawned, despawned);
    }

    public void RecordFailure(BotStage stage, string reason) =>
        _failures.AddOrUpdate($"{stage}: {Trim(reason)}", 1, (_, count) => count + 1);

    public void RecordDisconnect(BotStage stage) =>
        RecordFailure(stage, "server closed connection");

    public void RecordForcedDisconnect(string reason) =>
        _failures.AddOrUpdate($"ForcedDisconnect: {reason}", 1, (_, count) => count + 1);

    public void LeftWorld() => Interlocked.Decrement(ref _inWorld);

    public string Report(int bots, TimeSpan duration)
    {
        var lines = new List<string>
        {
            $"Bots requested     : {bots}",
            $"Reached world      : {_readyMs.Count}",
            $"Time to world (ms) : {Summary(_readyMs)}",
            $"Move ack RTT (ms)  : {Summary(_moveAckMs)}",
        };
        foreach ((PacketType type, ConcurrentBag<double> samples) in _requestMs.OrderBy(pair => pair.Key))
            lines.Add($"  {type,-38}: {Summary(samples)}");

        double seconds = Math.Max(1, duration.TotalSeconds);
        long snapshots = Interlocked.Read(ref _entitySnapshots);
        lines.Add($"Entity snapshots   : {snapshots} " +
                  $"({snapshots / seconds:0.0}/s total, spawn {_spawned}, move {_moved}, despawn {_despawned})");
        lines.Add($"Received           : {Interlocked.Read(ref _bytesReceived) / 1024.0 / seconds:0.0} KiB/s total");
        foreach ((PacketType type, long count) in _packets.OrderByDescending(pair => pair.Value).Take(8))
            lines.Add($"  {type,-38}: {count}");
        lines.Add(_failures.IsEmpty ? "Failures           : none" : "Failures:");
        foreach ((string reason, int count) in _failures.OrderByDescending(pair => pair.Value))
            lines.Add($"  {count,5} x {reason}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Summary(IEnumerable<double> values)
    {
        double[] sorted = values.Order().ToArray();
        if (sorted.Length == 0)
            return "n/a";
        return $"n={sorted.Length} p50={Percentile(sorted, 0.50):0.0} " +
               $"p95={Percentile(sorted, 0.95):0.0} p99={Percentile(sorted, 0.99):0.0} " +
               $"max={sorted[^1]:0.0}";
    }

    private static double Percentile(double[] sorted, double percentile) =>
        sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];

    private static string Trim(string value) =>
        value.Length <= 120 ? value : value[..120];
}
