using System.Data;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Persistence.Entities;
using KnightOnline.Server.Progression;
using KnightOnline.Server.Time;
using KnightOnline.Server.World;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace KnightOnline.Server.Tutorials;

public enum TutorialCommandStatus : byte
{
    Applied = 0, AlreadyApplied = 1, NoChange = 2,
    CharacterNotFound = 3, InvalidRequest = 4,
}

public sealed record TutorialCommandResult(
    TutorialCommandStatus Status,
    StarterTutorialOutcome Outcome,
    StarterTutorialSnapshot Progress,
    string MapDefinitionId,
    string SpawnPointId,
    int Level,
    long TotalExperience,
    IReadOnlyList<CharacterInventoryItemEntity> Inventory);

public sealed class StarterTutorialService(
    DbContextOptions<KnightDbContext> databaseOptions,
    TutorialDefinitionOptions definition,
    IExperienceCurve experienceCurve,
    IMapCatalog mapCatalog,
    IServerClock clock)
{
    public async Task<TutorialCommandResult> InteractWithQuestNpcAsync(
        Guid requestId, int characterId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty || characterId <= 0)
            return Invalid();

        await using var db = new KnightDbContext(databaseOptions);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        CharacterEntity? character = await LockCharacterAsync(db, characterId,
            cancellationToken);
        if (character == null)
            return NotFound();

        CharacterTutorialProgressEntity progress = await LoadProgressAsync(
            db, characterId, cancellationToken);
        TutorialCommandEntity? previous = await db.TutorialCommands
            .AsNoTracking().SingleOrDefaultAsync(value =>
                value.RequestId == requestId && value.CharacterId == characterId,
                cancellationToken);
        if (previous != null)
            return await BuildResultAsync(db, character, progress,
                TutorialCommandStatus.AlreadyApplied,
                Enum.Parse<StarterTutorialOutcome>(previous.ResultCode),
                cancellationToken);

        var machine = new StarterTutorialStateMachine(definition);
        StarterTutorialTransition transition = machine.TalkToNpc(
            ToSnapshot(progress), definition.QuestNpcDefinitionId);
        if (transition.Outcome == StarterTutorialOutcome.NoChange)
            return await BuildResultAsync(db, character, progress,
                TutorialCommandStatus.NoChange, transition.Outcome,
                cancellationToken);

        Apply(progress, transition.Snapshot);
        DateTime now = clock.UtcNow;
        progress.StartedAtUtc ??= now;
        progress.UpdatedAtUtc = now;
        progress.Version++;

        if (transition.Outcome == StarterTutorialOutcome.QuestCompleted)
        {
            await ApplyCompletionRewardAsync(db, character, progress,
                requestId, now, cancellationToken);
        }

        db.TutorialCommands.Add(new TutorialCommandEntity
        {
            RequestId = requestId,
            CharacterId = characterId,
            CommandType = "npc_interaction",
            ResultCode = transition.Outcome.ToString(),
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildResultAsync(db, character, progress,
            TutorialCommandStatus.Applied, transition.Outcome,
            cancellationToken);
    }

    public async Task<TutorialCommandResult> RecordKillAsync(
        int characterId, Guid monsterLifeId, int monsterDefinitionId,
        string mapDefinitionId, CancellationToken cancellationToken)
    {
        if (characterId <= 0 || monsterLifeId == Guid.Empty ||
            monsterDefinitionId <= 0 ||
            !string.Equals(mapDefinitionId, definition.QuestMapDefinitionId,
                StringComparison.OrdinalIgnoreCase))
            return Invalid();

        await using var db = new KnightDbContext(databaseOptions);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        CharacterEntity? character = await LockCharacterAsync(db, characterId,
            cancellationToken);
        if (character == null)
            return NotFound();
        CharacterTutorialProgressEntity progress = await LoadProgressAsync(
            db, characterId, cancellationToken);

        bool duplicate = await db.TutorialKillCredits.AnyAsync(value =>
            value.CharacterId == characterId &&
            value.TutorialDefinitionId == definition.DefinitionId &&
            value.MonsterLifeId == monsterLifeId, cancellationToken);
        if (duplicate)
            return await BuildResultAsync(db, character, progress,
                TutorialCommandStatus.AlreadyApplied,
                StarterTutorialOutcome.NoChange, cancellationToken);

        StarterTutorialTransition transition =
            new StarterTutorialStateMachine(definition).RecordMonsterKill(
                ToSnapshot(progress), monsterDefinitionId);
        if (transition.Outcome == StarterTutorialOutcome.NoChange)
            return await BuildResultAsync(db, character, progress,
                TutorialCommandStatus.NoChange, transition.Outcome,
                cancellationToken);

        db.TutorialKillCredits.Add(new TutorialKillCreditEntity
        {
            CharacterId = characterId,
            TutorialDefinitionId = definition.DefinitionId,
            MonsterLifeId = monsterLifeId,
            MonsterDefinitionId = monsterDefinitionId,
            CreditedAtUtc = clock.UtcNow,
        });
        Apply(progress, transition.Snapshot);
        progress.UpdatedAtUtc = clock.UtcNow;
        progress.Version++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildResultAsync(db, character, progress,
            TutorialCommandStatus.Applied, transition.Outcome,
            cancellationToken);
    }

    public async Task PersistPortalTransitionAsync(int characterId,
        string mapDefinitionId, string spawnPointId, float x, float y,
        CancellationToken cancellationToken)
    {
        await using var db = new KnightDbContext(databaseOptions);
        CharacterEntity? character = await db.Characters.SingleOrDefaultAsync(
            value => value.Id == characterId, cancellationToken);
        if (character == null) return;
        character.CurrentMapDefinitionId = mapDefinitionId;
        character.CurrentSpawnPointId = spawnPointId;
        character.PositionX = x;
        character.PositionY = y;
        character.Version++;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsAtStepAsync(int characterId,
        string stepDefinitionId, CancellationToken cancellationToken)
    {
        if (characterId <= 0 || string.IsNullOrWhiteSpace(stepDefinitionId))
            return false;
        await using var db = new KnightDbContext(databaseOptions);
        return await db.CharacterTutorialProgress.AsNoTracking().AnyAsync(
            value => value.CharacterId == characterId &&
                     value.TutorialDefinitionId == definition.DefinitionId &&
                     value.CurrentStepDefinitionId == stepDefinitionId,
            cancellationToken);
    }

    public async Task<TutorialCommandResult> GetCurrentAsync(int characterId,
        CancellationToken cancellationToken)
    {
        await using var db = new KnightDbContext(databaseOptions);
        CharacterEntity? character = await db.Characters.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == characterId,
                cancellationToken);
        if (character == null) return NotFound();
        CharacterTutorialProgressEntity progress = await db
            .CharacterTutorialProgress.AsNoTracking().SingleAsync(value =>
                value.CharacterId == characterId &&
                value.TutorialDefinitionId == definition.DefinitionId,
                cancellationToken);
        return await BuildResultAsync(db, character, progress,
            TutorialCommandStatus.NoChange, StarterTutorialOutcome.NoChange,
            cancellationToken);
    }

    private async Task ApplyCompletionRewardAsync(KnightDbContext db,
        CharacterEntity character, CharacterTutorialProgressEntity progress,
        Guid requestId, DateTime now, CancellationToken cancellationToken)
    {
        long totalAfter = character.TotalExperience + definition.ExperienceReward;
        int levelAfter = experienceCurve.ResolveLevel(totalAfter,
            experienceCurve.MaximumLevel);
        db.CharacterProgressionGrants.Add(new CharacterProgressionGrantEntity
        {
            RequestId = requestId,
            CharacterId = character.Id,
            SourceType = "tutorial_reward",
            SourceId = definition.DefinitionId,
            RequestedExperience = definition.ExperienceReward,
            AppliedExperience = definition.ExperienceReward,
            LevelBefore = character.Level,
            LevelAfter = levelAfter,
            TotalExperienceAfter = totalAfter,
            CreatedAtUtc = now,
        });
        character.Level = levelAfter;
        character.TotalExperience = totalAfter;
        character.Version++;
        progress.CompletedAtUtc = now;
        foreach (string itemId in definition.RewardItemDefinitionIds)
        {
            db.CharacterInventoryItems.Add(new CharacterInventoryItemEntity
            {
                Id = Guid.NewGuid(), CharacterId = character.Id,
                ItemDefinitionId = itemId, Quantity = 1,
                SourceType = "tutorial_reward",
                SourceId = definition.DefinitionId, CreatedAtUtc = now,
            });
        }
        string rewardJson = JsonSerializer.Serialize(new
        {
            TutorialDefinitionId = definition.DefinitionId,
            Experience = definition.ExperienceReward,
            Items = definition.RewardItemDefinitionIds,
            LevelAfter = levelAfter,
            MapAfter = character.CurrentMapDefinitionId,
        });
        Guid eventId = Guid.NewGuid();
        db.GameplayAuditRecords.Add(new GameplayAuditRecordEntity
        {
            EventId = eventId, RequestId = requestId,
            CharacterId = character.Id, Action = "tutorial_reward_granted",
            Reason = definition.DefinitionId, ResultJson = rewardJson,
            OccurredAtUtc = now,
        });
        db.DomainOutboxMessages.Add(new DomainOutboxMessageEntity
        {
            EventId = eventId, CorrelationId = requestId,
            CausationId = requestId, EventType = "TutorialCompleted",
            EventVersion = 1, AggregateType = "Character",
            AggregateId = character.Id.ToString(), PayloadJson = rewardJson,
            OccurredAtUtc = now,
        });
        await Task.CompletedTask;
    }

    private async Task<CharacterTutorialProgressEntity> LoadProgressAsync(
        KnightDbContext db, int characterId, CancellationToken token) =>
        await db.CharacterTutorialProgress.SingleAsync(value =>
            value.CharacterId == characterId &&
            value.TutorialDefinitionId == definition.DefinitionId, token);

    private static Task<CharacterEntity?> LockCharacterAsync(KnightDbContext db,
        int id, CancellationToken token) => db.Characters.FromSqlInterpolated(
            $"SELECT * FROM characters WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(token);

    private async Task<TutorialCommandResult> BuildResultAsync(
        KnightDbContext db, CharacterEntity character,
        CharacterTutorialProgressEntity progress, TutorialCommandStatus status,
        StarterTutorialOutcome outcome, CancellationToken token) =>
        new(status, outcome, ToSnapshot(progress),
            character.CurrentMapDefinitionId, character.CurrentSpawnPointId,
            character.Level, character.TotalExperience,
            await db.CharacterInventoryItems.AsNoTracking()
                .Where(value => value.CharacterId == character.Id)
                .OrderBy(value => value.CreatedAtUtc).ToArrayAsync(token));

    private static StarterTutorialSnapshot ToSnapshot(
        CharacterTutorialProgressEntity progress) =>
        new(progress.State, progress.CurrentStepDefinitionId,
            progress.ObjectiveProgress);
    private static void Apply(CharacterTutorialProgressEntity entity,
        StarterTutorialSnapshot snapshot)
    {
        entity.State = snapshot.State;
        entity.CurrentStepDefinitionId = snapshot.CurrentStepDefinitionId;
        entity.ObjectiveProgress = snapshot.ObjectiveProgress;
    }
    private void ApplyCharacterSpawn(CharacterEntity character, string mapId,
        string spawnPointId)
    {
        if (!mapCatalog.TryResolveSpawn(mapId, spawnPointId,
                out MapSpawnPoint spawn))
            throw new InvalidOperationException($"Missing spawn {mapId}/{spawnPointId}.");
        character.CurrentMapDefinitionId = mapId;
        character.CurrentSpawnPointId = spawnPointId;
        character.PositionX = spawn.Position.X;
        character.PositionY = spawn.Position.Y;
    }
    private TutorialCommandResult Invalid() => new(
        TutorialCommandStatus.InvalidRequest, StarterTutorialOutcome.NoChange,
        new(TutorialState.NotStarted, definition.InitialStepDefinitionId, 0),
        string.Empty, string.Empty, 1, 0, []);
    private TutorialCommandResult NotFound() => Invalid() with
        { Status = TutorialCommandStatus.CharacterNotFound };
}
