using System.Net;
using System.Net.Sockets;
using System.Numerics;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Combat;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Players;
using KnightOnline.Server.Time;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Tests.Combat;

public sealed class MonsterCombatMapTests
{
    private static readonly DateTime Now =
        new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Attack_RejectsMonsterOnAnotherMap()
    {
        var monsters = new MonsterService();
        int monsterId = monsters.Spawn(
            new MonsterDefinition(
                1,
                "Other Map Monster",
                1,
                50,
                TimeSpan.FromSeconds(10)),
            "map-b",
            new WorldPosition(0f, 0f));
        WorldOptions worldOptions = new()
        {
            PlayerCollisionRadius = 0.35f,
            MonsterCollisionRadius = 0.5f,
        };
        var movementResolver = new MonsterCollisionMovementResolver(
            monsters,
            worldOptions);
        var combat = new MonsterCombatService(
            monsters,
            new ConfiguredCombatStatsProvider(10),
            new DefaultDamageCalculator(),
            movementResolver,
            new CombatOptions
            {
                BaseAttackDamage = 10,
                AttackRange = 2f,
                AttackCooldownMilliseconds = 750,
            });
        await using ClientConnection connection =
            await CreateConnectionAsync();
        Assert.True(connection.TryAttachPlayerSession(CreatePlayerSession()));

        MonsterAttackResolution result = combat.Attack(
            connection,
            monsterId,
            Now);

        Assert.Equal(AttackResultStatus.WrongMap, result.Status);
        Assert.Equal(50, monsters.GetSnapshot(monsterId)!.CurrentHealth);
    }

    private static PlayerSession CreatePlayerSession() =>
        new(
            new PlayerSessionProfile(
                1,
                "Map Test",
                1,
                0,
                "warrior",
                "male",
                "map-a",
                "spawn-a",
                []),
            100,
            100,
            4f,
            Vector2.Zero,
            10,
            TimeSpan.FromMilliseconds(250),
            Now);

    private static async Task<ClientConnection> CreateConnectionAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new TcpClient();
        Task connect = client.ConnectAsync(IPAddress.Loopback, port);
        TcpClient accepted = await listener.AcceptTcpClientAsync();
        await connect;
        listener.Stop();
        client.Dispose();

        var clock = new FixedClock();
        var leases = new InMemoryActiveAccountLeaseStore(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            10);
        var dispatcher = new PacketDispatcher([], leases, clock);
        return new ClientConnection(
            accepted,
            dispatcher,
            maximumPacketSize: 1024);
    }

    private sealed class FixedClock : IServerClock
    {
        public DateTime UtcNow => Now;
    }
}
