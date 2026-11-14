using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Players;
using KnightOnline.Server.Time;
using KnightOnline.Server.World;

namespace KnightServer.Tests.World;

public sealed class WorldSimulationTests
{
    private static readonly DateTime Start =
        new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    // ---- AoiGrid -------------------------------------------------------

    [Theory]
    [InlineData(0f, 0f, 15.9f, 15.9f, true)]   // same cell
    [InlineData(0f, 0f, 31.9f, 0f, true)]      // neighbour cell
    [InlineData(0f, 0f, 32.1f, 0f, false)]     // two cells away
    [InlineData(-0.1f, 0f, 16.1f, 0f, false)]  // cell -1 vs cell 1
    public void Aoi_UsesThreeByThreeNeighbourhood(float ax, float ay,
        float bx, float by, bool expected)
    {
        var aoi = new AoiGrid(16f);

        Assert.Equal(expected, aoi.IsVisible("map", new Vector2(ax, ay),
            "map", new Vector2(bx, by)));
    }

    [Fact]
    public void Aoi_DifferentMap_IsNeverVisible()
    {
        var aoi = new AoiGrid(16f);

        Assert.False(aoi.IsVisible("a", Vector2.Zero, "b", Vector2.Zero));
    }

    // ---- VisibilityTracker --------------------------------------------

    [Fact]
    public void Tracker_ProducesSpawnMoveDespawn()
    {
        var tracker = new VisibilityTracker();
        WorldPlayerState other = State(2, new Vector2(1, 1));

        VisibilityDelta first = tracker.Update([other]);
        Assert.Single(first.Spawned);
        Assert.Empty(first.Moved);

        VisibilityDelta unchanged = tracker.Update([other]);
        Assert.True(unchanged.IsEmpty);

        VisibilityDelta moved = tracker.Update(
            [other with { Position = new Vector2(2, 1) }]);
        Assert.Single(moved.Moved);
        Assert.Empty(moved.Spawned);

        VisibilityDelta directionOnly = tracker.Update(
            [other with { Position = new Vector2(2, 1), Direction = Vector2.UnitX }]);
        Assert.Single(directionOnly.Moved);

        VisibilityDelta gone = tracker.Update([]);
        Assert.Equal([2], gone.Despawned);
        Assert.Equal(0, tracker.VisibleCount);
    }

    [Fact]
    public void Tracker_TinyJitter_IsNotResent()
    {
        var tracker = new VisibilityTracker();
        tracker.Update([State(2, Vector2.Zero)]);

        Assert.True(tracker.Update([State(2, new Vector2(0.001f, 0))]).IsEmpty);
    }

    // ---- WorldSimulation ---------------------------------------------

    [Fact]
    public async Task TwoPlayersOnSameMap_SeeEachOther_AndDespawnOnLeave()
    {
        var clock = new MutableClock(Start);
        var registry = new ActivePlayerRegistry();
        await using Player a = await Player.CreateAsync(1, "Alpha", "map", Vector2.Zero);
        await using Player b = await Player.CreateAsync(2, "Beta", "map", new Vector2(3, 0));
        a.Join(registry);
        b.Join(registry);
        WorldSimulation world = CreateWorld(registry, clock);

        world.Tick();

        WorldEntitiesSnapshotPacket seenByA = await a.ReadSnapshotAsync();
        WorldEntitiesSnapshotPacket seenByB = await b.ReadSnapshotAsync();
        RemotePlayerSpawnPacket betaSpawn = Assert.Single(seenByA.Spawned);
        Assert.Equal(2, betaSpawn.CharacterId);
        Assert.Equal("Beta", betaSpawn.Name);
        Assert.Equal(1, Assert.Single(seenByB.Spawned).CharacterId);

        b.Leave(registry);
        world.Tick();

        Assert.Equal([2], (await a.ReadSnapshotAsync()).Despawned);
    }

    [Fact]
    public async Task Movement_IsAdvancedByTick_AndSentAsMove()
    {
        var clock = new MutableClock(Start);
        var registry = new ActivePlayerRegistry();
        await using Player a = await Player.CreateAsync(1, "Alpha", "map", Vector2.Zero);
        await using Player b = await Player.CreateAsync(2, "Beta", "map", new Vector2(3, 0));
        a.Join(registry);
        b.Join(registry);
        WorldSimulation world = CreateWorld(registry, clock);
        world.Tick();
        await a.ReadSnapshotAsync();
        await b.ReadSnapshotAsync();

        Assert.True(b.Session.TrySetMovement(Vector2.UnitX, 1, clock.UtcNow,
            new PassThroughResolver()));
        clock.UtcNow = clock.UtcNow.AddMilliseconds(500);
        world.Tick();

        RemotePlayerMovePacket move = Assert.Single(
            (await a.ReadSnapshotAsync()).Moved);
        Assert.Equal(2, move.CharacterId);
        Assert.Equal(5f, move.X, 3); // 3 + 4 units/s * 0.5 s
        Assert.Equal(1f, move.DirectionX);
    }

    [Fact]
    public async Task BlockedPlayer_IsReportedAsStopped()
    {
        var clock = new MutableClock(Start);
        var registry = new ActivePlayerRegistry();
        await using Player a = await Player.CreateAsync(1, "Alpha", "map", Vector2.Zero);
        await using Player b = await Player.CreateAsync(2, "Beta", "map", new Vector2(3, 0));
        a.Join(registry);
        b.Join(registry);
        var world = new WorldSimulation(registry, new BlockingResolver(), clock,
            new WorldSimulationSettings(TimeSpan.FromMilliseconds(50), 1, 16f, 100));
        world.Tick();
        await a.ReadSnapshotAsync();
        await b.ReadSnapshotAsync();

        // Beta pushes against a wall: input direction set, position unchanged.
        b.Session.TrySetMovement(Vector2.UnitX, 1, clock.UtcNow, new BlockingResolver());
        clock.UtcNow = clock.UtcNow.AddMilliseconds(50);
        world.Tick();
        clock.UtcNow = clock.UtcNow.AddMilliseconds(50);
        world.Tick();

        // Clients must not extrapolate Beta through the wall.
        Assert.True(await a.TryReadNoSnapshotAsync());
    }

    [Fact]
    public async Task OtherMap_And_NotYetInWorld_AreInvisible()
    {
        var clock = new MutableClock(Start);
        var registry = new ActivePlayerRegistry();
        await using Player a = await Player.CreateAsync(1, "Alpha", "map", Vector2.Zero);
        await using Player farMap = await Player.CreateAsync(2, "Far", "other", Vector2.Zero);
        await using Player loading = await Player.CreateAsync(3, "Loading", "map", Vector2.One);
        a.Join(registry);
        farMap.Join(registry);
        loading.Join(registry, enterWorld: false);
        WorldSimulation world = CreateWorld(registry, clock);

        WorldTickResult result = world.Tick();

        Assert.Equal(2, result.PlayersInWorld);
        Assert.Equal(0, result.EntitySnapshotsQueued);
    }

    [Fact]
    public async Task CrowdedArea_IsCappedToNearestPlayers()
    {
        var clock = new MutableClock(Start);
        var registry = new ActivePlayerRegistry();
        await using Player observer = await Player.CreateAsync(1, "Obs", "map", Vector2.Zero);
        observer.Join(registry);
        var others = new List<Player>();
        try
        {
            for (int index = 0; index < 5; index++)
            {
                Player other = await Player.CreateAsync(10 + index, $"P{index}",
                    "map", new Vector2(index + 1, 0));
                other.Join(registry);
                others.Add(other);
            }

            WorldSimulation world = CreateWorld(registry, clock, maximumVisible: 2);
            world.Tick();

            WorldEntitiesSnapshotPacket seen = await observer.ReadSnapshotAsync();
            Assert.Equal([10, 11], seen.Spawned.Select(value => value.CharacterId)
                .Order().ToArray());
        }
        finally
        {
            foreach (Player other in others)
                await other.DisposeAsync();
        }
    }

    private static WorldSimulation CreateWorld(ActivePlayerRegistry registry,
        IServerClock clock, int maximumVisible = 100) =>
        new(registry, new PassThroughResolver(), clock,
            new WorldSimulationSettings(TimeSpan.FromMilliseconds(50),
                EntitySnapshotEveryTicks: 1, AoiCellSize: 16f,
                MaximumVisiblePlayers: maximumVisible));

    private static WorldPlayerState State(int id, Vector2 position) =>
        new(id, $"P{id}", 1, "warrior", "map", position, Vector2.Zero);

    private sealed class Player : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;

        private Player(TcpClient client, ClientConnection connection,
            PlayerSession session)
        {
            _client = client;
            _stream = client.GetStream();
            Connection = connection;
            Session = session;
        }

        public ClientConnection Connection { get; }
        public PlayerSession Session { get; }

        public static async Task<Player> CreateAsync(int characterId, string name,
            string map, Vector2 position)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var client = new TcpClient();
            Task connect = client.ConnectAsync(IPAddress.Loopback, port);
            TcpClient accepted = await listener.AcceptTcpClientAsync();
            await connect;
            listener.Stop();

            var leases = new InMemoryActiveAccountLeaseStore(
                TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), 10);
            var connection = new ClientConnection(accepted,
                new PacketDispatcher([], leases, new SystemServerClock()),
                maximumPacketSize: 64 * 1024);
            var session = new PlayerSession(
                new PlayerSessionProfile(characterId, name, 1, 0, "warrior",
                    "male", map, "spawn", []),
                100, 100, 4f, position, 10, TimeSpan.FromSeconds(1), Start);
            return new Player(client, connection, session);
        }

        public void Join(ActivePlayerRegistry registry, bool enterWorld = true)
        {
            Assert.True(Connection.TryAttachPlayerSession(Session));
            Assert.True(registry.TryClaim(Session.CharacterId, Connection));
            if (enterWorld)
                Session.MarkEnteredWorld();
        }

        public void Leave(ActivePlayerRegistry registry) =>
            registry.Release(Session.CharacterId, Connection);

        public async Task<WorldEntitiesSnapshotPacket> ReadSnapshotAsync()
        {
            using var timeout = new CancellationTokenSource(Deadline);
            var header = new byte[4];
            await _stream.ReadExactlyAsync(header, timeout.Token);
            var body = new byte[BitConverter.ToInt32(header)];
            await _stream.ReadExactlyAsync(body, timeout.Token);
            PacketEnvelope envelope = JsonSerializer.Deserialize<PacketEnvelope>(
                Encoding.UTF8.GetString(body))!;
            Assert.Equal(PacketType.WorldEntitiesSnapshot, envelope.Type);
            return JsonSerializer.Deserialize<WorldEntitiesSnapshotPacket>(
                envelope.Payload)!;
        }

        /// <summary>True when nothing arrives within a short window.</summary>
        public async Task<bool> TryReadNoSnapshotAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            try
            {
                int read = await _stream.ReadAsync(new byte[1], timeout.Token);
                return read == 0;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await Connection.DisposeAsync();
        }
    }

    private sealed class BlockingResolver : IWorldMovementResolver
    {
        public Vector2 Resolve(string mapDefinitionId, Vector2 start,
            Vector2 desiredEnd) => start;
    }

    private sealed class MutableClock(DateTime utcNow) : IServerClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class PassThroughResolver : IWorldMovementResolver
    {
        public Vector2 Resolve(string mapDefinitionId, Vector2 start,
            Vector2 desiredEnd) => desiredEnd;
    }
}
