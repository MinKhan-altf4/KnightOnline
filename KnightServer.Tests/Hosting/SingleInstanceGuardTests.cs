using KnightOnline.Server.Hosting;

namespace KnightServer.Tests.Hosting;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void Acquire_RejectsSecondInstance_AndAllowsRestartAfterRelease()
    {
        string mutexName = $@"Global\KnightOnline.Tests.{Guid.NewGuid():N}";
        using (SingleInstanceGuard first =
               SingleInstanceGuard.Acquire(mutexName))
        {
            Assert.Throws<InvalidOperationException>(
                () => SingleInstanceGuard.Acquire(mutexName));
        }

        using SingleInstanceGuard restarted =
            SingleInstanceGuard.Acquire(mutexName);
    }
}
