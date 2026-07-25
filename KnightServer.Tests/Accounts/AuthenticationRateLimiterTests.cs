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
}
