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
using KnightOnline.Server.Progression;
using KnightOnline.Server.World;
using KnightOnline.Server.Time;
using KnightOnline.Server.Tutorials;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnightServer.IntegrationTests.Characters;

public sealed class PostgresCharacterRepositoryIntegrationTests
{
    private static readonly DateTime InitialUtc =
        new(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task StarterTutorial_DeduplicatesKills_AndRewardsExactlyOnce()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        CreateCharacterResponsePacket created = await context.Repository.CreateAsync(
            context.AccountKey,
            CreateRequest(context.Options, $"Quest{context.Suffix}", 1),
            CancellationToken.None);
        int characterId = created.Character!.CharacterId;
        ServerOptions options = LoadServerOptions();
        TutorialDefinitionOptions definition = options.TutorialDefinitions.Single();
        var service = new StarterTutorialService(database.Options, definition,
            new ConfiguredExperienceCurve(options.Progression),
            new ConfiguredMapCatalog(options.MapDefinitions),
            new FixedClock(InitialUtc));

        TutorialCommandResult accepted = await service.InteractWithQuestNpcAsync(
            Guid.NewGuid(), characterId, CancellationToken.None);
        Assert.Equal(StarterTutorialOutcome.QuestAccepted, accepted.Outcome);
        Assert.Equal(definition.ReturnMapDefinitionId, accepted.MapDefinitionId);
        await service.PersistPortalTransitionAsync(characterId,
            definition.QuestMapDefinitionId, definition.QuestSpawnPointId,
            -7f, 0f, CancellationToken.None);

        Guid firstLife = Guid.NewGuid();
        TutorialCommandResult firstKill = await service.RecordKillAsync(characterId,
            firstLife, definition.RequiredMonsterDefinitionId,
            definition.QuestMapDefinitionId, CancellationToken.None);
        TutorialCommandResult duplicate = await service.RecordKillAsync(characterId,
            firstLife, definition.RequiredMonsterDefinitionId,
            definition.QuestMapDefinitionId, CancellationToken.None);
        Assert.Equal(1, firstKill.Progress.ObjectiveProgress);
        Assert.Equal(TutorialCommandStatus.AlreadyApplied, duplicate.Status);
        Assert.Equal(1, duplicate.Progress.ObjectiveProgress);

        TutorialCommandResult progress = firstKill;
        for (int index = 1; index < definition.RequiredKillCount; index++)
        {
            progress = await service.RecordKillAsync(characterId, Guid.NewGuid(),
                definition.RequiredMonsterDefinitionId,
                definition.QuestMapDefinitionId, CancellationToken.None);
        }
        Assert.Equal(StarterTutorialOutcome.ReadyToTurnIn, progress.Outcome);
        await service.PersistPortalTransitionAsync(characterId,
            definition.ReturnMapDefinitionId, definition.ReturnSpawnPointId,
            0f, 0f, CancellationToken.None);

        Guid rewardRequest = Guid.NewGuid();
        TutorialCommandResult completed = await service.InteractWithQuestNpcAsync(
            rewardRequest, characterId, CancellationToken.None);
        TutorialCommandResult retried = await service.InteractWithQuestNpcAsync(
            rewardRequest, characterId, CancellationToken.None);
        Assert.Equal(StarterTutorialOutcome.QuestCompleted, completed.Outcome);
        Assert.Equal(TutorialCommandStatus.AlreadyApplied, retried.Status);
        Assert.Equal(2, completed.Level);
        Assert.Equal(3, completed.Inventory.Count);
        Assert.Equal(definition.ReturnMapDefinitionId,
            completed.MapDefinitionId);

        await using var verification = new KnightDbContext(database.Options);
        Assert.Equal(definition.RequiredKillCount,
            await verification.TutorialKillCredits.CountAsync(value =>
                value.CharacterId == characterId));
        Assert.Equal(3, await verification.CharacterInventoryItems.CountAsync(
            value => value.CharacterId == characterId));
        Assert.Equal(1, await verification.CharacterProgressionGrants.CountAsync(
            value => value.CharacterId == characterId &&
                     value.SourceType == "tutorial_reward"));
        Assert.Equal(1, await verification.GameplayAuditRecords.CountAsync(
            value => value.CharacterId == characterId &&
                     value.RequestId == rewardRequest));
        Assert.Equal(1, await verification.DomainOutboxMessages.CountAsync(
            value => value.CorrelationId == rewardRequest &&
                     value.EventType == "TutorialCompleted"));
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task StarterTutorial_NpcAndPortalTravelAcrossTcp_AndReloadsState()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        CreateCharacterResponsePacket created = await context.Repository.CreateAsync(
            context.AccountKey,
            CreateRequest(context.Options, $"WireQuest{context.Suffix}", 1),
            CancellationToken.None);
        ServerOptions options = LoadServerOptions();
        TutorialDefinitionOptions definition = options.TutorialDefinitions.Single();
        var clock = new MutableClock(InitialUtc);
        var leases = CreateLeaseStore();
        await using NetworkTestConnection connection =
            await NetworkTestConnection.CreateAsync(context, leases, clock);
        await connection.AttachAccountAsync(context.AccountKey, leases,
            clock.UtcNow);
        await connection.SendAsync(PacketType.SelectCharacterRequest,
            new SelectCharacterRequestPacket(created.Character!.CharacterId));
        await connection.ReadPayloadAsync<SelectCharacterResponsePacket>(
            PacketType.SelectCharacterResponse);

        await connection.SendAsync(PacketType.InteractNpcRequest,
            new InteractNpcRequestPacket(Guid.NewGuid(),
                definition.QuestNpcDefinitionId));
        InteractNpcResponsePacket accepted =
            await connection.ReadPayloadAsync<InteractNpcResponsePacket>(
                PacketType.InteractNpcResponse);
        await connection.ReadPayloadAsync<TutorialProgressSnapshotPacket>(
            PacketType.TutorialProgressSnapshot);
        Assert.Equal(NpcInteractionResult.Success, accepted.Result);

        PortalDefinitionOptions toWolf = options.PortalDefinitions.Single(value =>
            value.DefinitionId == "village_to_wolf_field");
        connection.TeleportPlayer(toWolf.SourceMapDefinitionId,
            toWolf.PositionX, toWolf.PositionY, clock.UtcNow);
        await connection.SendAsync(PacketType.UsePortalRequest,
            new UsePortalRequestPacket(Guid.NewGuid(), toWolf.DefinitionId));
        Assert.Equal(PortalUseResult.Success,
            (await connection.ReadPayloadAsync<UsePortalResponsePacket>(
                PacketType.UsePortalResponse)).Result);
        MapTransitionSnapshotPacket wolfField = await connection
            .ReadPayloadAsync<MapTransitionSnapshotPacket>(
                PacketType.MapTransitionSnapshot);
        Assert.Equal(definition.QuestMapDefinitionId, wolfField.MapDefinitionId);

        var service = new StarterTutorialService(database.Options, definition,
            new ConfiguredExperienceCurve(options.Progression),
            new ConfiguredMapCatalog(options.MapDefinitions), clock);
        for (int index = 0; index < definition.RequiredKillCount; index++)
            await service.RecordKillAsync(created.Character.CharacterId,
                Guid.NewGuid(), definition.RequiredMonsterDefinitionId,
                definition.QuestMapDefinitionId, CancellationToken.None);

        PortalDefinitionOptions portal = options.PortalDefinitions.Single(value =>
            value.DefinitionId == "wolf_field_return_to_village");
        connection.TeleportPlayer(portal.SourceMapDefinitionId,
            portal.PositionX, portal.PositionY, clock.UtcNow);
        await connection.SendAsync(PacketType.UsePortalRequest,
            new UsePortalRequestPacket(Guid.NewGuid(), portal.DefinitionId));
        UsePortalResponsePacket portalResponse =
            await connection.ReadPayloadAsync<UsePortalResponsePacket>(
                PacketType.UsePortalResponse);
        MapTransitionSnapshotPacket village =
            await connection.ReadPayloadAsync<MapTransitionSnapshotPacket>(
                PacketType.MapTransitionSnapshot);
        Assert.Equal(PortalUseResult.Success, portalResponse.Result);
        Assert.Equal(definition.ReturnMapDefinitionId, village.MapDefinitionId);

        await connection.SendAsync(PacketType.InteractNpcRequest,
            new InteractNpcRequestPacket(Guid.NewGuid(),
                definition.QuestNpcDefinitionId));
        await connection.ReadPayloadAsync<InteractNpcResponsePacket>(
            PacketType.InteractNpcResponse);
        TutorialProgressSnapshotPacket completed =
            await connection.ReadPayloadAsync<TutorialProgressSnapshotPacket>(
                PacketType.TutorialProgressSnapshot);
        await connection.ReadPayloadAsync<CharacterProgressionChangedPacket>(
            PacketType.CharacterProgressionChanged);
        await connection.ReadPayloadAsync<CharacterVitalsSnapshotPacket>(
            PacketType.CharacterVitalsSnapshot);
        InventorySnapshotPacket inventory =
            await connection.ReadPayloadAsync<InventorySnapshotPacket>(
                PacketType.InventorySnapshot);
        Assert.Equal(definition.CompletedStepDefinitionId,
            completed.StepDefinitionId);
        Assert.Equal(3, inventory.Items.Count);

        PortalDefinitionOptions toSafe = options.PortalDefinitions.Single(value =>
            value.DefinitionId == "village_to_safe_zone_01");
        connection.TeleportPlayer(toSafe.SourceMapDefinitionId,
            toSafe.PositionX, toSafe.PositionY, clock.UtcNow);
        await connection.SendAsync(PacketType.UsePortalRequest,
            new UsePortalRequestPacket(Guid.NewGuid(), toSafe.DefinitionId));
        Assert.Equal(PortalUseResult.Success,
            (await connection.ReadPayloadAsync<UsePortalResponsePacket>(
                PacketType.UsePortalResponse)).Result);
        MapTransitionSnapshotPacket safeZone = await connection
            .ReadPayloadAsync<MapTransitionSnapshotPacket>(
                PacketType.MapTransitionSnapshot);
        Assert.Equal(definition.CompletionMapDefinitionId,
            safeZone.MapDefinitionId);

        TutorialCommandResult reloaded = await service.GetCurrentAsync(
            created.Character.CharacterId, CancellationToken.None);
        Assert.Equal(TutorialState.Completed, reloaded.Progress.State);
        Assert.Equal(3, reloaded.Inventory.Count);
        Assert.Equal(definition.CompletionMapDefinitionId,
            reloaded.MapDefinitionId);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ThreeSlots_HaveIndependentProgressionAndTutorialState()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        CreateCharacterResponsePacket[] created = new CreateCharacterResponsePacket[3];
        for (int slot = 1; slot <= 3; slot++)
        {
            created[slot - 1] = await context.Repository.CreateAsync(
                context.AccountKey,
                CreateRequest(
                    context.Options,
                    $"Slot{slot}{context.Suffix}",
                    slot),
                CancellationToken.None);
            Assert.Equal(CreateCharacterResult.Success, created[slot - 1].Result);
        }

        int firstCharacterId = created[0].Character!.CharacterId;
        await using (var mutation = new KnightDbContext(database.Options))
        {
            await mutation.Characters
                .Where(value => value.Id == firstCharacterId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(value => value.Level, 7)
                    .SetProperty(value => value.TotalExperience, 900));
            await mutation.CharacterTutorialProgress
                .Where(value => value.CharacterId == firstCharacterId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(value => value.ObjectiveProgress, 13));
        }

        await using var verification = new KnightDbContext(database.Options);
        var states = await verification.Characters
            .AsNoTracking()
            .Where(value => value.Account.AccountKey == context.AccountKey)
            .OrderBy(value => value.SlotIndex)
            .Select(value => new
            {
                value.SlotIndex,
                value.Level,
                value.TotalExperience,
                ObjectiveProgress = value.TutorialProgress
                    .Select(progress => progress.ObjectiveProgress)
                    .Single(),
            })
            .ToArrayAsync();

        Assert.Equal(3, states.Length);
        Assert.Equal((7, 900L, 13),
            (states[0].Level, states[0].TotalExperience,
                states[0].ObjectiveProgress));
        Assert.Equal((1, 0L, 0),
            (states[1].Level, states[1].TotalExperience,
                states[1].ObjectiveProgress));
        Assert.Equal((1, 0L, 0),
            (states[2].Level, states[2].TotalExperience,
                states[2].ObjectiveProgress));
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ExperienceGrant_IsIdempotentAndPersistsAtomically()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        CreateCharacterResponsePacket created =
            await context.Repository.CreateAsync(
                context.AccountKey,
                CreateRequest(
                    context.Options,
                    $"Progress {context.Suffix}",
                    slotIndex: 1),
                CancellationToken.None);
        Assert.Equal(CreateCharacterResult.Success, created.Result);

        ServerOptions serverOptions = LoadServerOptions();
        var curve = new ConfiguredExperienceCurve(serverOptions.Progression);
        var service = new CharacterProgressionService(
            database.Options,
            curve,
            new FixedClock(InitialUtc));
        Guid requestId = Guid.NewGuid();
        long reward = curve.GetExperienceRequiredToAdvance(1);

        ProgressionGrantResult first = await service.GrantExperienceAsync(
            requestId,
            created.Character!.CharacterId,
            reward,
            serverOptions.Progression.MaximumLevel,
            "integration_test",
            "monster-life-1",
            CancellationToken.None);
        ProgressionGrantResult retry = await service.GrantExperienceAsync(
            requestId,
            created.Character.CharacterId,
            reward,
            serverOptions.Progression.MaximumLevel,
            "integration_test",
            "monster-life-1",
            CancellationToken.None);

        Assert.Equal(ProgressionGrantStatus.Applied, first.Status);
        Assert.Equal(2, first.LevelAfter);
        Assert.Equal(ProgressionGrantStatus.AlreadyApplied, retry.Status);
        Assert.Equal(first.TotalExperience, retry.TotalExperience);
        await using var verification = new KnightDbContext(database.Options);
        Assert.Equal(
            first.TotalExperience,
            await verification.Characters
                .Where(value => value.Id == created.Character.CharacterId)
                .Select(value => value.TotalExperience)
                .SingleAsync());
        Assert.Equal(
            1,
            await verification.CharacterProgressionGrants.CountAsync(
                value => value.CharacterId == created.Character.CharacterId));
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task LegacyCharacterLevel_IsNotReducedByFirstExperienceGrant()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        TestContext context = await CreateContextAsync(database);
        CreateCharacterResponsePacket created =
            await context.Repository.CreateAsync(
                context.AccountKey,
                CreateRequest(
                    context.Options,
                    $"Legacy {context.Suffix}",
                    slotIndex: 1),
                CancellationToken.None);
        Assert.Equal(CreateCharacterResult.Success, created.Result);

        await using (var setup = new KnightDbContext(database.Options))
        {
            await setup.Characters
                .Where(value => value.Id == created.Character!.CharacterId)
                .ExecuteUpdateAsync(value => value
                    .SetProperty(character => character.Level, 5)
                    .SetProperty(character => character.TotalExperience, 0));
        }

        ServerOptions serverOptions = LoadServerOptions();
        var curve = new ConfiguredExperienceCurve(serverOptions.Progression);
        var service = new CharacterProgressionService(
            database.Options,
            curve,
            new FixedClock(InitialUtc));
        ProgressionGrantResult result = await service.GrantExperienceAsync(
            Guid.NewGuid(),
            created.Character!.CharacterId,
            1,
            serverOptions.Progression.MaximumLevel,
            "legacy_normalization",
            "integration-test",
            CancellationToken.None);

        Assert.Equal(ProgressionGrantStatus.Applied, result.Status);
        Assert.Equal(5, result.LevelBefore);
        Assert.Equal(5, result.LevelAfter);
        Assert.Equal(
            curve.GetTotalExperienceRequiredForLevel(5) + 1,
            result.TotalExperience);
    }

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
        await connection.ReadPayloadAsync<TutorialProgressSnapshotPacket>(
            PacketType.TutorialProgressSnapshot);
        await connection.ReadPayloadAsync<InventorySnapshotPacket>(
            PacketType.InventorySnapshot);
        await connection.SendAsync(
            PacketType.EnterWorldRequest,
            new EnterWorldRequestPacket(selected.GameplaySessionId));
        EnterWorldResponsePacket retriedEntry =
            await connection.ReadPayloadAsync<EnterWorldResponsePacket>(
                PacketType.EnterWorldResponse);
        await connection.ReadPayloadAsync<TutorialProgressSnapshotPacket>(
            PacketType.TutorialProgressSnapshot);
        await connection.ReadPayloadAsync<InventorySnapshotPacket>(
            PacketType.InventorySnapshot);
        // A malformed movement payload must not terminate the authenticated
        // transport; the next valid sequenced input still receives a snapshot.
        await connection.SendRawPayloadAsync(
            PacketType.PlayerMoveInput,
            "{");
        await connection.SendAsync(
            PacketType.PlayerMoveInput,
            new PlayerMoveInputPacket(1f, 0f, clientSequence: 1));
        PlayerPositionSnapshotPacket movementSnapshot =
            await connection.ReadPayloadAsync<PlayerPositionSnapshotPacket>(
                PacketType.PlayerPositionSnapshot);
        await connection.SendAsync(
            PacketType.PlayerMoveInput,
            new PlayerMoveInputPacket(0f, 1f, clientSequence: 1));
        PlayerPositionSnapshotPacket duplicateMovementSnapshot =
            await connection.ReadPayloadAsync<PlayerPositionSnapshotPacket>(
                PacketType.PlayerPositionSnapshot);

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
        Assert.True(movementSnapshot.InputAccepted);
        Assert.True(movementSnapshot.ServerSequence > 0);
        Assert.Equal(1, movementSnapshot.AcknowledgedSequence);
        Assert.False(duplicateMovementSnapshot.InputAccepted);
        Assert.True(
            duplicateMovementSnapshot.ServerSequence >
            movementSnapshot.ServerSequence);
        Assert.Equal(
            movementSnapshot.PositionX,
            duplicateMovementSnapshot.PositionX);
        Assert.Equal(
            movementSnapshot.PositionY,
            duplicateMovementSnapshot.PositionY);
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
            suffix,
            database.Options);
    }

    private static CharacterRepository CreateRepository(
        DbContextOptions<KnightDbContext> databaseOptions,
        CharacterOptions options)
    {
        ServerOptions serverOptions = LoadServerOptions();
        return new(
            databaseOptions,
            options,
            CreateCatalog(options),
            new CharacterNamePolicy(),
            new FixedClock(new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)),
            new ConfiguredMapCatalog(serverOptions.MapDefinitions));
    }

    private static InMemoryActiveAccountLeaseStore CreateLeaseStore() =>
        new(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            maximumActiveAccounts: 500);

    private static CharacterOptions LoadCharacterOptions() =>
        LoadServerOptions().Characters;

    private static ServerOptions LoadServerOptions() =>
        ServerOptions.Load(
            Path.Combine(AppContext.BaseDirectory, "serverSettings.json"));

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
        string Suffix,
        DbContextOptions<KnightDbContext> DatabaseOptions);

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
            ServerOptions serverOptions = LoadServerOptions();
            IWorldMovementResolver movementResolver =
                new MonsterCollisionMovementResolver(
                    new MonsterService(),
                    worldOptions,
                    new ConfiguredMapCatalog(serverOptions.MapDefinitions));
            TutorialDefinitionOptions tutorial =
                serverOptions.TutorialDefinitions.Single();
            var tutorialService = new StarterTutorialService(
                context.DatabaseOptions,
                tutorial,
                new ConfiguredExperienceCurve(serverOptions.Progression),
                new ConfiguredMapCatalog(serverOptions.MapDefinitions),
                clock);
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
                        worldOptions,
                        new CharacterStatsPipeline(context.Options),
                        new ConfiguredExperienceCurve(
                            new ProgressionOptions()),
                        clock),
                    new EnterWorldPacketHandler(clock, movementResolver,
                        tutorialService, tutorial),
                    new PlayerMoveInputPacketHandler(
                        clock,
                        movementResolver),
                    new ListNpcsPacketHandler(serverOptions.NpcDefinitions),
                    new InteractNpcPacketHandler(serverOptions.NpcDefinitions,
                        tutorial, tutorialService,
                        new ConfiguredExperienceCurve(serverOptions.Progression),
                        new CharacterStatsPipeline(serverOptions.Characters),
                        clock),
                    new ListPortalsPacketHandler(serverOptions.PortalDefinitions,
                        serverOptions.MapDefinitions),
                    new UsePortalPacketHandler(serverOptions.PortalDefinitions,
                        tutorialService,
                        new ConfiguredMapCatalog(serverOptions.MapDefinitions),
                        clock),
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

        public void TeleportPlayer(string mapId, float x, float y,
            DateTime utcNow) => _server.PlayerSession!.Teleport(
                mapId, "integration-test", new System.Numerics.Vector2(x, y),
                utcNow);

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
