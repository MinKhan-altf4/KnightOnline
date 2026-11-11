using System.Data.Common;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Characters;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Persistence.Entities;
using KnightOnline.Server.Progression;
using KnightOnline.Server.Time;
using KnightOnline.Server.Tutorials;
using KnightOnline.Server.World;
using KnightOnline.Server.Monsters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace KnightOnline.Server.Tests.Persistence;

public sealed class PostgreSqlPersistenceFactAttribute : FactAttribute
{
    public PostgreSqlPersistenceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    "KNIGHTONLINE_TEST_POSTGRES_CONNECTION")))
        {
            Skip = "Set KNIGHTONLINE_TEST_POSTGRES_CONNECTION to a dedicated " +
                   "PostgreSQL test database to run integration tests.";
        }
    }
}

public sealed class PostgreSqlPersistenceIntegrationTests
{
    [PostgreSqlPersistenceFact]
    public async Task SnapshotRetriesWhenTutorialRewardChangesCharacterVersion()
    {
        var interceptor = new TutorialRewardBeforeSnapshotInterceptor();
        await using TemporaryPostgreSqlDatabase testDatabase =
            await TemporaryPostgreSqlDatabase.CreateAsync(interceptor);
        DateTime now = DateTime.UtcNow;
        var clock = new TestClock(now);
        TutorialDefinitionOptions definition = CreateTutorialDefinition();
        var curve = new ConfiguredExperienceCurve(new ProgressionOptions());
        var tutorial = new StarterTutorialService(
            testDatabase.Options, definition, curve, clock);
        interceptor.RunTutorialRewardAsync = token =>
            tutorial.InteractWithQuestNpcAsync(
                Guid.NewGuid(), testDatabase.CharacterId, token);

        var repository = new CharacterRepository(
            testDatabase.Options,
            new CharacterOptions { ServerId = "integration-server" },
            new TestCharacterCreationCatalog(),
            new CharacterNamePolicy(),
            clock,
            new TestMapCatalog());

        bool saved = await repository.SaveSessionSnapshotAsync(
            testDatabase.AccountKey,
            testDatabase.CharacterId,
            testDatabase.Generation,
            "tutorial_map_02",
            "tutorial_spawn_02",
            17.5f,
            -3.25f,
            CancellationToken.None);

        Assert.True(interceptor.TutorialRewardRan);
        Assert.True(saved);
        await using var db = new KnightDbContext(testDatabase.Options);
        CharacterEntity character = await db.Characters.SingleAsync(
            value => value.Id == testDatabase.CharacterId);
        Assert.Equal("tutorial_map_02", character.CurrentMapDefinitionId);
        Assert.Equal("tutorial_spawn_02", character.CurrentSpawnPointId);
        Assert.Equal(17.5f, character.PositionX);
        Assert.Equal(-3.25f, character.PositionY);
        Assert.Equal(3, character.Version);
        Assert.Single(await db.CharacterInventoryItems
            .Where(value => value.CharacterId == character.Id).ToListAsync());
    }

    [PostgreSqlPersistenceFact]
    public async Task ConcurrentResumeRequestsWithSameRequestIdRotateTokenOnce()
    {
        await using TemporaryPostgreSqlDatabase testDatabase =
            await TemporaryPostgreSqlDatabase.CreateAsync();
        var tokens = new AuthTokenProtector("integration-v1",
            new Dictionary<string, string>
            {
                ["integration-v1"] = Convert.ToBase64String(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            });
        const string rawToken = "test-refresh-token-value-0123456789";
        const string deviceId = "integration-device-001";
        var account = new AccountEntity
        {
            AccountKey = testDatabase.AccountKey,
            Kind = AccountKind.Registered,
            CreatedAtUtc = DateTime.UtcNow,
        };
        account.RefreshSessions.Add(new RefreshSessionEntity
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            TokenHash = tokens.Hash(rawToken),
            DeviceIdHash = tokens.Hash(deviceId),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(3),
        });
        await using (var seed = new KnightDbContext(testDatabase.Options))
        {
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();
        }

        var service = new AccountAuthenticationService(
            testDatabase.Options,
            tokens,
            new PasswordHasher(),
            TimeSpan.FromDays(30),
            TimeSpan.FromHours(1),
            new TestClock(DateTime.UtcNow));
        Guid requestId = Guid.NewGuid();
        Task<AuthenticationResult> first = service.ResumeAsync(
            requestId, rawToken, deviceId, CancellationToken.None);
        Task<AuthenticationResult> duplicate = service.ResumeAsync(
            requestId, rawToken, deviceId, CancellationToken.None);

        AuthenticationResult[] results = await Task.WhenAll(first, duplicate);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(results[0].Account!.RefreshToken,
            results[1].Account!.RefreshToken);
        await using var verify = new KnightDbContext(testDatabase.Options);
        Assert.Equal(2, await verify.RefreshSessions.CountAsync());
        Assert.Single(await verify.AuthenticationRequestReceipts.ToListAsync());
    }

    [PostgreSqlPersistenceFact]
    public async Task ExpiredAuthenticationReceiptsAreDeleted()
    {
        await using TemporaryPostgreSqlDatabase testDatabase =
            await TemporaryPostgreSqlDatabase.CreateAsync();
        var tokens = new AuthTokenProtector("integration-v1",
            new Dictionary<string, string>
            {
                ["integration-v1"] = Convert.ToBase64String(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            });
        await using (var seed = new KnightDbContext(testDatabase.Options))
        {
            seed.AuthenticationRequestReceipts.Add(
                new AuthenticationRequestReceiptEntity
                {
                    RequestId = Guid.NewGuid(),
                    KeyId = tokens.ActiveKeyId,
                    Operation = "resume",
                    RequestFingerprint = tokens.Fingerprint("expired"),
                    ProtectedResult = tokens.Protect("expired-response"),
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(-1),
                });
            await seed.SaveChangesAsync();
        }

        var service = new AccountAuthenticationService(
            testDatabase.Options,
            tokens,
            new PasswordHasher(),
            TimeSpan.FromDays(30),
            TimeSpan.FromHours(1),
            new TestClock(DateTime.UtcNow));
        await service.DeleteExpiredReceiptsAsync(CancellationToken.None);

        await using var verify = new KnightDbContext(testDatabase.Options);
        Assert.Empty(await verify.AuthenticationRequestReceipts.ToListAsync());
    }

    private static TutorialDefinitionOptions CreateTutorialDefinition() => new()
    {
        DefinitionId = "integration_tutorial",
        InitialStepDefinitionId = "welcome",
        KillStepDefinitionId = "kill_training_wolf",
        ReturnStepDefinitionId = "return_to_mother",
        CompletedStepDefinitionId = "completed",
        QuestNpcDefinitionId = "npc_mother",
        RequiredMonsterDefinitionId = 1,
        RequiredKillCount = 1,
        QuestMapDefinitionId = "tutorial_map_01",
        QuestSpawnPointId = "spawn_01",
        ReturnMapDefinitionId = "tutorial_map_01",
        ReturnSpawnPointId = "spawn_01",
        CompletionMapDefinitionId = "tutorial_map_02",
        CompletionSpawnPointId = "spawn_02",
        ExperienceReward = 10,
        RewardItemDefinitionIds = ["starter_potion"],
    };

    private sealed class TutorialRewardBeforeSnapshotInterceptor
        : DbCommandInterceptor
    {
        private int _hasRun;

        public Func<CancellationToken, Task<TutorialCommandResult>>?
            RunTutorialRewardAsync { get; set; }
        public bool TutorialRewardRan { get; private set; }

        public override async ValueTask<InterceptionResult<int>>
            NonQueryExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            bool snapshotUpdate = command.CommandText.Contains(
                    "UPDATE", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains(
                    "position_x", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains(
                    "current_session_generation",
                    StringComparison.OrdinalIgnoreCase);
            if (snapshotUpdate &&
                Interlocked.Exchange(ref _hasRun, 1) == 0 &&
                RunTutorialRewardAsync != null)
            {
                TutorialCommandResult reward = await RunTutorialRewardAsync(
                    cancellationToken);
                TutorialRewardRan = reward.Outcome ==
                                    StarterTutorialOutcome.QuestCompleted;
            }

            return await base.NonQueryExecutingAsync(
                command, eventData, result, cancellationToken);
        }
    }

    private sealed class TestClock(DateTime utcNow) : IServerClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class TestCharacterCreationCatalog
        : ICharacterCreationCatalog
    {
        public GetCharacterCreationCatalogResponsePacket GetSnapshot(
            string serverId) => throw new NotSupportedException();

        public CharacterCreationValidationResult Validate(
            CreateCharacterRequestPacket request) =>
            throw new NotSupportedException();
    }

    private sealed class TestMapCatalog : IMapCatalog
    {
        public bool ContainsMap(string mapDefinitionId) => false;
        public bool ContainsPosition(string mapDefinitionId,
            WorldPosition position) => false;
        public WorldPosition ClampPosition(string mapDefinitionId,
            WorldPosition position) => position;
        public bool TryResolveSpawn(string mapDefinitionId,
            string spawnPointId, out MapSpawnPoint spawnPoint)
        {
            spawnPoint = default;
            return false;
        }
    }

    private sealed class TemporaryPostgreSqlDatabase : IAsyncDisposable
    {
        private TemporaryPostgreSqlDatabase(
            string schema,
            DbContextOptions<KnightDbContext> options,
            string accountKey)
        {
            Schema = schema;
            Options = options;
            AccountKey = accountKey;
        }

        private string Schema { get; }
        public DbContextOptions<KnightDbContext> Options { get; }
        public string AccountKey { get; }
        public int CharacterId { get; private set; }
        public Guid Generation { get; } = Guid.NewGuid();

        public static async Task<TemporaryPostgreSqlDatabase> CreateAsync(
            DbCommandInterceptor? interceptor = null)
        {
            string baseConnection = Environment.GetEnvironmentVariable(
                "KNIGHTONLINE_TEST_POSTGRES_CONNECTION")!;
            string schema = $"knight_test_{Guid.NewGuid():N}";
            var adminBuilder = new NpgsqlConnectionStringBuilder(baseConnection);
            await using (var admin = new NpgsqlConnection(
                             adminBuilder.ConnectionString))
            {
                await admin.OpenAsync();
                await using var createSchema = admin.CreateCommand();
                createSchema.CommandText = $"CREATE SCHEMA \"{schema}\"";
                await createSchema.ExecuteNonQueryAsync();
            }

            var testBuilder = new NpgsqlConnectionStringBuilder(baseConnection)
            {
                SearchPath = schema,
            };
            var optionsBuilder = new DbContextOptionsBuilder<KnightDbContext>()
                .UseNpgsql(testBuilder.ConnectionString);
            if (interceptor != null)
                optionsBuilder.AddInterceptors(interceptor);
            var database = new TemporaryPostgreSqlDatabase(
                schema,
                optionsBuilder.Options,
                $"account_{Guid.NewGuid():N}");

            try
            {
                await using var db = new KnightDbContext(database.Options);
                await db.Database.MigrateAsync();
                if (interceptor != null)
                    await database.SeedCharacterAsync(db);
                return database;
            }
            catch
            {
                await database.DropSchemaAsync(adminBuilder.ConnectionString);
                throw;
            }
        }

        private async Task SeedCharacterAsync(KnightDbContext db)
        {
            var account = new AccountEntity
            {
                AccountKey = AccountKey,
                Kind = AccountKind.Registered,
                CreatedAtUtc = DateTime.UtcNow,
            };
            var character = new CharacterEntity
            {
                Account = account,
                ServerId = "integration-server",
                SlotIndex = 1,
                Name = $"Test{Guid.NewGuid():N}"[..12],
                NormalizedName = $"TEST{Guid.NewGuid():N}"[..12],
                CurrentClassDefinitionId = "warrior",
                BodyTypeDefinitionId = "male",
                CurrentMapDefinitionId = "tutorial_map_01",
                CurrentSpawnPointId = "spawn_01",
                PositionX = 0,
                PositionY = 0,
                CurrentSessionGeneration = Generation,
                Version = 1,
                CreatedAtUtc = DateTime.UtcNow,
            };
            character.TutorialProgress.Add(new CharacterTutorialProgressEntity
            {
                TutorialDefinitionId = "integration_tutorial",
                CurrentStepDefinitionId = "return_to_mother",
                State = TutorialState.CoreTutorial,
                ObjectiveProgress = 1,
                UpdatedAtUtc = DateTime.UtcNow,
            });
            db.Accounts.Add(account);
            db.Characters.Add(character);
            await db.SaveChangesAsync();
            CharacterId = character.Id;
        }

        public async ValueTask DisposeAsync()
        {
            var builder = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable(
                    "KNIGHTONLINE_TEST_POSTGRES_CONNECTION")!);
            await DropSchemaAsync(builder.ConnectionString);
        }

        private async Task DropSchemaAsync(string connectionString)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var dropSchema = connection.CreateCommand();
            dropSchema.CommandText = $"DROP SCHEMA IF EXISTS \"{Schema}\" CASCADE";
            await dropSchema.ExecuteNonQueryAsync();
        }
    }
}
