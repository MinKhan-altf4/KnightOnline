using KnightOnline.Server.Accounts;

namespace KnightServer.Tests.Accounts;

public sealed class AccountSessionRegistryTests
{
    [Fact]
    public async Task TryClaimAsync_PreservesCurrentOwner_WhenAnotherArrives()
    {
        var store = new InMemoryActiveAccountLeaseStore();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        ActiveAccountLeaseClaimResult firstResult =
            await store.TryClaimAsync("account-1", first);
        ActiveAccountLeaseClaimResult secondResult =
            await store.TryClaimAsync("account-1", second);

        Assert.Equal(ActiveAccountLeaseClaimResult.Acquired, firstResult);
        Assert.Equal(
            ActiveAccountLeaseClaimResult.ActiveElsewhere,
            secondResult);
        Assert.True(await store.IsOwnerAsync("account-1", first));
        Assert.False(await store.IsOwnerAsync("account-1", second));
    }

    [Fact]
    public async Task ReleaseAsync_DoesNotRemoveLeaseOwnedByAnotherConnection()
    {
        var store = new InMemoryActiveAccountLeaseStore();
        Guid owner = Guid.NewGuid();
        Guid other = Guid.NewGuid();
        await store.TryClaimAsync("account-1", owner);

        bool releasedByOther =
            await store.ReleaseAsync("account-1", other);

        Assert.False(releasedByOther);
        Assert.True(await store.IsOwnerAsync("account-1", owner));
    }
}
