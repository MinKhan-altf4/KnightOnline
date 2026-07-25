using KnightOnline.Server.Accounts;
using KnightOnline.Server.Time;

namespace KnightOnline.Server.Tests.Accounts;

public sealed class RegistrationFlowTests
{
    [Fact]
    public async Task BeginAsync_CreatesExpiringPkceTransaction()
    {
        var clock = new FixedClock(
            new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        string verifier = new('a', 43);

        RegistrationStartResult result = await service.BeginAsync(
            Guid.NewGuid(),
            new AuthTokenProtector().CreateToken(),
            new string('d', 16),
            RegistrationFlowService.CreatePkceChallenge(verifier),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.DevelopmentAuthorizationCode);
        Assert.Equal(clock.UtcNow.AddMinutes(15), result.ExpiresAtUtc);
        Assert.Contains(
            result.TransactionId!.Value.ToString("D"),
            result.RegistrationUrl);
    }

    [Fact]
    public async Task BeginAsync_RejectsDuplicateRequestId()
    {
        var clock = new FixedClock(DateTime.UtcNow);
        var service = CreateService(clock);
        Guid requestId = Guid.NewGuid();
        string guestToken = new AuthTokenProtector().CreateToken();
        string challenge =
            RegistrationFlowService.CreatePkceChallenge(new string('v', 43));

        await service.BeginAsync(
            requestId,
            guestToken,
            new string('d', 16),
            challenge,
            CancellationToken.None);
        RegistrationStartResult duplicate = await service.BeginAsync(
            requestId,
            guestToken,
            new string('d', 16),
            challenge,
            CancellationToken.None);

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(
            RegistrationStartFailure.DuplicateRequest,
            duplicate.Failure);
    }

    [Fact]
    public async Task Store_InvalidPkceDoesNotConsumeTransaction()
    {
        var protector = new AuthTokenProtector();
        var store = new InMemoryRegistrationTransactionStore();
        DateTime now = DateTime.UtcNow;
        string code = protector.CreateToken();
        var transaction = new RegistrationTransaction
        {
            Id = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            GuestCredentialHandle = protector.CreateToken(),
            DeviceId = new string('d', 16),
            PkceChallenge = "expected",
            AuthorizationCodeHash = protector.Hash(code),
            ExpiresAtUtc = now.AddMinutes(15),
        };
        await store.AddAsync(transaction, CancellationToken.None);

        RegistrationTransaction? rejected = await store.TryConsumeAsync(
            transaction.Id,
            protector.Hash(code),
            "wrong",
            now,
            CancellationToken.None);
        RegistrationTransaction? accepted = await store.TryConsumeAsync(
            transaction.Id,
            protector.Hash(code),
            "expected",
            now,
            CancellationToken.None);

        Assert.Null(rejected);
        Assert.NotNull(accepted);
        Assert.NotNull(accepted.ConsumedAtUtc);
    }

    private static RegistrationFlowService CreateService(
        IServerClock clock) =>
        new(
            new InMemoryRegistrationTransactionStore(),
            new DevelopmentRegistrationPortal(
                "http://127.0.0.1:8080/register"),
            null!,
            new AuthTokenProtector(),
            TimeSpan.FromMinutes(15),
            clock);

    private sealed class FixedClock(DateTime utcNow) : IServerClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
