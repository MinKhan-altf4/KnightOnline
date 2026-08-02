using KnightOnline.Server.Accounts;
using KnightOnline.Server.Persistence;
using KnightOnline.Server.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnightServer.IntegrationTests.Accounts;

public sealed class PostgresAuthenticationIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task GuestConversion_LoginAndRotation_PersistCorrectly()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var clock = new MutableClock(DateTime.UtcNow);
        var service = CreateService(database.Options, clock);
        string suffix = Guid.NewGuid().ToString("N")[..12];
        string username = $"it_{suffix}";
        const string password = "Integration-Test-Password-2026";
        string deviceA = $"integration-device-a-{suffix}";
        string deviceB = $"integration-device-b-{suffix}";

        AuthenticationResult guest = await service.CreateGuestAsync(
            deviceA,
            CancellationToken.None);
        Assert.True(guest.IsSuccess);
        database.TrackAccount(guest.Account!.AccountKey);

        AuthenticationResult converted = await service.ConvertGuestAsync(
            guest.Account.RefreshToken,
            username,
            password,
            deviceA,
            CancellationToken.None);
        Assert.True(converted.IsSuccess);
        Assert.False(converted.Account!.IsGuest);

        AuthenticationResult login = await service.LoginAsync(
            username,
            password,
            deviceB,
            guestRefreshToken: null,
            CancellationToken.None);
        Assert.True(login.IsSuccess);

        AuthenticationResult rotated = await service.ResumeAsync(
            login.Account!.RefreshToken,
            deviceB,
            CancellationToken.None);
        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(
            login.Account.RefreshToken,
            rotated.Account!.RefreshToken);

        AuthenticationResult replay = await service.ResumeAsync(
            login.Account.RefreshToken,
            deviceB,
            CancellationToken.None);
        Assert.False(replay.IsSuccess);
        Assert.Equal(
            AuthenticationFailure.InvalidOrExpiredToken,
            replay.Failure);

        AuthenticationResult familyRevoked = await service.ResumeAsync(
            rotated.Account.RefreshToken,
            deviceB,
            CancellationToken.None);
        Assert.False(familyRevoked.IsSuccess);
        Assert.Equal(
            AuthenticationFailure.InvalidOrExpiredToken,
            familyRevoked.Failure);
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task TwoDeviceLogin_StillAllowsOnlyOneActiveLease()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var clock = new MutableClock(DateTime.UtcNow);
        var service = CreateService(database.Options, clock);
        var leases = new InMemoryActiveAccountLeaseStore(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            maximumActiveAccounts: 500);
        string suffix = Guid.NewGuid().ToString("N")[..12];
        string username = $"it_{suffix}";
        const string password = "Integration-Test-Password-2026";
        string deviceA = $"integration-device-a-{suffix}";
        string deviceB = $"integration-device-b-{suffix}";

        AuthenticationResult guest = await service.CreateGuestAsync(
            deviceA,
            CancellationToken.None);
        Assert.True(guest.IsSuccess);
        database.TrackAccount(guest.Account!.AccountKey);
        AuthenticationResult registered = await service.ConvertGuestAsync(
            guest.Account.RefreshToken,
            username,
            password,
            deviceA,
            CancellationToken.None);
        Assert.True(registered.IsSuccess);

        AuthenticationResult loginA = await service.LoginAsync(
            username,
            password,
            deviceA,
            null,
            CancellationToken.None);
        AuthenticationResult loginB = await service.LoginAsync(
            username,
            password,
            deviceB,
            null,
            CancellationToken.None);
        Assert.True(loginA.IsSuccess);
        Assert.True(loginB.IsSuccess);

        ActiveAccountLeaseClaim claimA = await leases.TryClaimAsync(
            loginA.Account!.AccountKey,
            Guid.NewGuid(),
            clock.UtcNow);
        ActiveAccountLeaseClaim claimB = await leases.TryClaimAsync(
            loginB.Account!.AccountKey,
            Guid.NewGuid(),
            clock.UtcNow);

        Assert.Equal(ActiveAccountLeaseClaimStatus.Acquired, claimA.Status);
        Assert.Equal(
            ActiveAccountLeaseClaimStatus.ActiveElsewhere,
            claimB.Status);
    }

    private static AccountAuthenticationService CreateService(
        DbContextOptions<KnightDbContext> options,
        IServerClock clock) =>
        new(
            options,
            new AuthTokenProtector(),
            new PasswordHasher(),
            TimeSpan.FromDays(30),
            clock);

    private sealed class MutableClock(DateTime utcNow) : IServerClock
    {
        public DateTime UtcNow { get; } = utcNow.ToUniversalTime();
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
