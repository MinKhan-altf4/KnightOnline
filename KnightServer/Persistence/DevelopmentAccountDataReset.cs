using Microsoft.EntityFrameworkCore;

namespace KnightOnline.Server.Persistence;

public readonly record struct DevelopmentAccountResetResult(
    int AccountsBefore,
    int CharactersBefore,
    int RefreshSessionsBefore,
    int DeletedAccounts,
    int AccountsAfter,
    int CharactersAfter,
    int RefreshSessionsAfter);

public sealed class DevelopmentAccountDataReset(
    DbContextOptions<KnightDbContext> databaseOptions)
{
    public async Task<DevelopmentAccountResetResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = new KnightDbContext(databaseOptions);
        int accountsBefore = await db.Accounts.CountAsync(cancellationToken);
        int charactersBefore = await db.Characters.CountAsync(cancellationToken);
        int refreshSessionsBefore =
            await db.RefreshSessions.CountAsync(cancellationToken);

        // Account is the aggregate root. PostgreSQL foreign keys cascade to
        // characters, refresh sessions, appearances, tutorial progress,
        // progression grants and character-creation request history.
        int deletedAccounts =
            await db.Accounts.ExecuteDeleteAsync(cancellationToken);

        return new DevelopmentAccountResetResult(
            accountsBefore,
            charactersBefore,
            refreshSessionsBefore,
            deletedAccounts,
            await db.Accounts.CountAsync(cancellationToken),
            await db.Characters.CountAsync(cancellationToken),
            await db.RefreshSessions.CountAsync(cancellationToken));
    }
}
