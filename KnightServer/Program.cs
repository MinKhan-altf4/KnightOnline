using System.Net;
using System.Net.Sockets;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Combat;
using KnightOnline.Server.Characters;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Hosting;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Networking.Handlers;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Players;
using KnightOnline.Server.Progression;
using KnightOnline.Server.Time;
using KnightOnline.Server.Tutorials;
using KnightOnline.Server.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KnightOnline.Server;

public static class Program
{
    private const string ResetDevelopmentAccountsArgument =
        "--reset-development-accounts";
    private const string ResetConfirmationArgument =
        "--confirm-reset=DELETE-ALL-DEVELOPMENT-ACCOUNTS";

    public static async Task Main(string[] args)
    {
        string settingsPath = args.FirstOrDefault(value =>
                !value.StartsWith("--", StringComparison.Ordinal))
            ?? Environment.GetEnvironmentVariable("KNIGHTONLINE_SETTINGS")
            ?? Path.Combine(AppContext.BaseDirectory, "serverSettings.json");
        ServerOptions options = ServerOptions.Load(settingsPath);

        DbContextOptions<KnightDbContext> databaseOptions =
            await ConfigureDatabaseAsync(options.Persistence);
        if (args.Contains(
                ResetDevelopmentAccountsArgument,
                StringComparer.OrdinalIgnoreCase))
        {
            await ResetDevelopmentAccountsAsync(
                args,
                options,
                databaseOptions);
            return;
        }

        using SingleInstanceGuard instanceGuard =
            SingleInstanceGuard.Acquire();
        IServerClock clock = new SystemServerClock();
        ICharacterCreationCatalog characterCatalog =
            CreateCharacterCatalog(options.Characters);
        IMapCatalog mapCatalog =
            new ConfiguredMapCatalog(options.MapDefinitions);
        var characterNamePolicy = new CharacterNamePolicy();

        var characterRepository = new CharacterRepository(
            databaseOptions,
            options.Characters,
            characterCatalog,
            characterNamePolicy,
            clock,
            mapCatalog);
        await characterRepository.ClearSessionFencesAfterProcessRestartAsync();
        var sessionPersistence = new SessionPersistenceGate(
            options.Persistence.MaximumConcurrentSessionWrites,
            options.Persistence.SessionWriteAttempts);
        await characterRepository.EnsureAccountExistsAsync(
            options.Characters.DevelopmentAccountKey);

        MonsterService monsterService = CreateMonsterWorld(
            options.MonsterDefinitions,
            options.MonsterSpawns);
        IWorldMovementResolver movementResolver =
            new MonsterCollisionMovementResolver(
                monsterService,
                options.World,
                mapCatalog);
        var respawnDisplacementResolver =
            new MonsterRespawnDisplacementResolver(
                monsterService,
                options.World);
        var connections = new ConnectionRegistry(
            options.Capacity.MaximumTransportConnections);
        var activePlayers = new ActivePlayerRegistry();
        IExperienceCurve experienceCurve =
            new ConfiguredExperienceCurve(options.Progression);
        var characterStatsPipeline =
            new CharacterStatsPipeline(options.Characters);
        var progressionService = new CharacterProgressionService(
            databaseOptions,
            experienceCurve,
            clock);
        TutorialDefinitionOptions starterTutorial = options.TutorialDefinitions
            .Single(value => string.Equals(value.DefinitionId,
                options.Characters.StartingTutorialDefinitionId,
                StringComparison.OrdinalIgnoreCase));
        var starterTutorialService = new StarterTutorialService(
            databaseOptions, starterTutorial, experienceCurve, clock);
        IActiveAccountLeaseStore accountSessions =
            new InMemoryActiveAccountLeaseStore(
                TimeSpan.FromSeconds(
                    options.Authentication.SessionLeaseTtlSeconds),
                TimeSpan.FromSeconds(
                    options.Authentication.DisconnectGraceSeconds),
                options.Capacity.MaximumActiveAccounts);
        IConfiguration applicationConfiguration = DatabaseConfiguration.Build();
        string activeReceiptKeyId = ReadAuthenticationReceiptActiveKeyId(
                applicationConfiguration)
            ?? throw new InvalidOperationException(
                "Set Authentication:ActiveReceiptKeyId in User Secrets or " +
                "KNIGHTONLINE_Authentication__ActiveReceiptKeyId.");
        Dictionary<string, string> receiptKeys =
            ReadAuthenticationReceiptKeys(applicationConfiguration);
        var authentication = new AccountAuthenticationService(
            databaseOptions,
            new AuthTokenProtector(activeReceiptKeyId, receiptKeys),
            new PasswordHasher(),
            TimeSpan.FromDays(
                options.Authentication.RefreshTokenLifetimeDays),
            TimeSpan.FromMinutes(
                options.Authentication.AuthenticationReceiptLifetimeMinutes),
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
        var accountLoginRateLimiter = new AuthenticationRateLimiter(
            options.Authentication.MaximumLoginAttemptsPerAccount,
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
            movementResolver,
            options.Combat);

        var packetDispatcher = new PacketDispatcher(
        [
            new ConnectPacketHandler(
                accountIdentities,
                options.Authentication.DevelopmentBypassEnabled,
                options.Network.MinimumProtocolVersion,
                options.Network.MaximumProtocolVersion),
            new CreateGuestPacketHandler(
                authentication,
                accountSessions,
                authenticationRateLimiter,
                clock,
                options.Authentication),
            new ResumeAccountPacketHandler(
                authentication,
                accountSessions,
                authenticationRateLimiter,
                clock,
                options.Authentication),
            new LoginPacketHandler(
                authentication,
                accountSessions,
                authenticationRateLimiter,
                clock,
                options.Authentication,
                accountLoginRateLimiter),
            new LeaveAccountSessionPacketHandler(
                accountSessions,
                activePlayers,
                characterRepository,
                clock,
                movementResolver),
            new AccountSessionHeartbeatPacketHandler(
                accountSessions,
                clock),
            new BeginRegistrationPacketHandler(registration),
            new CompleteDevelopmentRegistrationPacketHandler(
                registration,
                options.Registration.DevelopmentCompletionEnabled,
                accountSessions,
                clock,
                options.Authentication),
            new GetCharacterCreationCatalogPacketHandler(
                characterCatalog,
                options.Characters),
            new CheckCharacterNamePacketHandler(
                characterRepository,
                characterNamePolicy,
                options.Characters),
            new CreateCharacterPacketHandler(characterRepository),
            new ListCharactersPacketHandler(
                characterRepository,
                options.Characters),
            new ListMonstersPacketHandler(monsterService),
            new ListNpcsPacketHandler(options.NpcDefinitions),
            new InteractNpcPacketHandler(options.NpcDefinitions,
                starterTutorial, starterTutorialService,
                experienceCurve, characterStatsPipeline, clock,
                movementResolver),
            new ListPortalsPacketHandler(options.PortalDefinitions,
                options.MapDefinitions),
            new UsePortalPacketHandler(options.PortalDefinitions,
                starterTutorialService, characterRepository, mapCatalog, clock,
                movementResolver),
            new SelectCharacterPacketHandler(
                characterRepository,
                activePlayers,
                accountSessions,
                options.Characters,
                options.World,
                characterStatsPipeline,
                experienceCurve,
                clock),
            new EnterWorldPacketHandler(clock, movementResolver,
                starterTutorialService, starterTutorial),
            new PlayerMoveInputPacketHandler(clock, movementResolver),
            new AttackMonsterPacketHandler(
                combatService,
                monsterService,
                connections,
                progressionService,
                characterStatsPipeline,
                options.Progression,
                options.Guest,
                starterTutorialService,
                starterTutorial,
                clock),
        ],
            accountSessions,
            clock,
            requireHandshake: true);

        var worldSimulation = new WorldSimulation(
            activePlayers,
            movementResolver,
            clock,
            new WorldSimulationSettings(
                TimeSpan.FromMilliseconds(options.World.SimulationTickMilliseconds),
                options.World.EntitySnapshotEveryTicks,
                options.World.AoiCellSize,
                options.World.MaximumVisiblePlayers,
                new DeadReckoningSettings(
                    options.World.DeadReckoningErrorThreshold,
                    TimeSpan.FromMilliseconds(
                        options.World.DeadReckoningRefreshMilliseconds))));
        ObserveBackgroundTask(worldSimulation.RunAsync(), "world-simulation");
        ObserveBackgroundTask(
            RunMonsterRespawnLoopAsync(
                monsterService,
                connections,
                activePlayers,
                respawnDisplacementResolver,
                clock,
                TimeSpan.FromMilliseconds(
                    options.World.TickMilliseconds)),
            "monster-respawn-loop");
        ObserveBackgroundTask(
            RunCharacterSnapshotLoopAsync(
                activePlayers,
                accountSessions,
                characterRepository,
                sessionPersistence,
                clock,
                movementResolver,
                TimeSpan.FromSeconds(5)),
            "character-snapshot-loop");
        ObserveBackgroundTask(
            RunAuthenticationReceiptCleanupLoopAsync(
                authentication, TimeSpan.FromMinutes(15)),
            "authentication-receipt-cleanup-loop");

        var listener = new TcpListener(
            IPAddress.Parse(options.Network.BindAddress),
            options.Network.Port);
        var runtime = new ServerRuntime(characterRepository, movementResolver,
            sessionPersistence);
        listener.Start();
        Console.WriteLine($"[Server] Listening on port {options.Network.Port}.");
        Console.WriteLine(
            $"[Monster] Loaded {monsterService.GetSnapshots().Count} monster instance(s).");

        var anonymousTimeout = TimeSpan.FromSeconds(
            options.Network.AnonymousConnectionTimeoutSeconds);
        while (true)
        {
            // B2: nothing a single client does may escape this loop, otherwise
            // the whole server stops accepting connections.
            TcpClient tcpClient;
            try
            {
                tcpClient = await listener.AcceptTcpClientAsync();
            }
            catch (SocketException exception)
            {
                Console.WriteLine(
                    $"[Network][Warning] Accept failed: {exception.SocketErrorCode}.");
                continue;
            }

            ClientConnection connection;
            try
            {
                connection = new ClientConnection(
                    tcpClient,
                    packetDispatcher,
                    options.Network.MaximumPacketBytes,
                    anonymousTimeout);
            }
            catch (Exception exception) when (
                exception is IOException or SocketException or
                    InvalidOperationException or ObjectDisposedException)
            {
                // The client reset before the stream could be opened.
                Console.WriteLine(
                    "[Network][Warning] Dropped connection during accept: " +
                    $"{exception.GetType().Name}.");
                tcpClient.Dispose();
                continue;
            }

            if (!connections.TryAdd(connection))
            {
                Console.WriteLine(
                    "[Capacity][Warning] Rejected transport connection: " +
                    "server transport capacity reached.");
                ObserveBackgroundTask(
                    RejectServerFullAsync(connection),
                    "reject-server-full");
                continue;
            }
            ObserveBackgroundTask(
                RunConnectionAsync(
                    connection,
                    connections,
                    activePlayers,
                    accountSessions,
                    clock,
                    runtime),
                "client-connection");
        }
    }

    /// <summary>
    /// Tells a client the server is full, off the accept loop and with a short
    /// deadline so a slow or resetting client cannot block or crash accept.
    /// </summary>
    private static async Task RejectServerFullAsync(ClientConnection connection)
    {
        connection.TryEnqueue(
            PacketType.ConnectResponse,
            new ConnectResponsePacket(
                ConnectResult.ServerFull,
                "Máy chủ hiện đã đầy. Vui lòng thử lại sau."));
        // Flush is bounded (2 s) inside CloseAfterFlushAsync.
        await connection.CloseAfterFlushAsync();
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
        IActiveAccountLeaseStore accountSessions,
        IServerClock clock,
        ServerRuntime runtime)
    {
        try
        {
            await connection.RunAsync();
        }
        finally
        {
            try
            {
                if (connection.PlayerSession is { } playerSession &&
                    connection.AccountKey is { } playerAccountKey)
                {
                    // B5: persist where the player is now, not where the last
                    // movement input left them.
                    playerSession.AdvancePosition(
                        clock.UtcNow, runtime.MovementResolver);
                    Guid generation = connection.AccountSessionGeneration;
                    // Bounded + retried: a mass disconnect must not exhaust
                    // database connections.
                    await runtime.SessionPersistence.RunAsync(
                        "disconnect snapshot save",
                        token => runtime.Characters.SaveSessionSnapshotAsync(
                            playerAccountKey,
                            playerSession.CharacterId,
                            generation,
                            playerSession.MapDefinitionId,
                            playerSession.SpawnPointId,
                            playerSession.Position.X,
                            playerSession.Position.Y,
                            token));
                    await runtime.SessionPersistence.RunAsync(
                        "disconnect character release",
                        token => runtime.Characters.ReleaseSessionAsync(
                            playerAccountKey,
                            playerSession.CharacterId,
                            generation,
                            token));
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"[Persistence][Error] Session snapshot cleanup failed for " +
                    $"connection {connection.ConnectionId}: {exception}");
            }
            if (connection.AccountKey is { } accountKey)
            {
                await accountSessions.BeginDisconnectGraceAsync(
                    accountKey,
                    connection.ConnectionId,
                    connection.AccountSessionGeneration,
                    clock.UtcNow);
            }
            activePlayers.Release(connection);
            connections.Remove(connection);
        }
    }

    private sealed record ServerRuntime(
        CharacterRepository Characters,
        IWorldMovementResolver MovementResolver,
        SessionPersistenceGate SessionPersistence);

    private static async Task RunMonsterRespawnLoopAsync(
        MonsterService monsterService,
        ConnectionRegistry connections,
        ActivePlayerRegistry activePlayers,
        MonsterRespawnDisplacementResolver displacementResolver,
        IServerClock clock,
        TimeSpan tickInterval)
    {
        using var timer = new PeriodicTimer(tickInterval);

        while (await timer.WaitForNextTickAsync())
        {
            IReadOnlyList<int> respawnedIds =
                monsterService.RespawnReadyMonsters(clock.UtcNow);

            if (respawnedIds.Count > 0)
            {
                foreach (ClientConnection connection in
                         activePlayers.GetConnectionsSnapshot())
                {
                    PlayerSession? session = connection.PlayerSession;
                    if (session == null)
                        continue;

                    bool displaced = session.TryResolveAuthoritativePosition(
                        position => displacementResolver.Resolve(
                            session.MapDefinitionId,
                            session.CharacterId,
                            position),
                        out _);
                    if (!displaced)
                        continue;

                    PlayerPositionState positionState =
                        session.CapturePositionSnapshot();
                    try
                    {
                        await connection.SendAsync(
                            PacketType.PlayerPositionSnapshot,
                            new PlayerPositionSnapshotPacket(
                                positionState.ServerSequence,
                                positionState.AcknowledgedClientSequence,
                                PlayerPositionSnapshotReason.RespawnDisplacement,
                                positionState.Position.X,
                                positionState.Position.Y,
                                clock.UtcNow));
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(
                            "[World][Warning] Respawn displacement snapshot " +
                            $"delivery failed: {exception.GetType().Name}: " +
                            exception.Message);
                    }
                }
            }

            foreach (int monsterId in respawnedIds)
            {
                MonsterSnapshot? snapshot =
                    monsterService.GetSnapshot(monsterId);

                if (snapshot == null)
                    continue;

                connections.BroadcastToMap(
                    snapshot.MapDefinitionId,
                    PacketType.MonsterRespawned,
                    new MonsterRespawnedPacket(ToPacket(snapshot)));
            }
        }
    }

    private static async Task RunCharacterSnapshotLoopAsync(
        ActivePlayerRegistry activePlayers,
        IActiveAccountLeaseStore accountSessions,
        CharacterRepository characters,
        SessionPersistenceGate sessionPersistence,
        IServerClock clock,
        IWorldMovementResolver movementResolver,
        TimeSpan tickInterval)
    {
        using var timer = new PeriodicTimer(tickInterval);
        while (await timer.WaitForNextTickAsync())
        {
            foreach (ClientConnection connection in
                     activePlayers.GetConnectionsSnapshot())
            {
                PlayerSession? session = connection.PlayerSession;
                string? accountKey = connection.AccountKey;
                if (session == null || accountKey == null ||
                    !await accountSessions.IsOwnerAsync(
                        accountKey,
                        connection.ConnectionId,
                        connection.AccountSessionGeneration,
                        clock.UtcNow))
                    continue;

                // The connection may have closed after the ownership check;
                // its own cleanup saves the final position.
                if (connection.IsClosed)
                    continue;

                try
                {
                    session.AdvancePosition(clock.UtcNow, movementResolver);
                    Guid generation = connection.AccountSessionGeneration;
                    await sessionPersistence.RunAsync(
                        "periodic snapshot save",
                        token => characters.SaveSessionSnapshotAsync(
                            accountKey,
                            session.CharacterId,
                            generation,
                            session.MapDefinitionId,
                            session.SpawnPointId,
                            session.Position.X,
                            session.Position.Y,
                            token));
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        "[Persistence][Warning] Character snapshot failed: " +
                        $"{exception.GetType().Name}: {exception.Message}");
                }
            }
        }
    }

    private static async Task RunAuthenticationReceiptCleanupLoopAsync(
        AccountAuthenticationService authentication,
        TimeSpan interval)
    {
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                await authentication.DeleteExpiredReceiptsAsync(
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "[Authentication][Warning] Receipt cleanup failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    private static string? ReadAuthenticationReceiptActiveKeyId(
        IConfiguration configuration) =>
        Environment.GetEnvironmentVariable(
            "KNIGHTONLINE_Authentication__ActiveReceiptKeyId") ??
        configuration["Authentication:ActiveReceiptKeyId"];

    private static Dictionary<string, string> ReadAuthenticationReceiptKeys(
        IConfiguration configuration)
    {
        const string environmentPrefix =
            "KNIGHTONLINE_Authentication__ReceiptKeys__";
        Dictionary<string, string> keys = configuration
            .GetSection("Authentication:ReceiptKeys")
            .GetChildren()
            .ToDictionary(value => value.Key,
                value => value.Value ?? string.Empty,
                StringComparer.Ordinal);

        foreach (System.Collections.DictionaryEntry entry in
                 Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string key ||
                !key.StartsWith(environmentPrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                entry.Value is not string value)
                continue;
            string keyId = key[environmentPrefix.Length..];
            if (!string.IsNullOrWhiteSpace(keyId))
                keys[keyId] = value;
        }

        return keys;
    }

    private static MonsterSnapshotPacket ToPacket(MonsterSnapshot snapshot) =>
        new(
            snapshot.MonsterId,
            snapshot.DefinitionId,
            snapshot.Name,
            snapshot.Level,
            snapshot.MapDefinitionId,
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
        ConfigureDatabaseAsync(PersistenceOptions persistence)
    {
        var configuration = DatabaseConfiguration.Build();
        string connectionString = DatabaseConfiguration.ApplyPoolLimit(
            DatabaseConfiguration.GetRequiredConnectionString(configuration),
            persistence.MaximumPoolSize);
        var databaseOptions = new DbContextOptionsBuilder<KnightDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new KnightDbContext(databaseOptions);
        await db.Database.MigrateAsync();

        return databaseOptions;
    }

    private static async Task ResetDevelopmentAccountsAsync(
        IReadOnlyCollection<string> args,
        ServerOptions options,
        DbContextOptions<KnightDbContext> databaseOptions)
    {
        if (!string.Equals(
                options.Environment,
                "Development",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Account reset is only available in Development.");
        }

        if (!args.Contains(
                ResetConfirmationArgument,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Account reset requires the exact confirmation argument: " +
                ResetConfirmationArgument);
        }

        var reset = new DevelopmentAccountDataReset(databaseOptions);
        DevelopmentAccountResetResult result = await reset.ExecuteAsync();
        Console.WriteLine(
            "[DevelopmentDataReset] Completed. " +
            $"accounts={result.AccountsBefore}->{result.AccountsAfter}, " +
            $"characters={result.CharactersBefore}->{result.CharactersAfter}, " +
            $"refreshSessions={result.RefreshSessionsBefore}->" +
            $"{result.RefreshSessionsAfter}, " +
            $"deletedAccountRoots={result.DeletedAccounts}.");
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
                    TimeSpan.FromSeconds(definition.RespawnSeconds),
                    definition.ExperienceReward));

        foreach (MonsterSpawnOptions spawn in spawns)
        {
            monsterService.Spawn(
                definitions[spawn.DefinitionId],
                spawn.MapDefinitionId,
                new WorldPosition(spawn.PositionX, spawn.PositionY));
        }

        return monsterService;
    }
}
