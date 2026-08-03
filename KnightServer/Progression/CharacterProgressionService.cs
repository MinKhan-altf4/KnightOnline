using System.Data;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Persistence.Entities;
using KnightOnline.Server.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KnightOnline.Server.Progression;

public enum ProgressionGrantStatus : byte
{
    Applied = 0,
    AlreadyApplied = 1,
    CharacterNotFound = 2,
    InvalidRequest = 3,
}

public sealed record ProgressionGrantResult(
    ProgressionGrantStatus Status,
    Guid RequestId,
    int CharacterId,
    long RequestedExperience,
    long AppliedExperience,
    long TotalExperience,
    int LevelBefore,
    int LevelAfter,
    long ExperienceIntoLevel,
    long ExperienceToNextLevel);

public sealed class CharacterProgressionService(
    DbContextOptions<KnightDbContext> databaseOptions,
    IExperienceCurve curve,
    IServerClock clock)
{
    public async Task<ProgressionGrantResult> GrantExperienceAsync(
        Guid requestId,
        int characterId,
        long requestedExperience,
        int maximumAllowedLevel,
        string sourceType,
        string sourceId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty ||
            characterId <= 0 ||
            requestedExperience <= 0 ||
            string.IsNullOrWhiteSpace(sourceType) ||
            sourceType.Trim().Length > 32 ||
            string.IsNullOrWhiteSpace(sourceId) ||
            sourceId.Trim().Length > 128)
        {
            return Invalid(requestId, characterId, requestedExperience);
        }

        await using var db = new KnightDbContext(databaseOptions);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        CharacterProgressionGrantEntity? existing =
            await db.CharacterProgressionGrants.AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.RequestId == requestId,
                    cancellationToken);
        if (existing != null)
            return ToResult(existing, ProgressionGrantStatus.AlreadyApplied);

        CharacterEntity? character = await db.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM characters WHERE id = {characterId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character == null)
            return new ProgressionGrantResult(
                ProgressionGrantStatus.CharacterNotFound,
                requestId,
                characterId,
                requestedExperience,
                0,
                0,
                1,
                1,
                0,
                curve.GetExperienceRequiredToAdvance(1));

        int levelCap = Math.Clamp(maximumAllowedLevel, 1, curve.MaximumLevel);
        int levelBefore = Math.Clamp(character.Level, 1, levelCap);
        long storedLevelFloor = curve.GetTotalExperienceRequiredForLevel(
            levelBefore);
        long normalizedTotal = Math.Max(
            character.TotalExperience,
            storedLevelFloor);
        long maximumExperience =
            curve.GetTotalExperienceRequiredForLevel(levelCap);
        long remainingCapacity = Math.Max(
            0,
            maximumExperience - normalizedTotal);
        long applied = Math.Min(requestedExperience, remainingCapacity);
        long totalAfter = normalizedTotal + applied;
        int levelAfter = curve.ResolveLevel(totalAfter, levelCap);
        character.TotalExperience = totalAfter;
        character.Level = levelAfter;
        character.Version++;

        var grant = new CharacterProgressionGrantEntity
        {
            RequestId = requestId,
            CharacterId = characterId,
            SourceType = sourceType.Trim(),
            SourceId = sourceId.Trim(),
            RequestedExperience = requestedExperience,
            AppliedExperience = applied,
            LevelBefore = levelBefore,
            LevelAfter = levelAfter,
            TotalExperienceAfter = totalAfter,
            CreatedAtUtc = clock.UtcNow,
        };
        db.CharacterProgressionGrants.Add(grant);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(grant, ProgressionGrantStatus.Applied);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            await using var retryDb = new KnightDbContext(databaseOptions);
            CharacterProgressionGrantEntity stored =
                await retryDb.CharacterProgressionGrants.AsNoTracking()
                    .SingleAsync(
                        value => value.RequestId == requestId,
                        cancellationToken);
            return ToResult(stored, ProgressionGrantStatus.AlreadyApplied);
        }
    }

    private ProgressionGrantResult ToResult(
        CharacterProgressionGrantEntity grant,
        ProgressionGrantStatus status)
    {
        long levelFloor = curve.GetTotalExperienceRequiredForLevel(
            grant.LevelAfter);
        long intoLevel = grant.TotalExperienceAfter - levelFloor;
        long toNext = curve.GetExperienceRequiredToAdvance(grant.LevelAfter);
        return new ProgressionGrantResult(
            status,
            grant.RequestId,
            grant.CharacterId,
            grant.RequestedExperience,
            grant.AppliedExperience,
            grant.TotalExperienceAfter,
            grant.LevelBefore,
            grant.LevelAfter,
            intoLevel,
            toNext);
    }

    private ProgressionGrantResult Invalid(
        Guid requestId,
        int characterId,
        long requestedExperience) =>
        new(
            ProgressionGrantStatus.InvalidRequest,
            requestId,
            characterId,
            requestedExperience,
            0,
            0,
            1,
            1,
            0,
            curve.GetExperienceRequiredToAdvance(1));
}
