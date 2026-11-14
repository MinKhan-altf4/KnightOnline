using System.Diagnostics;
using System.Numerics;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Players;
using KnightOnline.Server.Time;

namespace KnightOnline.Server.World;

public sealed record WorldSimulationSettings(
    TimeSpan TickInterval,
    int EntitySnapshotEveryTicks,
    float AoiCellSize,
    int MaximumVisiblePlayers);

public readonly record struct WorldTickResult(
    long Tick,
    int PlayersInWorld,
    int EntitySnapshotsQueued,
    TimeSpan Elapsed);

/// <summary>
/// Fixed-rate world tick (World_Simulation_P1_Design §5-6). Each tick advances
/// every in-world player to "now"; every N ticks it sends each player the
/// spawn/move/despawn delta of other players in its area of interest.
///
/// <see cref="Tick"/> is synchronous and never awaits the network (it only
/// calls <see cref="ClientConnection.TryEnqueue{T}"/>), so it is unit
/// testable and a slow client cannot stall the world.
/// </summary>
public sealed class WorldSimulation
{
    private readonly ActivePlayerRegistry _players;
    private readonly IWorldMovementResolver _movementResolver;
    private readonly IServerClock _clock;
    private readonly WorldSimulationSettings _settings;
    private readonly AoiGrid _aoi;
    private readonly Dictionary<Guid, VisibilityTracker> _observers = new();
    private readonly WorldSnapshotJson _snapshotJson = new();
    private long _tick;

    public WorldSimulation(
        ActivePlayerRegistry players,
        IWorldMovementResolver movementResolver,
        IServerClock clock,
        WorldSimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.TickInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(settings));
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.EntitySnapshotEveryTicks, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MaximumVisiblePlayers, 1);
        _players = players;
        _movementResolver = movementResolver;
        _clock = clock;
        _settings = settings;
        _aoi = new AoiGrid(settings.AoiCellSize);
    }

    public long CurrentTick => Interlocked.Read(ref _tick);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var timer = new PeriodicTimer(_settings.TickInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                WorldTickResult result = Tick();
                if (result.Elapsed > _settings.TickInterval)
                {
                    Console.WriteLine(
                        $"[World][Warning] Tick {result.Tick} took " +
                        $"{result.Elapsed.TotalMilliseconds:0.0} ms " +
                        $"(budget {_settings.TickInterval.TotalMilliseconds:0} ms, " +
                        $"{result.PlayersInWorld} players).");
                }
            }
            catch (Exception exception)
            {
                // One bad tick must not stop the world.
                Console.WriteLine(
                    $"[World][Error] Tick {CurrentTick} failed: {exception}");
            }
        }
    }

    public WorldTickResult Tick()
    {
        long started = Stopwatch.GetTimestamp();
        long tick = Interlocked.Increment(ref _tick);
        DateTime utcNow = _clock.UtcNow;

        var inWorld = new List<(ClientConnection Connection, WorldPlayerState State)>();
        foreach (ClientConnection connection in _players.GetConnectionsSnapshot())
        {
            PlayerSession? session = connection.PlayerSession;
            if (session == null || !session.IsInWorld || connection.IsClosed)
                continue;

            session.AdvancePosition(utcNow, _movementResolver);
            inWorld.Add((connection, Capture(session)));
        }

        int queued = 0;
        if (tick % _settings.EntitySnapshotEveryTicks == 0)
            queued = SendEntitySnapshots(tick, utcNow, inWorld);

        return new WorldTickResult(tick, inWorld.Count, queued,
            Stopwatch.GetElapsedTime(started));
    }

    private int SendEntitySnapshots(long tick, DateTime utcNow,
        List<(ClientConnection Connection, WorldPlayerState State)> inWorld)
    {
        var activeObservers = new HashSet<Guid>();
        int queued = 0;
        _snapshotJson.BeginTick();

        foreach ((ClientConnection observer, WorldPlayerState self) in inWorld)
        {
            activeObservers.Add(observer.ConnectionId);
            if (!_observers.TryGetValue(observer.ConnectionId,
                    out VisibilityTracker? tracker))
            {
                tracker = new VisibilityTracker();
                _observers.Add(observer.ConnectionId, tracker);
            }

            VisibilityDelta delta = tracker.Update(SelectVisible(self, inWorld));
            if (delta.IsEmpty)
                continue;

            if (observer.TryEnqueuePayloadJson(PacketType.WorldEntitiesSnapshot,
                    _snapshotJson.Build(tick, utcNow, delta)))
                queued++;
        }

        // Forget observers that left the world, logged out or disconnected so
        // a later re-entry starts with a full spawn list.
        foreach (Guid connectionId in _observers.Keys.ToArray())
        {
            if (!activeObservers.Contains(connectionId))
                _observers.Remove(connectionId);
        }

        return queued;
    }

    private List<WorldPlayerState> SelectVisible(WorldPlayerState self,
        List<(ClientConnection Connection, WorldPlayerState State)> inWorld)
    {
        var visible = new List<WorldPlayerState>();
        foreach ((_, WorldPlayerState other) in inWorld)
        {
            if (other.CharacterId == self.CharacterId)
                continue;
            if (_aoi.IsVisible(self.MapDefinitionId, self.Position,
                    other.MapDefinitionId, other.Position))
                visible.Add(other);
        }

        if (visible.Count <= _settings.MaximumVisiblePlayers)
            return visible;

        // Crowded: keep the nearest players so packet size stays bounded.
        visible.Sort((a, b) =>
            Vector2.DistanceSquared(self.Position, a.Position)
                .CompareTo(Vector2.DistanceSquared(self.Position, b.Position)));
        visible.RemoveRange(_settings.MaximumVisiblePlayers,
            visible.Count - _settings.MaximumVisiblePlayers);
        return visible;
    }

    private static WorldPlayerState Capture(PlayerSession session)
    {
        PlayerMovementState movement = session.CaptureMovementState();
        return new WorldPlayerState(
            session.CharacterId,
            session.CharacterName,
            session.Level,
            session.Profile.ClassDefinitionId,
            movement.MapDefinitionId,
            movement.Position,
            movement.Direction);
    }

}
