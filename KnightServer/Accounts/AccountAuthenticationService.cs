using KnightOnline.Server.Persistence;
using KnightOnline.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnightOnline.Server.Accounts;

public sealed class AccountAuthenticationService(
    DbContextOptions<KnightDbContext> databaseOptions,
    AuthTokenProtector tokens,
    PasswordHasher passwords,
    TimeSpan refreshTokenLifetime)
{
    public async Task<AuthenticationResult> CreateGuestAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        string rawToken = tokens.CreateToken();
        var account = new AccountEntity
        {
            AccountKey = $"guest:{Guid.NewGuid():N}",
            Kind = AccountKind.Guest,
            CreatedAtUtc = now,
        };
        account.RefreshSessions.Add(CreateSession(
            account,
            rawToken,
            deviceId,
            now));

        await using var db = new KnightDbContext(databaseOptions);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return AuthenticationResult.Success(ToAuthenticated(
            account,
            rawToken,
            now));
    }

    public async Task<AuthenticationResult> ResumeAsync(
        string refreshToken,
        string deviceId,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        string tokenHash = tokens.Hash(refreshToken);
        string deviceHash = tokens.Hash(deviceId);

        await using var db = new KnightDbContext(databaseOptions);
        RefreshSessionEntity? current = await db.RefreshSessions
            .Include(session => session.Account)
            .SingleOrDefaultAsync(
                session =>
                    session.TokenHash == tokenHash &&
                    session.DeviceIdHash == deviceHash,
                cancellationToken);

        if (current == null ||
            current.RevokedAtUtc != null ||
            current.ExpiresAtUtc <= now)
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidOrExpiredToken);

        current.RevokedAtUtc = now;
        string replacementToken = tokens.CreateToken();
        db.RefreshSessions.Add(CreateSession(
            current.Account,
            replacementToken,
            deviceId,
            now));
        await db.SaveChangesAsync(cancellationToken);

        return AuthenticationResult.Success(ToAuthenticated(
            current.Account,
            replacementToken,
            now));
    }

    public async Task<AuthenticationResult> LoginAsync(
        string username,
        string password,
        string deviceId,
        string? guestRefreshToken,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        string normalizedUsername = NormalizeUsername(username);
        string deviceHash = tokens.Hash(deviceId);

        await using var db = new KnightDbContext(databaseOptions);
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        AccountEntity? account = await db.Accounts.SingleOrDefaultAsync(
            candidate =>
                candidate.Kind == AccountKind.Registered &&
                candidate.NormalizedUsername == normalizedUsername,
            cancellationToken);

        if (account?.PasswordHash == null ||
            !passwords.Verify(password, account.PasswordHash))
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidCredentials);

        // A device remembers one account only. Revoke every older token issued
        // to this device before issuing the replacement.
        List<RefreshSessionEntity> deviceSessions =
            await db.RefreshSessions
                .Where(session =>
                    session.DeviceIdHash == deviceHash &&
                    session.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);
        foreach (RefreshSessionEntity session in deviceSessions)
            session.RevokedAtUtc = now;

        if (!string.IsNullOrWhiteSpace(guestRefreshToken))
        {
            string guestTokenHash = tokens.Hash(guestRefreshToken);
            AccountEntity? guest = await db.Accounts.SingleOrDefaultAsync(
                candidate =>
                    candidate.Kind == AccountKind.Guest &&
                    candidate.RefreshSessions.Any(session =>
                        session.TokenHash == guestTokenHash &&
                        session.DeviceIdHash == deviceHash &&
                        session.RevokedAtUtc == null &&
                        session.ExpiresAtUtc > now),
                cancellationToken);
            if (guest != null)
                db.Accounts.Remove(guest);
        }

        string rawToken = tokens.CreateToken();
        db.RefreshSessions.Add(CreateSession(
            account,
            rawToken,
            deviceId,
            now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AuthenticationResult.Success(ToAuthenticated(
            account,
            rawToken,
            now));
    }

    public async Task<AuthenticationResult> ConvertGuestAsync(
        string guestRefreshToken,
        string username,
        string password,
        string deviceId,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        string tokenHash = tokens.Hash(guestRefreshToken);
        string normalizedUsername = NormalizeUsername(username);

        await using var db = new KnightDbContext(databaseOptions);
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        RefreshSessionEntity? session = await db.RefreshSessions
            .Include(candidate => candidate.Account)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.TokenHash == tokenHash &&
                    candidate.RevokedAtUtc == null &&
                    candidate.ExpiresAtUtc > now,
                cancellationToken);

        if (session?.Account.Kind != AccountKind.Guest)
            return AuthenticationResult.Failed(
                AuthenticationFailure.GuestNotFound);

        bool usernameTaken = await db.Accounts.AnyAsync(
            candidate =>
                candidate.NormalizedUsername == normalizedUsername,
            cancellationToken);
        if (usernameTaken)
            return AuthenticationResult.Failed(
                AuthenticationFailure.UsernameUnavailable);

        AccountEntity account = session.Account;
        account.Kind = AccountKind.Registered;
        account.Username = username.Trim();
        account.NormalizedUsername = normalizedUsername;
        account.PasswordHash = passwords.Hash(password);
        account.RegisteredAtUtc = now;
        session.RevokedAtUtc = now;

        string replacementToken = tokens.CreateToken();
        db.RefreshSessions.Add(CreateSession(
            account,
            replacementToken,
            deviceId,
            now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AuthenticationResult.Success(ToAuthenticated(
            account,
            replacementToken,
            now));
    }

    private RefreshSessionEntity CreateSession(
        AccountEntity account,
        string rawToken,
        string deviceId,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Account = account,
            TokenHash = tokens.Hash(rawToken),
            DeviceIdHash = tokens.Hash(deviceId),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(refreshTokenLifetime),
        };

    private AuthenticatedAccount ToAuthenticated(
        AccountEntity account,
        string rawToken,
        DateTime now) =>
        new(
            account.AccountKey,
            account.Kind == AccountKind.Guest,
            rawToken,
            now.Add(refreshTokenLifetime));

    private static string NormalizeUsername(string username) =>
        username.Trim().ToUpperInvariant();
}
