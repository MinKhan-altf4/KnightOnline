using KnightOnline.Server.Accounts;

namespace KnightOnline.Server.Tests.Accounts;

public sealed class AuthenticationRateLimiterTests
{
    [Fact]
    public void RejectsAttemptsPastLimitAndResetsAfterWindow()
    {
        var limiter = new AuthenticationRateLimiter(
            maximumAttempts: 2,
            window: TimeSpan.FromMinutes(1));
        DateTime now = new(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(limiter.TryAcquire("client", now));
        Assert.True(limiter.TryAcquire("client", now));
        Assert.False(limiter.TryAcquire("client", now));
        Assert.True(limiter.TryAcquire(
            "client",
            now.AddMinutes(1)));
    }

    [Fact]
    public void TracksKeysIndependently()
    {
        var limiter = new AuthenticationRateLimiter(
            maximumAttempts: 1,
            window: TimeSpan.FromMinutes(1));
        DateTime now = DateTime.UnixEpoch;

        Assert.True(limiter.TryAcquire("client-a", now));
        Assert.True(limiter.TryAcquire("client-b", now));
        Assert.False(limiter.TryAcquire("client-a", now));
    }

    [Fact]
    public void ExpiredKeys_AreSwept_SoMemoryStaysBounded()
    {
        var limiter = new AuthenticationRateLimiter(
            maximumAttempts: 5,
            window: TimeSpan.FromMinutes(1));
        DateTime now = DateTime.UnixEpoch;

        for (int index = 0; index < 1_000; index++)
            Assert.True(limiter.TryAcquire($"random-device-{index}", now));
        Assert.Equal(1_000, limiter.TrackedKeyCount);

        Assert.True(limiter.TryAcquire("late", now.AddMinutes(1)));

        Assert.Equal(1, limiter.TrackedKeyCount);
    }

    [Fact]
    public void FullTable_RejectsNewKeys_ButKeepsExistingKeysWorking()
    {
        var limiter = new AuthenticationRateLimiter(
            maximumAttempts: 5,
            window: TimeSpan.FromMinutes(1),
            maximumTrackedKeys: 3);
        DateTime now = DateTime.UnixEpoch;

        Assert.True(limiter.TryAcquire("a", now));
        Assert.True(limiter.TryAcquire("b", now));
        Assert.True(limiter.TryAcquire("c", now));

        Assert.False(limiter.TryAcquire("d", now.AddSeconds(1)));
        Assert.True(limiter.TryAcquire("a", now.AddSeconds(1)));
        Assert.True(limiter.TryAcquire("d", now.AddMinutes(1)));
    }

    [Fact]
    public void LimitStillApplies_AfterKeyWasSweptAndRecreated()
    {
        var limiter = new AuthenticationRateLimiter(
            maximumAttempts: 1,
            window: TimeSpan.FromMinutes(1));
        DateTime now = DateTime.UnixEpoch;

        Assert.True(limiter.TryAcquire("account", now));
        Assert.True(limiter.TryAcquire("account", now.AddMinutes(2)));
        Assert.False(limiter.TryAcquire("account", now.AddMinutes(2)));
    }
}
