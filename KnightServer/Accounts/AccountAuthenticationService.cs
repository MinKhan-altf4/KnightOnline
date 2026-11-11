using KnightOnline.Server.Persistence;
using KnightOnline.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using KnightOnline.Server.Time;
using Npgsql;
using System.Text.Json;

namespace KnightOnline.Server.Accounts;

public sealed class AccountAuthenticationService(
    DbContextOptions<KnightDbContext> databaseOptions,
    AuthTokenProtector tokens,
    PasswordHasher passwords,
    TimeSpan refreshTokenLifetime,
    TimeSpan authenticationReceiptLifetime,
    IServerClock clock)
{
    private readonly string _dummyPasswordHash =
        passwords.Hash("KnightOnline-Dummy-Password-Verification");

    public async Task<AuthenticationResult> CreateGuestAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationInputPolicy.IsValidDeviceId(deviceId))
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidRequest);

        DateTime now = clock.UtcNow;
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
        Guid requestId,
        string refreshToken,
        string deviceId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty ||
            !AuthenticationInputPolicy.IsValidRefreshToken(refreshToken) ||
            !AuthenticationInputPolicy.IsValidDeviceId(deviceId))
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidRequest);

        DateTime now = clock.UtcNow;
        string tokenHash = tokens.Hash(refreshToken);
        string deviceHash = tokens.Hash(deviceId);

        await using var db = new KnightDbContext(databaseOptions);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockRequestAsync(db, requestId, cancellationToken);
        string fingerprintInput = $"resume|{deviceId}|{refreshToken}";
        await DeleteExpiredReceiptsAsync(db, now, cancellationToken);
        AuthenticationResult? replay = await FindReceiptAsync(
            db, requestId, "resume", fingerprintInput, now,
            cancellationToken);
        if (replay != null)
            return replay;
        RefreshSessionEntity? current = await db.RefreshSessions
            .Include(session => session.Account)
            .SingleOrDefaultAsync(
                session =>
                    session.TokenHash == tokenHash &&
                    session.DeviceIdHash == deviceHash,
                cancellationToken);

        if (current == null)
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidOrExpiredToken);

        if (current.RevokedAtUtc != null)
        {
            await RevokeTokenFamilyAsync(
                db,
                current.FamilyId,
                now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            Console.WriteLine(
                "[Security][Warning] Refresh-token reuse detected; " +
                "the token family was revoked.");
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidOrExpiredToken);
        }

        if (current.ExpiresAtUtc <= now)
        {
            current.RevokedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidOrExpiredToken);
        }

        int claimed = await db.RefreshSessions
            .Where(session =>
                session.Id == current.Id &&
                session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    session => session.RevokedAtUtc,
                    now),
                cancellationToken);
        if (claimed != 1)
        {
            await RevokeTokenFamilyAsync(
                db,
                current.FamilyId,
                now,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidOrExpiredToken);
        }

        string replacementToken = tokens.CreateToken();
        RefreshSessionEntity replacement = CreateSession(
            current.Account,
            replacementToken,
            deviceId,
            now,
            current.FamilyId);
        current.ReplacedBySessionId = replacement.Id;
        db.RefreshSessions.Add(replacement);
        AuthenticationResult result = AuthenticationResult.Success(ToAuthenticated(
            current.Account,
            replacementToken,
            now));
        db.AuthenticationRequestReceipts.Add(CreateReceipt(
            requestId, "resume", fingerprintInput, result, now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AuthenticationResult> LoginAsync(
        Guid requestId,
        string username,
        string password,
        string deviceId,
        string? guestRefreshToken,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty ||
            !AuthenticationInputPolicy.IsValidLoginUsername(username) ||
            !AuthenticationInputPolicy.IsValidLoginPassword(password) ||
            !AuthenticationInputPolicy.IsValidDeviceId(deviceId) ||
            (!string.IsNullOrEmpty(guestRefreshToken) &&
             !AuthenticationInputPolicy.IsValidRefreshToken(
                 guestRefreshToken)))
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidRequest);

        DateTime now = clock.UtcNow;
        string normalizedUsername = NormalizeUsername(username);
        string deviceHash = tokens.Hash(deviceId);

        await using var db = new KnightDbContext(databaseOptions);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockRequestAsync(db, requestId, cancellationToken);
        string fingerprintInput =
            $"login|{NormalizeUsername(username)}|{password}|{deviceId}|" +
            $"{guestRefreshToken}";
        await DeleteExpiredReceiptsAsync(db, now, cancellationToken);
        AuthenticationResult? replay = await FindReceiptAsync(
            db, requestId, "login", fingerprintInput, now,
            cancellationToken);
        if (replay != null)
            return replay;

        AccountEntity? account = await db.Accounts.SingleOrDefaultAsync(
            candidate =>
                candidate.Kind == AccountKind.Registered &&
                candidate.NormalizedUsername == normalizedUsername,
            cancellationToken);

        string passwordHash = account?.PasswordHash ?? _dummyPasswordHash;
        bool passwordValid = passwords.Verify(password, passwordHash);
        if (account == null || !passwordValid)
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
            RefreshSessionEntity? guestSession =
                await db.RefreshSessions
                    .Include(session => session.Account)
                    .SingleOrDefaultAsync(
                        session =>
                            session.TokenHash == guestTokenHash &&
                            session.DeviceIdHash == deviceHash &&
                            session.RevokedAtUtc == null &&
                            session.ExpiresAtUtc > now &&
                            session.Account.Kind == AccountKind.Guest,
                        cancellationToken);
            if (guestSession != null)
            {
                int guestClaimed = await db.RefreshSessions
                    .Where(session =>
                        session.Id == guestSession.Id &&
                        session.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(
                        updates => updates.SetProperty(
                            session => session.RevokedAtUtc,
                            now),
                        cancellationToken);
                if (guestClaimed == 1)
                    db.Accounts.Remove(guestSession.Account);
            }
        }

        string rawToken = tokens.CreateToken();
        db.RefreshSessions.Add(CreateSession(
            account,
            rawToken,
            deviceId,
            now));
        AuthenticationResult result = AuthenticationResult.Success(ToAuthenticated(
            account,
            rawToken,
            now));
        db.AuthenticationRequestReceipts.Add(CreateReceipt(
            requestId, "login", fingerprintInput, result, now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AuthenticationResult> ConvertGuestAsync(
        string guestRefreshToken,
        string username,
        string password,
        string deviceId,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationInputPolicy.IsValidRefreshToken(
                guestRefreshToken) ||
            !AuthenticationInputPolicy.IsValidLoginUsername(username) ||
            !AuthenticationInputPolicy.IsValidRegistrationPassword(password) ||
            !AuthenticationInputPolicy.IsValidDeviceId(deviceId))
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidRequest);

        DateTime now = clock.UtcNow;
        string tokenHash = tokens.Hash(guestRefreshToken);
        string normalizedUsername = NormalizeUsername(username);
        string passwordHash = passwords.Hash(password);

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

        int claimed = await db.RefreshSessions
            .Where(candidate =>
                candidate.Id == session.Id &&
                candidate.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    candidate => candidate.RevokedAtUtc,
                    now),
                cancellationToken);
        if (claimed != 1)
            return AuthenticationResult.Failed(
                AuthenticationFailure.GuestNotFound);

        AccountEntity account = session.Account;
        account.Kind = AccountKind.Registered;
        account.Username = username.Trim();
        account.NormalizedUsername = normalizedUsername;
        account.PasswordHash = passwordHash;
        account.RegisteredAtUtc = now;

        string replacementToken = tokens.CreateToken();
        db.RefreshSessions.Add(CreateSession(
            account,
            replacementToken,
            deviceId,
            now,
            session.FamilyId));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AuthenticationResult.Failed(
                AuthenticationFailure.UsernameUnavailable);
        }

        return AuthenticationResult.Success(ToAuthenticated(
            account,
            replacementToken,
            now));
    }

    private RefreshSessionEntity CreateSession(
        AccountEntity account,
        string rawToken,
        string deviceId,
        DateTime now,
        Guid? familyId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId ?? Guid.NewGuid(),
            Account = account,
            TokenHash = tokens.Hash(rawToken),
            DeviceIdHash = tokens.Hash(deviceId),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(refreshTokenLifetime),
        };

    private async Task<AuthenticationResult?> FindReceiptAsync(
        KnightDbContext db,
        Guid requestId,
        string operation,
        string fingerprintInput,
        DateTime now,
        CancellationToken cancellationToken)
    {
        AuthenticationRequestReceiptEntity? receipt =
            await db.AuthenticationRequestReceipts
                .SingleOrDefaultAsync(value => value.RequestId == requestId,
                    cancellationToken);
        if (receipt == null)
            return null;
        if (receipt.ExpiresAtUtc <= now)
            return null;
        string fingerprint = tokens.Fingerprint(
            receipt.KeyId, fingerprintInput);
        if (!string.Equals(receipt.Operation, operation,
                StringComparison.Ordinal) ||
            !string.Equals(receipt.RequestFingerprint, fingerprint,
                StringComparison.Ordinal))
            return AuthenticationResult.Failed(
                AuthenticationFailure.InvalidRequest);

        return JsonSerializer.Deserialize<AuthenticationResult>(
            tokens.Unprotect(receipt.KeyId, receipt.ProtectedResult))
            ?? throw new InvalidDataException(
                "Stored authentication request receipt is invalid.");
    }

    private AuthenticationRequestReceiptEntity CreateReceipt(
        Guid requestId,
        string operation,
        string fingerprintInput,
        AuthenticationResult result,
        DateTime now) => new()
    {
        RequestId = requestId,
        KeyId = tokens.ActiveKeyId,
        Operation = operation,
        RequestFingerprint = tokens.Fingerprint(fingerprintInput),
        ProtectedResult = tokens.Protect(JsonSerializer.Serialize(result)),
        CreatedAtUtc = now,
        ExpiresAtUtc = now.Add(authenticationReceiptLifetime),
    };

    public async Task DeleteExpiredReceiptsAsync(
        CancellationToken cancellationToken)
    {
        await using var db = new KnightDbContext(databaseOptions);
        await DeleteExpiredReceiptsAsync(db, clock.UtcNow, cancellationToken);
    }

    private static Task<int> DeleteExpiredReceiptsAsync(
        KnightDbContext db,
        DateTime now,
        CancellationToken cancellationToken) =>
        db.AuthenticationRequestReceipts
            .Where(value => value.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);

    private static Task<int> LockRequestAsync(
        KnightDbContext db,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        long lockKey = BitConverter.ToInt64(requestId.ToByteArray());
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
    }

    private static async Task RevokeTokenFamilyAsync(
        KnightDbContext db,
        Guid familyId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await db.RefreshSessions
            .Where(session =>
                session.FamilyId == familyId &&
                session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    session => session.RevokedAtUtc,
                    now),
                cancellationToken);
    }

    private AuthenticatedAccount ToAuthenticated(
        AccountEntity account,
        string rawToken,
        DateTime now) =>
        new(
            account.AccountKey,
            account.Username ?? account.AccountKey,
            account.Kind == AccountKind.Guest,
            rawToken,
            now.Add(refreshTokenLifetime));

    private static string NormalizeUsername(string username) =>
        username.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        };
}
