using KnightOnline.Server.Accounts;

namespace KnightServer.Tests.Accounts;

public sealed class AccountSessionRegistryTests
{
    private static readonly DateTime Now =
        new(2026, 8, 2, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Claim_PreservesLiveOwner_WhenAnotherConnectionArrives()
    {
        var store = CreateStore();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        ActiveAccountLeaseClaim firstClaim =
            await store.TryClaimAsync("account-1", first, Now);
        ActiveAccountLeaseClaim secondClaim =
            await store.TryClaimAsync("account-1", second, Now);

        Assert.Equal(ActiveAccountLeaseClaimStatus.Acquired, firstClaim.Status);
        Assert.NotEqual(Guid.Empty, firstClaim.Generation);
        Assert.Equal(
            ActiveAccountLeaseClaimStatus.ActiveElsewhere,
            secondClaim.Status);
        Assert.True(await store.IsOwnerAsync(
            "account-1", first, firstClaim.Generation, Now));
    }

    [Fact]
    public async Task Claim_ReplacesExpiredLease_WithNewGeneration()
    {
        var store = CreateStore();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        ActiveAccountLeaseClaim firstClaim =
            await store.TryClaimAsync("account-1", first, Now);

        ActiveAccountLeaseClaim secondClaim =
            await store.TryClaimAsync(
                "account-1",
                second,
                Now.AddSeconds(21));

        Assert.Equal(ActiveAccountLeaseClaimStatus.Acquired, secondClaim.Status);
        Assert.NotEqual(firstClaim.Generation, secondClaim.Generation);
        Assert.True(await store.IsOwnerAsync(
            "account-1",
            second,
            secondClaim.Generation,
            Now.AddSeconds(21)));
    }

    [Fact]
    public async Task Heartbeat_RenewsOnlyMatchingGeneration()
    {
        var store = CreateStore();
        Guid connection = Guid.NewGuid();
        ActiveAccountLeaseClaim claim =
            await store.TryClaimAsync("account-1", connection, Now);

        ActiveAccountLeaseRenewal stale = await store.RenewAsync(
            "account-1",
            connection,
            Guid.NewGuid(),
            Now.AddSeconds(5));
        ActiveAccountLeaseRenewal valid = await store.RenewAsync(
            "account-1",
            connection,
            claim.Generation,
            Now.AddSeconds(5));

        Assert.False(stale.Renewed);
        Assert.True(valid.Renewed);
        Assert.Equal(Now.AddSeconds(25), valid.ExpiresAtUtc);
    }

    [Fact]
    public async Task Heartbeat_RemainsOwnedAcrossTenMinutesOfRenewals()
    {
        var store = CreateStore();
        Guid connection = Guid.NewGuid();
        ActiveAccountLeaseClaim claim =
            await store.TryClaimAsync("account-1", connection, Now);

        DateTime heartbeatUtc = Now;
        for (int index = 0; index < 120; index++)
        {
            heartbeatUtc = heartbeatUtc.AddSeconds(5);
            ActiveAccountLeaseRenewal renewal = await store.RenewAsync(
                "account-1",
                connection,
                claim.Generation,
                heartbeatUtc);

            Assert.True(renewal.Renewed);
            Assert.Equal(
                heartbeatUtc.AddSeconds(20),
                renewal.ExpiresAtUtc);
        }

        Assert.True(await store.IsOwnerAsync(
            "account-1",
            connection,
            claim.Generation,
            heartbeatUtc));
    }

    [Fact]
    public async Task DisconnectGrace_ShortensLease_ThenAllowsReplacement()
    {
        var store = CreateStore();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        ActiveAccountLeaseClaim claim =
            await store.TryClaimAsync("account-1", first, Now);

        Assert.True(await store.BeginDisconnectGraceAsync(
            "account-1",
            first,
            claim.Generation,
            Now.AddSeconds(2)));
        ActiveAccountLeaseClaim duringGrace = await store.TryClaimAsync(
            "account-1",
            second,
            Now.AddSeconds(11));
        ActiveAccountLeaseClaim afterGrace = await store.TryClaimAsync(
            "account-1",
            second,
            Now.AddSeconds(13));

        Assert.Equal(
            ActiveAccountLeaseClaimStatus.ActiveElsewhere,
            duringGrace.Status);
        Assert.Equal(ActiveAccountLeaseClaimStatus.Acquired, afterGrace.Status);
    }

    [Fact]
    public async Task StaleCleanup_CannotReleaseReplacementLease()
    {
        var store = CreateStore();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        ActiveAccountLeaseClaim firstClaim =
            await store.TryClaimAsync("account-1", first, Now);
        ActiveAccountLeaseClaim secondClaim =
            await store.TryClaimAsync(
                "account-1",
                second,
                Now.AddSeconds(21));

        bool staleRelease = await store.ReleaseAsync(
            "account-1",
            first,
            firstClaim.Generation);

        Assert.False(staleRelease);
        Assert.True(await store.IsOwnerAsync(
            "account-1",
            second,
            secondClaim.Generation,
            Now.AddSeconds(21)));
    }

    [Fact]
    public async Task ConcurrentClaims_ProduceExactlyOneOwner()
    {
        var store = CreateStore();
        Guid[] connections = Enumerable.Range(0, 20)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        ActiveAccountLeaseClaim[] claims = await Task.WhenAll(
            connections.Select(connection =>
                store.TryClaimAsync("account-1", connection, Now)
                    .AsTask()));

        Assert.Single(
            claims,
            claim =>
                claim.Status == ActiveAccountLeaseClaimStatus.Acquired);
        Assert.Equal(
            connections.Length - 1,
            claims.Count(claim =>
                claim.Status ==
                ActiveAccountLeaseClaimStatus.ActiveElsewhere));
    }

    [Fact]
    public async Task Capacity_AllowsExactly500ActiveAccounts()
    {
        var store = new InMemoryActiveAccountLeaseStore(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            maximumActiveAccounts: 500);

        ActiveAccountLeaseClaim[] claims = await Task.WhenAll(
            Enumerable.Range(1, 501).Select(index =>
                store.TryClaimAsync(
                        $"account-{index}",
                        Guid.NewGuid(),
                        Now)
                    .AsTask()));
        ActiveAccountLeaseCapacitySnapshot capacity =
            await store.GetCapacityAsync(Now);

        Assert.Equal(
            500,
            claims.Count(claim =>
                claim.Status == ActiveAccountLeaseClaimStatus.Acquired));
        Assert.Single(
            claims,
            claim =>
                claim.Status ==
                ActiveAccountLeaseClaimStatus.CapacityReached);
        Assert.Equal(500, capacity.ActiveCount);
        Assert.Equal(500, capacity.MaximumActiveAccounts);
    }

    [Fact]
    public async Task Capacity_ReclaimsExpiredLeaseBeforeRejecting()
    {
        var store = new InMemoryActiveAccountLeaseStore(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            maximumActiveAccounts: 1);
        await store.TryClaimAsync("account-1", Guid.NewGuid(), Now);

        ActiveAccountLeaseClaim replacement = await store.TryClaimAsync(
            "account-2",
            Guid.NewGuid(),
            Now.AddSeconds(21));

        Assert.Equal(
            ActiveAccountLeaseClaimStatus.Acquired,
            replacement.Status);
    }

    private static InMemoryActiveAccountLeaseStore CreateStore() =>
        new(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            maximumActiveAccounts: 500);
}
