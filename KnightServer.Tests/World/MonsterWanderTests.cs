using System.Numerics;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Time;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Tests.World;

public sealed class MonsterWanderTests
{
    private const string Map = "wander-map";
    private static readonly DateTime Start = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly WorldPosition Spawn = new(2f, 1f);

    [Fact]
    public void WithoutPolicy_MonsterStaysOnSpawn()
    {
        var clock = new ManualClock(Start);
        var monsters = new MonsterService(clock);
        int id = Spawn1(monsters);

        for (int step = 0; step < 100; step++)
        {
            clock.UtcNow = Start.AddSeconds(step);
            Assert.Empty(monsters.AdvanceWander(clock.UtcNow));
        }

        Assert.Equal(Spawn, monsters.GetSnapshot(id)!.Position);
    }

    [Fact]
    public void FirstAdvance_OnlySchedulesRest()
    {
        var clock = new ManualClock(Start);
        MonsterService monsters = CreateWandering(clock, minimumIdle: 1, maximumIdle: 1);
        Spawn1(monsters);

        Assert.Empty(monsters.AdvanceWander(Start));
        Assert.Empty(monsters.AdvanceWander(Start.AddMilliseconds(500)));
        Assert.Single(monsters.AdvanceWander(Start.AddSeconds(1)));
    }

    [Fact]
    public void Legs_StayWithinRadiusOfSpawnAndInsideMap()
    {
        var clock = new ManualClock(Start);
        MonsterService monsters = CreateWandering(clock, radius: 3f, mapHalfSize: 4f);
        int id = Spawn1(monsters);

        int legs = 0;
        for (int step = 0; step < 4000; step++)
        {
            clock.UtcNow = Start.AddMilliseconds(step * 250);
            foreach (MonsterMovementLeg leg in monsters.AdvanceWander(clock.UtcNow))
            {
                legs++;
                Assert.Equal(id, leg.MonsterId);
                Assert.Equal(Map, leg.MapDefinitionId);
                Assert.True(Distance(leg.To, Spawn) <= 3f + 1e-4f);
                Assert.InRange(leg.To.X, -4f, 4f);
                Assert.InRange(leg.To.Y, -4f, 4f);
                Assert.True(Distance(leg.From, leg.To) >= MonsterWanderPolicy.MinimumLegDistance);
            }
            WorldPosition position = monsters.GetSnapshot(id)!.Position;
            Assert.True(Distance(position, Spawn) <= 3f + 1e-4f);
        }

        Assert.True(legs > 20, $"expected many legs, got {legs}");
    }

    [Fact]
    public void Position_MovesAlongLegAtConfiguredSpeed()
    {
        var clock = new ManualClock(Start);
        MonsterService monsters = CreateWandering(clock, speed: 2f, minimumIdle: 0, maximumIdle: 0);
        int id = Spawn1(monsters);
        monsters.AdvanceWander(Start);
        MonsterMovementLeg leg = monsters.AdvanceWander(Start).Single();

        float length = Distance(leg.From, leg.To);
        Assert.Equal(length / 2f, (float)leg.Duration.TotalSeconds, 3);

        clock.UtcNow = Start + leg.Duration / 2;
        MonsterSnapshot halfway = monsters.GetSnapshot(id)!;
        Assert.Equal((leg.From.X + leg.To.X) / 2f, halfway.Position.X, 3);
        Assert.Equal((leg.From.Y + leg.To.Y) / 2f, halfway.Position.Y, 3);
        Assert.Equal(leg.To, halfway.MoveTarget);
        Assert.InRange(halfway.MoveRemaining,
            leg.Duration / 2 - TimeSpan.FromMilliseconds(1),
            leg.Duration / 2 + TimeSpan.FromMilliseconds(1));

        clock.UtcNow = Start + leg.Duration + TimeSpan.FromSeconds(1);
        MonsterSnapshot arrived = monsters.GetSnapshot(id)!;
        Assert.Equal(leg.To, arrived.Position);
        Assert.Equal(TimeSpan.Zero, arrived.MoveRemaining);
    }

    [Fact]
    public void NoNewLeg_WhileCurrentLegIsWalking()
    {
        var clock = new ManualClock(Start);
        MonsterService monsters = CreateWandering(clock, speed: 0.5f, minimumIdle: 0, maximumIdle: 0);
        Spawn1(monsters);
        monsters.AdvanceWander(Start);
        MonsterMovementLeg leg = monsters.AdvanceWander(Start).Single();

        Assert.Empty(monsters.AdvanceWander(Start + leg.Duration / 2));
    }

    [Fact]
    public void KilledMonster_StopsWhereItFell_AndRespawnsOnSpawn()
    {
        var clock = new ManualClock(Start);
        MonsterService monsters = CreateWandering(clock, minimumIdle: 0, maximumIdle: 0);
        int id = Spawn1(monsters);
        monsters.AdvanceWander(Start);
        MonsterMovementLeg leg = monsters.AdvanceWander(Start).Single();

        DateTime killedAt = Start + leg.Duration / 2;
        clock.UtcNow = killedAt;
        WorldPosition fallen = monsters.GetSnapshot(id)!.Position;
        monsters.ApplyDamage(id, 1000, killedAt);

        clock.UtcNow = killedAt + TimeSpan.FromSeconds(5);
        Assert.Empty(monsters.AdvanceWander(clock.UtcNow));
        Assert.Equal(fallen, monsters.GetSnapshot(id)!.Position);

        Assert.Single(monsters.RespawnReadyMonsters(killedAt + TimeSpan.FromSeconds(11)));
        clock.UtcNow = killedAt + TimeSpan.FromSeconds(11);
        Assert.Equal(Spawn, monsters.GetSnapshot(id)!.Position);
    }

    [Fact]
    public void CollisionResolver_BlocksAtLivePosition()
    {
        var clock = new ManualClock(Start);
        MonsterService monsters = CreateWandering(clock, minimumIdle: 0, maximumIdle: 0);
        int id = Spawn1(monsters);
        monsters.AdvanceWander(Start);
        MonsterMovementLeg leg = monsters.AdvanceWander(Start).Single();
        clock.UtcNow = Start + leg.Duration;
        var resolver = new MonsterCollisionMovementResolver(
            monsters,
            new WorldOptions { PlayerCollisionRadius = 0.35f, MonsterCollisionRadius = 0.5f },
            CreateMaps(10f));

        // Walk straight at the monster's new position from 3 units away.
        var target = new Vector2(leg.To.X, leg.To.Y);
        Vector2 start = target - new Vector2(3f, 0f);
        Vector2 resolved = resolver.Resolve(Map, start, target);

        Assert.InRange(Vector2.Distance(resolved, target), 0.84f, 0.86f);
        Assert.Equal(leg.To, monsters.GetSnapshot(id)!.Position);
    }

    [Fact]
    public void Mapper_CarriesLiveLegToClients()
    {
        var clock = new ManualClock(Start);
        MonsterService monsters = CreateWandering(clock, minimumIdle: 0, maximumIdle: 0);
        int id = Spawn1(monsters);
        monsters.AdvanceWander(Start);
        MonsterMovementLeg leg = monsters.AdvanceWander(Start).Single();
        clock.UtcNow = Start + leg.Duration / 4;

        MonsterSnapshotPacket packet = MonsterPacketMapper.ToPacket(monsters.GetSnapshot(id)!);
        MonsterMovedPacket moved = MonsterPacketMapper.ToMovedPacket(leg);

        Assert.Equal(leg.To.X, packet.TargetX);
        Assert.Equal(leg.To.Y, packet.TargetY);
        Assert.InRange(packet.MoveRemainingMilliseconds,
            (int)(leg.Duration.TotalMilliseconds * 0.75) - 1,
            (int)(leg.Duration.TotalMilliseconds * 0.75) + 1);
        Assert.Equal(id, moved.MonsterId);
        Assert.Equal(leg.From.X, moved.FromX);
        Assert.Equal(leg.To.Y, moved.ToY);
        Assert.True(moved.DurationMilliseconds > 0);
    }

    [Theory]
    [InlineData(-1f, 1f, 0, 0)]
    [InlineData(2f, 0f, 0, 0)]
    [InlineData(2f, 1f, 3000, 1000)]
    public void Options_RejectInvalidWanderSettings(float radius, float speed,
        int minimumIdle, int maximumIdle)
    {
        string path = Path.Combine(Path.GetTempPath(), $"wander-{Guid.NewGuid():N}.json");
        try
        {
            string settings = File.ReadAllText(FindServerSettings());
            settings = settings
                .Replace("\"monsterWanderRadius\": 2.5", $"\"monsterWanderRadius\": {radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
                .Replace("\"monsterWanderSpeed\": 1.2", $"\"monsterWanderSpeed\": {speed.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
                .Replace("\"monsterWanderMinimumIdleMilliseconds\": 2000", $"\"monsterWanderMinimumIdleMilliseconds\": {minimumIdle}")
                .Replace("\"monsterWanderMaximumIdleMilliseconds\": 6000", $"\"monsterWanderMaximumIdleMilliseconds\": {maximumIdle}");
            File.WriteAllText(path, settings);

            Assert.Throws<InvalidDataException>(() => ServerOptions.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Options_ShippedSettingsLoadWithWanderEnabled()
    {
        ServerOptions options = ServerOptions.Load(FindServerSettings());

        Assert.True(options.World.MonsterWanderRadius > 0);
        Assert.Equal(PacketProtocol.Version, options.Network.MinimumProtocolVersion);
    }

    private static MonsterService CreateWandering(ManualClock clock, float radius = 2.5f,
        float speed = 1.2f, int minimumIdle = 2, int maximumIdle = 6, float mapHalfSize = 10f)
    {
        IMapCatalog maps = CreateMaps(mapHalfSize);
        return new MonsterService(
            clock,
            new MonsterWanderPolicy(radius, speed,
                TimeSpan.FromSeconds(minimumIdle), TimeSpan.FromSeconds(maximumIdle)),
            new Random(1234),
            (mapId, position) => maps.ClampPosition(mapId, position));
    }

    private static int Spawn1(MonsterService monsters) =>
        monsters.Spawn(
            new MonsterDefinition(1, "Wolf", 1, 50, TimeSpan.FromSeconds(10)),
            Map,
            Spawn);

    private static IMapCatalog CreateMaps(float halfSize) => new ConfiguredMapCatalog(
    [
        new MapDefinitionOptions
        {
            DefinitionId = Map, DisplayName = "Wander",
            MinimumX = -halfSize, MaximumX = halfSize,
            MinimumY = -halfSize, MaximumY = halfSize,
            SpawnPoints = [new MapSpawnPointOptions { SpawnPointId = "spawn" }],
        },
    ]);

    private static float Distance(WorldPosition a, WorldPosition b) =>
        Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));

    private static string FindServerSettings(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        // Walk up from this source file: the test output may live outside the repo.
        string? directory = Path.GetDirectoryName(sourceFile);
        while (directory != null)
        {
            string candidate = Path.Combine(directory, "KnightServer", "serverSettings.json");
            if (File.Exists(candidate))
                return candidate;
            directory = Path.GetDirectoryName(directory);
        }
        throw new FileNotFoundException("KnightServer/serverSettings.json not found.");
    }

    private sealed class ManualClock(DateTime utcNow) : IServerClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
