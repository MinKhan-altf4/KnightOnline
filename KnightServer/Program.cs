using System.Net;
using System.Net.Sockets;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Combat;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Networking.Handlers;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Players;
using Microsoft.EntityFrameworkCore;

namespace KnightOnline.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        string settingsPath = args.FirstOrDefault()
            ?? Environment.GetEnvironmentVariable("KNIGHTONLINE_SETTINGS")
            ?? Path.Combine(AppContext.BaseDirectory, "serverSettings.json");
        ServerOptions options = ServerOptions.Load(settingsPath);

        DbContextOptions<KnightDbContext> databaseOptions =
            await ConfigureDatabaseAsync();

        var characterRepository = new CharacterRepository(
            databaseOptions,
            options.Characters.DevelopmentAccountKey,
            options.Characters.MaximumPerAccount,
            options.Characters.InitialLevel);
        await characterRepository.EnsureAccountExistsAsync();

        MonsterService monsterService = CreateMonsterWorld(
            options.MonsterDefinitions,
            options.MonsterSpawns);
        var connections = new ConnectionRegistry();
        var activePlayers = new ActivePlayerRegistry();
        var accountSessions = new AccountSessionRegistry();
        IAccountIdentityProvider accountIdentities =
            new DevelopmentAccountIdentityProvider(
                options.Characters.DevelopmentAccountKey);
        ICombatStatsProvider combatStats =
            new ConfiguredCombatStatsProvider(options.Combat.BaseAttackDamage);
        IDamageCalculator damageCalculator = new DefaultDamageCalculator();
        var combatService = new MonsterCombatService(
            monsterService,
            combatStats,
            damageCalculator,
            options.Combat);

        var packetDispatcher = new PacketDispatcher(
        [
            new ConnectPacketHandler(accountIdentities, accountSessions),
            new CreateCharacterPacketHandler(characterRepository),
            new ListCharactersPacketHandler(characterRepository),
            new ListMonstersPacketHandler(monsterService),
            new SelectCharacterPacketHandler(
                characterRepository,
                activePlayers,
                options.Characters,
                options.Combat,
                options.World),
            new PlayerMoveInputPacketHandler(),
            new AttackMonsterPacketHandler(
                combatService,
                monsterService,
                connections),
        ]);

        _ = RunMonsterRespawnLoopAsync(
            monsterService,
            connections,
            TimeSpan.FromMilliseconds(options.World.TickMilliseconds));

        var listener = new TcpListener(IPAddress.Any, options.Network.Port);
        listener.Start();
        Console.WriteLine($"[Server] Listening on port {options.Network.Port}.");
        Console.WriteLine(
            $"[Monster] Loaded {monsterService.GetSnapshots().Count} monster instance(s).");

        while (true)
        {
            TcpClient tcpClient = await listener.AcceptTcpClientAsync();
            var connection = new ClientConnection(
                tcpClient,
                packetDispatcher,
                options.Network.MaximumPacketBytes);
            connections.Add(connection);
            _ = RunConnectionAsync(
                connection,
                connections,
                activePlayers,
                accountSessions);
        }
    }

    private static async Task RunConnectionAsync(
        ClientConnection connection,
        ConnectionRegistry connections,
        ActivePlayerRegistry activePlayers,
        AccountSessionRegistry accountSessions)
    {
        try
        {
            await connection.RunAsync();
        }
        finally
        {
            accountSessions.Release(connection);
            activePlayers.Release(connection);
            connections.Remove(connection);
        }
    }

    private static async Task RunMonsterRespawnLoopAsync(
        MonsterService monsterService,
        ConnectionRegistry connections,
        TimeSpan tickInterval)
    {
        using var timer = new PeriodicTimer(tickInterval);

        while (await timer.WaitForNextTickAsync())
        {
            IReadOnlyList<int> respawnedIds =
                monsterService.RespawnReadyMonsters(DateTime.UtcNow);

            foreach (int monsterId in respawnedIds)
            {
                MonsterSnapshot? snapshot =
                    monsterService.GetSnapshot(monsterId);

                if (snapshot == null)
                    continue;

                await connections.BroadcastAsync(
                    PacketType.MonsterRespawned,
                    new MonsterRespawnedPacket(ToPacket(snapshot)));
            }
        }
    }

    private static MonsterSnapshotPacket ToPacket(MonsterSnapshot snapshot) =>
        new(
            snapshot.MonsterId,
            snapshot.DefinitionId,
            snapshot.Name,
            snapshot.Level,
            snapshot.CurrentHealth,
            snapshot.MaximumHealth,
            snapshot.IsAlive,
            snapshot.SpawnPosition.X,
            snapshot.SpawnPosition.Y);

    private static async Task<DbContextOptions<KnightDbContext>>
        ConfigureDatabaseAsync()
    {
        var configuration = DatabaseConfiguration.Build();
        string connectionString =
            DatabaseConfiguration.GetRequiredConnectionString(configuration);
        var databaseOptions = new DbContextOptionsBuilder<KnightDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new KnightDbContext(databaseOptions);
        await db.Database.MigrateAsync();

        return databaseOptions;
    }

    private static MonsterService CreateMonsterWorld(
        IReadOnlyList<MonsterDefinitionOptions> definitionOptions,
        IReadOnlyList<MonsterSpawnOptions> spawns)
    {
        var monsterService = new MonsterService();
        Dictionary<int, MonsterDefinition> definitions = definitionOptions
            .ToDictionary(
                definition => definition.DefinitionId,
                definition => new MonsterDefinition(
                    definition.DefinitionId,
                    definition.Name,
                    definition.Level,
                    definition.MaximumHealth,
                    TimeSpan.FromSeconds(definition.RespawnSeconds)));

        foreach (MonsterSpawnOptions spawn in spawns)
        {
            monsterService.Spawn(
                definitions[spawn.DefinitionId],
                new WorldPosition(spawn.PositionX, spawn.PositionY));
        }

        return monsterService;
    }
}
