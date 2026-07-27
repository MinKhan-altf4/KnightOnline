using System.Net;
using System.Net.Sockets;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Combat;
using KnightOnline.Server.Characters;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Networking.Handlers;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Players;
using KnightOnline.Server.Time;
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
        IServerClock clock = new SystemServerClock();
        ICharacterCreationCatalog characterCatalog =
            CreateCharacterCatalog(options.Characters);
        var characterNamePolicy = new CharacterNamePolicy();

        var characterRepository = new CharacterRepository(
            databaseOptions,
            options.Characters,
            characterCatalog,
            characterNamePolicy,
            clock);
        await characterRepository.EnsureAccountExistsAsync(
            options.Characters.DevelopmentAccountKey);

        MonsterService monsterService = CreateMonsterWorld(
            options.MonsterDefinitions,
            options.MonsterSpawns);
        var connections = new ConnectionRegistry();
        var activePlayers = new ActivePlayerRegistry();
        IActiveAccountLeaseStore accountSessions =
            new InMemoryActiveAccountLeaseStore();
        var authentication = new AccountAuthenticationService(
            databaseOptions,
            new AuthTokenProtector(),
            new PasswordHasher(),
            TimeSpan.FromDays(
                options.Authentication.RefreshTokenLifetimeDays),
            clock);
        IRegistrationTransactionStore registrationTransactions =
            new InMemoryRegistrationTransactionStore();
        var registration = new RegistrationFlowService(
            registrationTransactions,
            new DevelopmentRegistrationPortal(
                options.Registration.PortalBaseUrl),
            new ExistingGuestRegistrationConverter(authentication),
            new AuthTokenProtector(),
            TimeSpan.FromMinutes(
                options.Registration.TransactionLifetimeMinutes),
            clock);
        var authenticationRateLimiter = new AuthenticationRateLimiter(
            options.Authentication.MaximumAttemptsPerWindow,
            TimeSpan.FromSeconds(
                options.Authentication.AttemptWindowSeconds));
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
            new ConnectPacketHandler(
                accountIdentities,
                options.Authentication.DevelopmentBypassEnabled),
            new CreateGuestPacketHandler(
                authentication,
                accountSessions,
                authenticationRateLimiter,
                clock),
            new ResumeAccountPacketHandler(
                authentication,
                accountSessions,
                authenticationRateLimiter,
                clock),
            new LoginPacketHandler(
                authentication,
                accountSessions,
                authenticationRateLimiter,
                clock),
            new LeaveAccountSessionPacketHandler(accountSessions),
            new BeginRegistrationPacketHandler(registration),
            new CompleteDevelopmentRegistrationPacketHandler(
                registration,
                options.Registration.DevelopmentCompletionEnabled),
            new GetCharacterCreationCatalogPacketHandler(
                characterCatalog,
                options.Characters),
            new CheckCharacterNamePacketHandler(
                characterRepository,
                characterNamePolicy,
                options.Characters),
            new CreateCharacterPacketHandler(characterRepository),
            new ListCharactersPacketHandler(characterRepository),
            new ListMonstersPacketHandler(monsterService),
            new SelectCharacterPacketHandler(
                characterRepository,
                activePlayers,
                accountSessions,
                options.Characters,
                options.Combat,
                options.World,
                clock),
            new PlayerMoveInputPacketHandler(clock),
            new AttackMonsterPacketHandler(
                combatService,
                monsterService,
                connections,
                clock),
        ]);

        ObserveBackgroundTask(
            RunMonsterRespawnLoopAsync(
                monsterService,
                connections,
                clock,
                TimeSpan.FromMilliseconds(
                    options.World.TickMilliseconds)),
            "monster-respawn-loop");

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
            ObserveBackgroundTask(
                RunConnectionAsync(
                    connection,
                    connections,
                    activePlayers,
                    accountSessions),
                "client-connection");
        }
    }

    private static ICharacterCreationCatalog CreateCharacterCatalog(
        CharacterOptions options) =>
        new ConfiguredCharacterCreationCatalog(
            new GetCharacterCreationCatalogResponsePacket(
                options.CatalogVersion,
                options.ServerId,
                options.Classes.Select(value =>
                    new CharacterClassDefinitionPacket(
                        value.DefinitionId,
                        value.DisplayName,
                        value.Description,
                        value.AllowedBodyTypeIds,
                        value.PreviewAssetAddress)).ToArray(),
                options.BodyTypes.Select(value =>
                    new BodyTypeDefinitionPacket(
                        value.DefinitionId,
                        value.DisplayName)).ToArray(),
                options.AppearanceOptions.Select(value =>
                    new AppearanceDefinitionPacket(
                        value.DefinitionId,
                        value.SlotDefinitionId,
                        value.DisplayName,
                        value.AllowedBodyTypeIds,
                        value.AllowedClassDefinitionIds,
                        value.AssetAddress,
                        value.IsStarterOption)).ToArray()),
            options.RequiredStarterAppearanceSlotIds);

    private static async Task RunConnectionAsync(
        ClientConnection connection,
        ConnectionRegistry connections,
        ActivePlayerRegistry activePlayers,
        IActiveAccountLeaseStore accountSessions)
    {
        try
        {
            await connection.RunAsync();
        }
        finally
        {
            if (connection.AccountKey is { } accountKey)
            {
                await accountSessions.ReleaseAsync(
                    accountKey,
                    connection.ConnectionId);
            }
            activePlayers.Release(connection);
            connections.Remove(connection);
        }
    }

    private static async Task RunMonsterRespawnLoopAsync(
        MonsterService monsterService,
        ConnectionRegistry connections,
        IServerClock clock,
        TimeSpan tickInterval)
    {
        using var timer = new PeriodicTimer(tickInterval);

        while (await timer.WaitForNextTickAsync())
        {
            IReadOnlyList<int> respawnedIds =
                monsterService.RespawnReadyMonsters(clock.UtcNow);

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

    private static void ObserveBackgroundTask(Task task, string operation)
    {
        _ = task.ContinueWith(
            completed =>
            {
                Exception exception =
                    completed.Exception?.GetBaseException()
                    ?? new InvalidOperationException(
                        "Background task failed without an exception.");
                Console.WriteLine(
                    $"[Server][Error] Background operation '{operation}' " +
                    $"failed: {exception}");
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

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
