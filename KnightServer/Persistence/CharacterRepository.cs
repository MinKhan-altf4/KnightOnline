using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KnightOnline.Server.Persistence;

public sealed class CharacterRepository(
    DbContextOptions<KnightDbContext> options,
    int maximumCharactersPerAccount,
    int initialLevel)
{
    public async Task EnsureAccountExistsAsync(string accountKey)
    {
        await using var db = new KnightDbContext(options);
        AccountEntity? existing = await db.Accounts.SingleOrDefaultAsync(
            account => account.AccountKey == accountKey);
        if (existing != null)
        {
            if (existing.Kind != AccountKind.Development)
            {
                existing.Kind = AccountKind.Development;
                await db.SaveChangesAsync();
            }
            return;
        }

        db.Accounts.Add(new AccountEntity
        {
            AccountKey = accountKey,
            Kind = AccountKind.Development,
            CreatedAtUtc = DateTime.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            // Một connection khác đã tạo development account trước.
        }
    }

    public async Task<IReadOnlyList<CharacterSummaryPacket>> ListAsync(
        string accountKey)
    {
        await using var db = new KnightDbContext(options);

        return await db.Characters
            .AsNoTracking()
            .Where(character => character.Account.AccountKey == accountKey)
            .OrderBy(character => character.Id)
            .Select(character => new CharacterSummaryPacket(
                character.Name,
                character.Id,
                character.Level))
            .ToArrayAsync();
    }

    public async Task<CharacterSummaryPacket?> FindOwnedAsync(
        string accountKey,
        int characterId)
    {
        await using var db = new KnightDbContext(options);

        return await db.Characters
            .AsNoTracking()
            .Where(character =>
                character.Id == characterId &&
                character.Account.AccountKey == accountKey)
            .Select(character => new CharacterSummaryPacket(
                character.Name,
                character.Id,
                character.Level))
            .SingleOrDefaultAsync();
    }

    public async Task<CreateCharacterResponsePacket> CreateAsync(
        string accountKey,
        string name)
    {
        string normalizedName = name.ToUpperInvariant();

        await using var db = new KnightDbContext(options);
        AccountEntity account = await db.Accounts
            .SingleAsync(existingAccount => existingAccount.AccountKey == accountKey);

        int characterCount = await db.Characters
            .CountAsync(character => character.AccountId == account.Id);

        if (characterCount >= maximumCharactersPerAccount)
        {
            return new CreateCharacterResponsePacket(
                CreateCharacterResult.CharacterLimitReached,
                $"An account can have at most {maximumCharactersPerAccount} characters.");
        }

        db.Characters.Add(new CharacterEntity
        {
            AccountId = account.Id,
            Name = name,
            NormalizedName = normalizedName,
            Level = initialLevel,
            CreatedAtUtc = DateTime.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync();
            return new CreateCharacterResponsePacket(
                CreateCharacterResult.Success,
                name);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            return new CreateCharacterResponsePacket(
                CreateCharacterResult.NameAlreadyTaken,
                "That character name already exists.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        };
    }
}
