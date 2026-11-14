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
    int MaximumVisiblePlayers,
    DeadReckoningSettings? DeadReckoning = null);

/// <summary>Time spent per phase of an entity-snapshot tick (diagnostics).</summary>
public readonly record struct WorldTickPhases(
    TimeSpan Advance,
    TimeSpan SelectVisible,
    TimeSpan TrackDelta,
    TimeSpan BuildJson,
    TimeSpan Enqueue);

public readonly record struct WorldTickResult(
    long Tick,
    int PlayersInWorld,
    int EntitySnapshotsQueued,
    TimeSpan Elapsed,
    WorldTickPhases Phases = default);

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
    // Reused per observer inside one tick (the tick is single-threaded).
    private readonly List<WorldPlayerState> _visibleBuffer = new();
    private readonly List<int> _candidateBuffer = new();
    private float[] _keyBuffer = new float[256];
    private int[] _indexBuffer = new int[256];
    private readonly Dictionary<int, (string Map, Vector2 Position)> _previousPositions = new();
    private DateTime _previousTickUtc;
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

        double elapsedSeconds = _previousTickUtc == default
            ? 0
            : (utcNow - _previousTickUtc).TotalSeconds;
        _previousTickUtc = utcNow;

        var inWorld = new List<(ClientConnection Connection, WorldPlayerState State)>();
        var present = new HashSet<int>();
        foreach (ClientConnection connection in _players.GetConnectionsSnapshot())
        {
            PlayerSession? session = connection.PlayerSession;
            if (session == null || !session.IsInWorld || connection.IsClosed)
                continue;

            session.AdvancePosition(utcNow, _movementResolver);
            WorldPlayerState state = WithEffectiveDirection(Capture(session), elapsedSeconds);
            present.Add(state.CharacterId);
            inWorld.Add((connection, state));
        }

        foreach (int characterId in _previousPositions.Keys.ToArray())
        {
            if (!present.Contains(characterId))
                _previousPositions.Remove(characterId);
        }

        TimeSpan advance = Stopwatch.GetElapsedTime(started);
        int queued = 0;
        WorldTickPhases phases = new(advance, default, default, default, default);
        if (tick % _settings.EntitySnapshotEveryTicks == 0)
            queued = SendEntitySnapshots(tick, utcNow, inWorld, ref phases);

        return new WorldTickResult(tick, inWorld.Count, queued,
            Stopwatch.GetElapsedTime(started), phases);
    }

    private int SendEntitySnapshots(long tick, DateTime utcNow,
        List<(ClientConnection Connection, WorldPlayerState State)> inWorld,
        ref WorldTickPhases phases)
    {
        var activeObservers = new HashSet<Guid>();
        int queued = 0;
        long select = 0, track = 0, build = 0, enqueue = 0;
        _snapshotJson.BeginTick();
        WorldPlayerState[] states = new WorldPlayerState[inWorld.Count];
        for (int index = 0; index < states.Length; index++)
            states[index] = inWorld[index].State;
        long bucketStart = Stopwatch.GetTimestamp();
        AoiBuckets buckets = BuildBuckets(states);
        select += Stopwatch.GetTimestamp() - bucketStart;

        foreach ((ClientConnection observer, WorldPlayerState self) in inWorld)
        {
            activeObservers.Add(observer.ConnectionId);
            if (!_observers.TryGetValue(observer.ConnectionId,
                    out VisibilityTracker? tracker))
            {
                tracker = new VisibilityTracker(_settings.DeadReckoning);
                _observers.Add(observer.ConnectionId, tracker);
            }

            long mark = Stopwatch.GetTimestamp();
            List<WorldPlayerState> visible = SelectVisible(self, states, buckets, tracker);
            long afterSelect = Stopwatch.GetTimestamp();
            VisibilityDelta delta = tracker.Update(visible, utcNow);
            long afterTrack = Stopwatch.GetTimestamp();
            select += afterSelect - mark;
            track += afterTrack - afterSelect;
            if (delta.IsEmpty)
                continue;

            string payload = _snapshotJson.Build(tick, utcNow, delta);
            long afterBuild = Stopwatch.GetTimestamp();
            if (observer.TryEnqueuePayloadJson(PacketType.WorldEntitiesSnapshot, payload))
                queued++;
            build += afterBuild - afterTrack;
            enqueue += Stopwatch.GetTimestamp() - afterBuild;
        }

        phases = phases with
        {
            SelectVisible = Stopwatch.GetElapsedTime(0, select),
            TrackDelta = Stopwatch.GetElapsedTime(0, track),
            BuildJson = Stopwatch.GetElapsedTime(0, build),
            Enqueue = Stopwatch.GetElapsedTime(0, enqueue),
        };

        // Forget observers that left the world, logged out or disconnected so
        // a later re-entry starts with a full spawn list.
        foreach (Guid connectionId in _observers.Keys.ToArray())
        {
            if (!activeObservers.Contains(connectionId))
                _observers.Remove(connectionId);
        }

        return queued;
    }

    /// <summary>
    /// Distance bonus (world units) that keeps an already visible player in
    /// a crowded observer's capped set, so the "nearest N" boundary does not
    /// churn spawn/despawn every snapshot.
    /// </summary>
    public const float CrowdHysteresisDistance = 2f;

    private List<WorldPlayerState> SelectVisible(WorldPlayerState self,
        WorldPlayerState[] states, AoiBuckets buckets, VisibilityTracker tracker)
    {
        _visibleBuffer.Clear();
        _candidateBuffer.Clear();
        (int cellX, int cellY) = _aoi.CellOf(self.Position);
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (!buckets.TryGetValue((self.MapDefinitionId, cellX + dx, cellY + dy),
                    out List<int>? cell))
                continue;
            foreach (int index in cell)
            {
                if (states[index].CharacterId != self.CharacterId)
                    _candidateBuffer.Add(index);
            }
        }

        int maximum = _settings.MaximumVisiblePlayers;
        int count = _candidateBuffer.Count;
        if (count > maximum)
        {
            // Crowded: keep the nearest players so packet size stays bounded.
            // Sort int indices by float keys (cheap); already visible players
            // get a hysteresis bonus so the boundary does not churn.
            if (_keyBuffer.Length < count)
            {
                _keyBuffer = new float[count * 2];
                _indexBuffer = new int[count * 2];
            }
            for (int item = 0; item < count; item++)
            {
                int index = _candidateBuffer[item];
                float distance = Vector2.Distance(self.Position, states[index].Position);
                if (tracker.IsVisible(states[index].CharacterId))
                    distance -= CrowdHysteresisDistance;
                _keyBuffer[item] = distance;
                _indexBuffer[item] = index;
            }
            Array.Sort(_keyBuffer, _indexBuffer, 0, count);
            for (int item = 0; item < maximum; item++)
                _visibleBuffer.Add(states[_indexBuffer[item]]);
            return _visibleBuffer;
        }

        foreach (int index in _candidateBuffer)
            _visibleBuffer.Add(states[index]);
        return _visibleBuffer;
    }

    private AoiBuckets BuildBuckets(WorldPlayerState[] states)
    {
        var buckets = new AoiBuckets();
        for (int index = 0; index < states.Length; index++)
        {
            (int x, int y) = _aoi.CellOf(states[index].Position);
            var key = (states[index].MapDefinitionId, x, y);
            if (!buckets.TryGetValue(key, out List<int>? cell))
            {
                cell = new List<int>();
                buckets.Add(key, cell);
            }
            cell.Add(index);
        }
        return buckets;
    }

    private sealed class AoiBuckets : Dictionary<(string Map, int X, int Y), List<int>>
    {
    }

    /// <summary>
    /// Clients extrapolate with Direction * Speed, so report a stopped
    /// direction when the player did not actually move this tick (blocked by
    /// a wall or monster); otherwise the client would walk them into it.
    /// </summary>
    private WorldPlayerState WithEffectiveDirection(WorldPlayerState state,
        double elapsedSeconds)
    {
        bool hadPrevious = _previousPositions.TryGetValue(state.CharacterId,
            out (string Map, Vector2 Position) previous);
        _previousPositions[state.CharacterId] = (state.MapDefinitionId, state.Position);

        if (state.Direction == Vector2.Zero || !hadPrevious || elapsedSeconds <= 0 ||
            previous.Map != state.MapDefinitionId)
            return state;

        float expected = state.Direction.Length() * state.Speed * (float)elapsedSeconds;
        float actual = Vector2.Distance(previous.Position, state.Position);
        return actual < expected * 0.1f ? state with { Direction = Vector2.Zero } : state;
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
            movement.Direction,
            session.MoveSpeed);
    }

}
