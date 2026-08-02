using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Characters;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Networking.Handlers;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Players;
using KnightOnline.Server.World;
using KnightOnline.Server.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnightServer.IntegrationTests.Characters;

public sealed class PostgresCharacterRepositoryIntegrationTests
{
    private static readonly DateTime InitialUtc =
        new(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task CreateListAndOwnership_PersistAndRemainAccountScoped()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        string otherAccountKey = $"character-it-other-{context.Suffix}";
        await context.Repository.EnsureAccountExistsAsync(otherAccountKey);
        database.TrackAccount(otherAccountKey);
        CreateCharacterRequestPacket request = CreateRequest(
            context.Options,
            $"Knight {context.Suffix}",
            slotIndex: 1);

        CreateCharacterResponsePacket created =
            await context.Repository.CreateAsync(
                context.AccountKey,
                request,
                CancellationToken.None);

        Assert.Equal(CreateCharacterResult.Success, created.Result);
        Assert.NotNull(created.Character);
        IReadOnlyList<CharacterSummaryPacket> roster =
            await context.Repository.ListAsync(context.AccountKey);
        Assert.Single(roster);
        Assert.Equal(created.Character!.CharacterId, roster[0].CharacterId);
        Assert.Null(await context.Repository.FindOwnedAsync(
            otherAccountKey,
            created.Character.CharacterId));
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task RetryWithSameRequestId_ReturnsStoredCharacterWithoutDuplicate()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        CreateCharacterRequestPacket request = CreateRequest(
            context.Options,
            $"Retry {context.Suffix}",
            slotIndex: 1);

        CreateCharacterResponsePacket first =
            await context.Repository.CreateAsync(
                context.AccountKey,
                request,
                CancellationToken.None);
        CreateCharacterResponsePacket retry =
            await context.Repository.CreateAsync(
                context.AccountKey,
                request,
                CancellationToken.None);

        Assert.Equal(CreateCharacterResult.Success, first.Result);
        Assert.Equal(CreateCharacterResult.Success, retry.Result);
        Assert.Equal(first.Character!.CharacterId, retry.Character!.CharacterId);
        Assert.Single(await context.Repository.ListAsync(context.AccountKey));
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task DuplicateNormalizedNameAndFourthCharacter_AreRejected()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        string sharedName = $"Hero {context.Suffix}";

        CreateCharacterResponsePacket first =
            await context.Repository.CreateAsync(
                context.AccountKey,
                CreateRequest(context.Options, sharedName, 1),
                CancellationToken.None);
        CreateCharacterResponsePacket duplicateName =
            await context.Repository.CreateAsync(
                context.AccountKey,
                CreateRequest(
                    context.Options,
                    sharedName.ToLowerInvariant(),
                    2),
                CancellationToken.None);
        await context.Repository.CreateAsync(
            context.AccountKey,
            CreateRequest(context.Options, $"Mage {context.Suffix}", 2),
            CancellationToken.None);
        await context.Repository.CreateAsync(
            context.AccountKey,
            CreateRequest(context.Options, $"Archer {context.Suffix}", 3),
            CancellationToken.None);
        CreateCharacterResponsePacket fourth =
            await context.Repository.CreateAsync(
                context.AccountKey,
                CreateRequest(context.Options, $"Fourth {context.Suffix}", 1),
                CancellationToken.None);

        Assert.Equal(CreateCharacterResult.Success, first.Result);
        Assert.Equal(
            CreateCharacterResult.NameAlreadyTaken,
            duplicateName.Result);
        Assert.Equal(
            CreateCharacterResult.CharacterLimitReached,
            fourth.Result);
        Assert.Equal(
            context.Options.MaximumPerAccount,
            (await context.Repository.ListAsync(context.AccountKey)).Count);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task MissingAccount_IsRejectedWithoutCreatingCharacter()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        CharacterOptions options = LoadCharacterOptions();
        CharacterRepository repository = CreateRepository(
            database.Options,
            options);

        CreateCharacterResponsePacket response = await repository.CreateAsync(
            $"missing-{Guid.NewGuid():N}",
            CreateRequest(options, "Missing Account", 1),
            CancellationToken.None);

        Assert.Equal(CreateCharacterResult.Unauthorized, response.Result);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task CreateAndList_TravelThroughTcpAndDispatcher()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        var clock = new MutableClock(InitialUtc);
        var leases = CreateLeaseStore();
        await using NetworkTestConnection connection =
            await NetworkTestConnection.CreateAsync(context, leases, clock);
        await connection.AttachAccountAsync(
            context.AccountKey,
            leases,
            clock.UtcNow);

        await connection.SendAsync(
            PacketType.CreateCharacterRequest,
            CreateRequest(
                context.Options,
                $"Tcp {context.Suffix}",
                slotIndex: 1));
        CreateCharacterResponsePacket created =
            await connection.ReadPayloadAsync<CreateCharacterResponsePacket>(
                PacketType.CreateCharacterResponse);
        await connection.SendAsync(
            PacketType.ListCharactersRequest,
            new ListCharactersRequestPacket(context.Options.ServerId));
        ListCharactersResponsePacket listed =
            await connection.ReadPayloadAsync<ListCharactersResponsePacket>(
                PacketType.ListCharactersResponse);
        await connection.SendRawPayloadAsync(
            PacketType.ListCharactersRequest,
            "{}");
        ListCharactersResponsePacket legacyList =
            await connection.ReadPayloadAsync<ListCharactersResponsePacket>(
                PacketType.ListCharactersResponse);

        Assert.Equal(CreateCharacterResult.Success, created.Result);
        Assert.Equal(ListCharactersResult.Success, listed.Result);
        Assert.Single(listed.Characters);
        Assert.Equal(ListCharactersResult.Success, legacyList.Result);
        Assert.Single(legacyList.Characters);
        Assert.Equal(
            created.Character!.CharacterId,
            listed.Characters[0].CharacterId);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task MalformedCreateAndWrongListServer_ReturnExplicitRejections()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        var clock = new MutableClock(InitialUtc);
        var leases = CreateLeaseStore();
        await using NetworkTestConnection connection =
            await NetworkTestConnection.CreateAsync(context, leases, clock);
        await connection.AttachAccountAsync(
            context.AccountKey,
            leases,
            clock.UtcNow);

        await connection.SendRawPayloadAsync(
            PacketType.CreateCharacterRequest,
            "{not-valid-json");
        CreateCharacterResponsePacket malformed =
            await connection.ReadPayloadAsync<CreateCharacterResponsePacket>(
                PacketType.CreateCharacterResponse);
        await connection.SendAsync(
            PacketType.ListCharactersRequest,
            new ListCharactersRequestPacket("unknown-server"));
        ListCharactersResponsePacket wrongServer =
            await connection.ReadPayloadAsync<ListCharactersResponsePacket>(
                PacketType.ListCharactersResponse);
        await connection.SendRawPayloadAsync(
            PacketType.ListCharactersRequest,
            "{not-valid-json");
        ListCharactersResponsePacket malformedList =
            await connection.ReadPayloadAsync<ListCharactersResponsePacket>(
                PacketType.ListCharactersResponse);

        Assert.Equal(
            CreateCharacterResult.MalformedRequest,
            malformed.Result);
        Assert.Equal(
            ListCharactersResult.InvalidServer,
            wrongServer.Result);
        Assert.Equal(
            ListCharactersResult.MalformedRequest,
            malformedList.Result);
        Assert.Empty(await context.Repository.ListAsync(context.AccountKey));
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task AnonymousCharacterRequest_IsRejectedBeforeRepository()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        var clock = new MutableClock(InitialUtc);
        var leases = CreateLeaseStore();
        await using NetworkTestConnection connection =
            await NetworkTestConnection.CreateAsync(context, leases, clock);

        await connection.SendAsync(
            PacketType.CreateCharacterRequest,
            CreateRequest(
                context.Options,
                $"Anon {context.Suffix}",
                slotIndex: 1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => connection.ReadEnvelopeAsync(
                TimeSpan.FromMilliseconds(250)));
        Assert.Empty(await context.Repository.ListAsync(context.AccountKey));
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ExpiredLease_IsForcedOffBeforeCharacterQuery()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        var clock = new MutableClock(InitialUtc);
        var leases = CreateLeaseStore();
        await using NetworkTestConnection connection =
            await NetworkTestConnection.CreateAsync(context, leases, clock);
        await connection.AttachAccountAsync(
            context.AccountKey,
            leases,
            clock.UtcNow);
        clock.UtcNow = InitialUtc.AddSeconds(21);

        await connection.SendAsync(
            PacketType.ListCharactersRequest,
            new ListCharactersRequestPacket(context.Options.ServerId));
        ForcedDisconnectPacket forced =
            await connection.ReadPayloadAsync<ForcedDisconnectPacket>(
                PacketType.ForcedDisconnect);

        Assert.Equal(
            ForcedDisconnectReason.SessionLeaseExpired,
            forced.Reason);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task SelectAndEnterWorld_AreIdempotentAcrossTcp()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        CreateCharacterResponsePacket created =
            await context.Repository.CreateAsync(
                context.AccountKey,
                CreateRequest(
                    context.Options,
                    $"World {context.Suffix}",
                    slotIndex: 1),
                CancellationToken.None);
        var clock = new MutableClock(InitialUtc);
        var leases = CreateLeaseStore();
        await using NetworkTestConnection connection =
            await NetworkTestConnection.CreateAsync(context, leases, clock);
        await connection.AttachAccountAsync(
            context.AccountKey,
            leases,
            clock.UtcNow);

        var selectRequest = new SelectCharacterRequestPacket(
            created.Character!.CharacterId);
        await connection.SendAsync(
            PacketType.SelectCharacterRequest,
            selectRequest);
        SelectCharacterResponsePacket selected =
            await connection.ReadPayloadAsync<SelectCharacterResponsePacket>(
                PacketType.SelectCharacterResponse);
        await connection.SendAsync(
            PacketType.SelectCharacterRequest,
            selectRequest);
        SelectCharacterResponsePacket retriedSelection =
            await connection.ReadPayloadAsync<SelectCharacterResponsePacket>(
                PacketType.SelectCharacterResponse);

        await connection.SendAsync(
            PacketType.EnterWorldRequest,
            new EnterWorldRequestPacket(selected.GameplaySessionId));
        EnterWorldResponsePacket entered =
            await connection.ReadPayloadAsync<EnterWorldResponsePacket>(
                PacketType.EnterWorldResponse);
        await connection.SendAsync(
            PacketType.EnterWorldRequest,
            new EnterWorldRequestPacket(selected.GameplaySessionId));
        EnterWorldResponsePacket retriedEntry =
            await connection.ReadPayloadAsync<EnterWorldResponsePacket>(
                PacketType.EnterWorldResponse);

        Assert.Equal(SelectCharacterResult.Success, selected.Result);
        Assert.NotEqual(Guid.Empty, selected.GameplaySessionId);
        Assert.Equal(
            selected.GameplaySessionId,
            retriedSelection.GameplaySessionId);
        Assert.Equal(EnterWorldResult.Success, entered.Result);
        Assert.Equal(EnterWorldResult.Success, retriedEntry.Result);
        Assert.Equal(
            selected.GameplaySessionId,
            entered.Snapshot!.GameplaySessionId);
        Assert.Equal(
            context.Options.StartingMapDefinitionId,
            entered.Snapshot.Character.MapDefinitionId);
        Assert.Equal(
            context.Options.StartingSpawnPointId,
            entered.Snapshot.Character.SpawnPointId);
        Assert.Equal(
            entered.Snapshot.Character.CharacterId,
            retriedEntry.Snapshot!.Character.CharacterId);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task SelectOtherAccountsCharacter_IsRejectedAcrossTcp()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext owner = await CreateContextAsync(database);
        CreateCharacterResponsePacket created =
            await owner.Repository.CreateAsync(
                owner.AccountKey,
                CreateRequest(
                    owner.Options,
                    $"Owner {owner.Suffix}",
                    slotIndex: 1),
                CancellationToken.None);
        string otherAccount = $"character-it-other-{owner.Suffix}";
        await owner.Repository.EnsureAccountExistsAsync(otherAccount);
        database.TrackAccount(otherAccount);
        var clock = new MutableClock(InitialUtc);
        var leases = CreateLeaseStore();
        await using NetworkTestConnection connection =
            await NetworkTestConnection.CreateAsync(owner, leases, clock);
        await connection.AttachAccountAsync(
            otherAccount,
            leases,
            clock.UtcNow);

        await connection.SendRawPayloadAsync(
            PacketType.SelectCharacterRequest,
            "{not-valid-json");
        SelectCharacterResponsePacket malformed =
            await connection.ReadPayloadAsync<SelectCharacterResponsePacket>(
                PacketType.SelectCharacterResponse);
        await connection.SendAsync(
            PacketType.SelectCharacterRequest,
            new SelectCharacterRequestPacket(
                created.Character!.CharacterId));
        SelectCharacterResponsePacket response =
            await connection.ReadPayloadAsync<SelectCharacterResponsePacket>(
                PacketType.SelectCharacterResponse);

        Assert.Equal(
            SelectCharacterResult.MalformedRequest,
            malformed.Result);
        Assert.Equal(
            SelectCharacterResult.CharacterNotFound,
            response.Result);
        Assert.Equal(Guid.Empty, response.GameplaySessionId);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task EnterWorldWithStaleSessionId_IsRejectedAcrossTcp()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        CreateCharacterResponsePacket created =
            await context.Repository.CreateAsync(
                context.AccountKey,
                CreateRequest(
                    context.Options,
                    $"Stale {context.Suffix}",
                    slotIndex: 1),
                CancellationToken.None);
        var clock = new MutableClock(InitialUtc);
        var leases = CreateLeaseStore();
        await using NetworkTestConnection connection =
            await NetworkTestConnection.CreateAsync(context, leases, clock);
        await connection.AttachAccountAsync(
            context.AccountKey,
            leases,
            clock.UtcNow);
        await connection.SendAsync(
            PacketType.SelectCharacterRequest,
            new SelectCharacterRequestPacket(
                created.Character!.CharacterId));
        SelectCharacterResponsePacket selected =
            await connection.ReadPayloadAsync<SelectCharacterResponsePacket>(
                PacketType.SelectCharacterResponse);

        await connection.SendAsync(
            PacketType.EnterWorldRequest,
            new EnterWorldRequestPacket(Guid.NewGuid()));
        EnterWorldResponsePacket response =
            await connection.ReadPayloadAsync<EnterWorldResponsePacket>(
                PacketType.EnterWorldResponse);

        Assert.Equal(SelectCharacterResult.Success, selected.Result);
        Assert.Equal(EnterWorldResult.SessionMismatch, response.Result);
        Assert.Null(response.Snapshot);
    }

    private static async Task<TestContext> CreateContextAsync(
        TestDatabase database)
    {
        CharacterOptions options = LoadCharacterOptions();
        CharacterRepository repository = CreateRepository(
            database.Options,
            options);
        string suffix = Guid.NewGuid().ToString("N")[..10];
        string accountKey = $"character-it-{suffix}";
        await repository.EnsureAccountExistsAsync(accountKey);
        database.TrackAccount(accountKey);
        return new TestContext(
            repository,
            options,
            accountKey,
            suffix);
    }

    private static CharacterRepository CreateRepository(
        DbContextOptions<KnightDbContext> databaseOptions,
        CharacterOptions options) =>
        new(
            databaseOptions,
            options,
            CreateCatalog(options),
            new CharacterNamePolicy(),
            new FixedClock(new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)));

    private static InMemoryActiveAccountLeaseStore CreateLeaseStore() =>
        new(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            maximumActiveAccounts: 500);

    private static CharacterOptions LoadCharacterOptions() =>
        ServerOptions.Load(
            Path.Combine(AppContext.BaseDirectory, "serverSettings.json"))
        .Characters;

    private static ICharacterCreationCatalog CreateCatalog(
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

    private static CreateCharacterRequestPacket CreateRequest(
        CharacterOptions options,
        string name,
        int slotIndex) =>
        new(
            Guid.NewGuid(),
            options.ServerId,
            slotIndex,
            name,
            "warrior",
            "male",
            [
                new AppearanceSelectionPacket("base_body", "body_male_001"),
                new AppearanceSelectionPacket("hair", "hair_001"),
                new AppearanceSelectionPacket("bottom", "bottom_001"),
                new AppearanceSelectionPacket(
                    "expression",
                    "expression_001"),
            ],
            options.CatalogVersion);

    private sealed record TestContext(
        CharacterRepository Repository,
        CharacterOptions Options,
        string AccountKey,
        string Suffix);

    private sealed class FixedClock(DateTime utcNow) : IServerClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class MutableClock(DateTime utcNow) : IServerClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class NetworkTestConnection : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly ClientConnection _server;
        private readonly CancellationTokenSource _serverCancellation = new();
        private readonly Task _serverTask;

        private NetworkTestConnection(
            TcpClient client,
            ClientConnection server)
        {
            _client = client;
            _server = server;
            _serverTask = server.RunAsync(_serverCancellation.Token);
        }

        public static async Task<NetworkTestConnection> CreateAsync(
            TestContext context,
            IActiveAccountLeaseStore leases,
            IServerClock clock)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var client = new TcpClient();
            Task connect = client.ConnectAsync(IPAddress.Loopback, port);
            TcpClient accepted = await listener.AcceptTcpClientAsync();
            await connect;
            listener.Stop();

            var worldOptions = new WorldOptions
            {
                TickMilliseconds = 250,
                MaximumMovementDeltaMilliseconds = 250,
                PlayerCollisionRadius = 0.35f,
                MonsterCollisionRadius = 0.5f,
            };
            IWorldMovementResolver movementResolver =
                new MonsterCollisionMovementResolver(
                    new MonsterService(),
                    worldOptions);
            var dispatcher = new PacketDispatcher(
                [
                    new CreateCharacterPacketHandler(context.Repository),
                    new ListCharactersPacketHandler(
                        context.Repository,
                        context.Options),
                    new SelectCharacterPacketHandler(
                        context.Repository,
                        new ActivePlayerRegistry(),
                        leases,
                        context.Options,
                        new CombatOptions
                        {
                            BaseAttackDamage = 10,
                            AttackRange = 2,
                            AttackCooldownMilliseconds = 750,
                        },
                        worldOptions,
                        clock),
                    new EnterWorldPacketHandler(clock, movementResolver),
                ],
                leases,
                clock);
            var server = new ClientConnection(
                accepted,
                dispatcher,
                maximumPacketSize: 1024 * 1024);
            return new NetworkTestConnection(client, server);
        }

        public async Task AttachAccountAsync(
            string accountKey,
            IActiveAccountLeaseStore leases,
            DateTime utcNow)
        {
            ActiveAccountLeaseClaim claim = await leases.TryClaimAsync(
                accountKey,
                _server.ConnectionId,
                utcNow);
            Assert.Equal(
                ActiveAccountLeaseClaimStatus.Acquired,
                claim.Status);
            Assert.True(_server.TryAttachAccount(
                accountKey,
                claim.Generation));
        }

        public Task SendAsync<T>(PacketType type, T payload) =>
            SendRawPayloadAsync(type, JsonSerializer.Serialize(payload));

        public async Task SendRawPayloadAsync(
            PacketType type,
            string payload)
        {
            string envelopeJson = JsonSerializer.Serialize(
                new PacketEnvelope(type, payload));
            byte[] bytes = Encoding.UTF8.GetBytes(envelopeJson);
            NetworkStream stream = _client.GetStream();
            await stream.WriteAsync(BitConverter.GetBytes(bytes.Length));
            await stream.WriteAsync(bytes);
        }

        public async Task<T> ReadPayloadAsync<T>(PacketType expectedType)
        {
            PacketEnvelope envelope = await ReadEnvelopeAsync(
                TimeSpan.FromSeconds(3));
            Assert.Equal(expectedType, envelope.Type);
            return JsonSerializer.Deserialize<T>(envelope.Payload)
                   ?? throw new InvalidDataException(
                       "Server returned an empty response payload.");
        }

        public async Task<PacketEnvelope> ReadEnvelopeAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            NetworkStream stream = _client.GetStream();
            var lengthBytes = new byte[sizeof(int)];
            await stream.ReadExactlyAsync(
                lengthBytes,
                cancellation.Token);
            int length = BitConverter.ToInt32(lengthBytes);
            var payload = new byte[length];
            await stream.ReadExactlyAsync(payload, cancellation.Token);
            return JsonSerializer.Deserialize<PacketEnvelope>(
                       Encoding.UTF8.GetString(payload))
                   ?? throw new InvalidDataException(
                       "Server returned an invalid packet envelope.");
        }

        public async ValueTask DisposeAsync()
        {
            _serverCancellation.Cancel();
            _client.Dispose();
            await _server.DisposeAsync();
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }
            _serverCancellation.Dispose();
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly List<string> _accountKeys = [];
        public DbContextOptions<KnightDbContext> Options { get; }

        private TestDatabase(DbContextOptions<KnightDbContext> options) =>
            Options = options;

        public static async Task<TestDatabase> CreateAsync()
        {
            string connectionString =
                DatabaseConfiguration.GetRequiredConnectionString(
                    DatabaseConfiguration.Build());
            var options = new DbContextOptionsBuilder<KnightDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var db = new KnightDbContext(options);
            await db.Database.MigrateAsync();
            return new TestDatabase(options);
        }

        public void TrackAccount(string accountKey) =>
            _accountKeys.Add(accountKey);

        public async ValueTask DisposeAsync()
        {
            if (_accountKeys.Count == 0)
                return;

            await using var db = new KnightDbContext(Options);
            await db.Accounts
                .Where(account => _accountKeys.Contains(account.AccountKey))
                .ExecuteDeleteAsync();
        }
    }
}
