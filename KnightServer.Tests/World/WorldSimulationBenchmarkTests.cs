using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Players;
using KnightOnline.Server.Time;
using KnightOnline.Server.World;
using Xunit.Abstractions;

namespace KnightServer.Tests.World;

/// <summary>
/// Opt-in benchmark of the world tick with N players on one map (worst case
/// for AOI: everyone sees everyone), real loopback sockets and draining
/// clients. Runs only when KNIGHTONLINE_RUN_BENCHMARKS=1 so CI stays fast:
///
///   set KNIGHTONLINE_RUN_BENCHMARKS=1
///   dotnet test KnightServer.Tests -c Release --filter Category=Benchmark --logger "console;verbosity=detailed"
/// </summary>
[Trait("Category", "Benchmark")]
public sealed class WorldSimulationBenchmarkTests(ITestOutputHelper output)
{
    private const int Ticks = 200; // 10 s of simulated time at 20 Hz

    /// <param name="directionChangeEveryTicks">
    /// Average ticks between a player's direction changes: 2 = every input
    /// (worst case), 40 = about every 2 s with 30% of changes being a stop.
    /// </param>
    [Theory]
    [InlineData(50, 2)]
    [InlineData(200, 2)]
    [InlineData(500, 2)]
    [InlineData(500, 40)]
    public async Task TickCost_WithPlayersOnOneMap(int players, int directionChangeEveryTicks)
    {
        if (Environment.GetEnvironmentVariable("KNIGHTONLINE_RUN_BENCHMARKS") != "1")
            return;

        var clock = new SteppingClock(new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc));
        var registry = new ActivePlayerRegistry();
        var resolver = new BoundedResolver(-10f, 10f, -6f, 6f);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var leases = new InMemoryActiveAccountLeaseStore(
            TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), players + 1);
        var dispatcher = new PacketDispatcher([], leases, new SystemServerClock());

        var clients = new List<TcpClient>();
        var connections = new List<ClientConnection>();
        var sessions = new List<PlayerSession>();
        long bytesReceived = 0;
        using var stopDrain = new CancellationTokenSource();
        var drains = new List<Task>();
        var random = new Random(7);
        try
        {
            for (int index = 0; index < players; index++)
            {
                var client = new TcpClient();
                Task connect = client.ConnectAsync(IPAddress.Loopback, port);
                TcpClient accepted = await listener.AcceptTcpClientAsync();
                await connect;
                clients.Add(client);
                drains.Add(DrainAsync(client, stopDrain.Token, count =>
                    Interlocked.Add(ref bytesReceived, count)));

                var connection = new ClientConnection(accepted, dispatcher, 64 * 1024);
                var session = new PlayerSession(
                    new PlayerSessionProfile(index + 1, $"Bench{index}", 1, 0, "warrior",
                        "male", "map", "spawn", []),
                    100, 100, 4f,
                    new Vector2(random.NextSingle() * 20 - 10, random.NextSingle() * 12 - 6),
                    10, TimeSpan.FromMilliseconds(250), clock.UtcNow);
                connection.TryAttachPlayerSession(session);
                registry.TryClaim(session.CharacterId, connection);
                session.MarkEnteredWorld();
                connections.Add(connection);
                sessions.Add(session);
            }

            var world = new WorldSimulation(registry, resolver, clock,
                new WorldSimulationSettings(TimeSpan.FromMilliseconds(50),
                    EntitySnapshotEveryTicks: 2, AoiCellSize: 16f,
                    MaximumVisiblePlayers: 100));

            var tickMs = new List<double>();
            double selectMs = 0, trackMs = 0, buildMs = 0, enqueueMs = 0;
            int snapshotTicks = 0;
            var snapshotTickMs = new List<double>();
            long sequence = 0;
            for (int tick = 0; tick < Ticks; tick++)
            {
                // ~10 Hz input per player, like the real client.
                if (tick % 2 == 0)
                {
                    sequence++;
                    double changeChance = 2.0 / directionChangeEveryTicks;
                    foreach (PlayerSession session in sessions)
                    {
                        if (tick > 0 && random.NextDouble() >= changeChance)
                            continue;
                        double angle = random.NextDouble() * Math.PI * 2;
                        Vector2 direction = directionChangeEveryTicks > 2 &&
                                            random.NextDouble() < 0.3
                            ? Vector2.Zero
                            : new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                        session.TrySetMovement(direction, sequence, clock.UtcNow, resolver);
                    }
                }

                clock.Advance(TimeSpan.FromMilliseconds(50));
                WorldTickResult result = world.Tick();
                double elapsed = result.Elapsed.TotalMilliseconds;
                tickMs.Add(elapsed);
                if (result.EntitySnapshotsQueued > 0)
                {
                    snapshotTickMs.Add(elapsed);
                    snapshotTicks++;
                    selectMs += result.Phases.SelectVisible.TotalMilliseconds;
                    trackMs += result.Phases.TrackDelta.TotalMilliseconds;
                    buildMs += result.Phases.BuildJson.TotalMilliseconds;
                    enqueueMs += result.Phases.Enqueue.TotalMilliseconds;
                }
                // Give the writer tasks and draining clients real time, like
                // the 50 ms PeriodicTimer would.
                await Task.Delay(5);
            }

            await Task.Delay(500);
            int closed = connections.Count(connection => connection.IsClosed);
            double simulatedSeconds = Ticks * 0.05;
            output.WriteLine(
                $"players={players} change/{directionChangeEveryTicks}t ticks={Ticks} " +
                $"tick p50={Percentile(tickMs, 0.5):0.00}ms p95={Percentile(tickMs, 0.95):0.00}ms " +
                $"max={tickMs.Max():0.00}ms | snapshot ticks p95={Percentile(snapshotTickMs, 0.95):0.00}ms " +
                $"| sent {bytesReceived / 1024.0 / simulatedSeconds:0.0} KiB/s total, " +
                $"{bytesReceived / 1024.0 / simulatedSeconds / players:0.00} KiB/s per player " +
                $"| slow consumers closed={closed} | avg per snapshot tick: " +
                $"select {selectMs / Math.Max(1, snapshotTicks):0.0} track {trackMs / Math.Max(1, snapshotTicks):0.0} " +
                $"build {buildMs / Math.Max(1, snapshotTicks):0.0} enqueue {enqueueMs / Math.Max(1, snapshotTicks):0.0} ms");

            Assert.Equal(0, closed);
        }
        finally
        {
            stopDrain.Cancel();
            listener.Stop();
            foreach (ClientConnection connection in connections)
                await connection.DisposeAsync();
            foreach (TcpClient client in clients)
                client.Dispose();
            await Task.WhenAll(drains).WaitAsync(TimeSpan.FromSeconds(5))
                .ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    private static async Task DrainAsync(TcpClient client, CancellationToken token,
        Action<int> received)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            NetworkStream stream = client.GetStream();
            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, token);
                if (read == 0)
                    return;
                received(read);
            }
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or IOException or ObjectDisposedException)
        {
        }
    }

    private static double Percentile(List<double> values, double percentile)
    {
        if (values.Count == 0)
            return 0;
        double[] sorted = values.Order().ToArray();
        return sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];
    }

    private sealed class SteppingClock(DateTime start) : IServerClock
    {
        public DateTime UtcNow { get; private set; } = start;
        public void Advance(TimeSpan delta) => UtcNow += delta;
    }

    /// <summary>Keeps players inside the map like the real resolver.</summary>
    private sealed class BoundedResolver(float minX, float maxX, float minY, float maxY)
        : IWorldMovementResolver
    {
        public Vector2 Resolve(string mapDefinitionId, Vector2 start, Vector2 desiredEnd) =>
            new(Math.Clamp(desiredEnd.X, minX, maxX), Math.Clamp(desiredEnd.Y, minY, maxY));
    }
}
