namespace KnightOnline.Server.Hosting;

/// <summary>
/// Prevents two KnightServer processes from serving the same local deployment.
/// The lease itself remains process-local; an OS mutex is released on clean
/// shutdown or process termination, including a crash.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly ManualResetEventSlim _release = new(false);
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _ownerThread;
    private Exception? _acquireFailure;

    private SingleInstanceGuard(string name)
    {
        _ownerThread = new Thread(() => HoldMutex(name))
        {
            IsBackground = true,
            Name = "KnightOnline.SingleInstanceGuard",
        };
        _ownerThread.Start();
        _ready.Wait();
        if (_acquireFailure != null)
        {
            _release.Dispose();
            _ready.Dispose();
            throw _acquireFailure;
        }
    }

    public static SingleInstanceGuard Acquire(
        string name = @"Global\KnightOnline.GameServer.SingleInstance")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new SingleInstanceGuard(name);
    }

    public void Dispose()
    {
        _release.Set();
        _ownerThread.Join();
        _release.Dispose();
        _ready.Dispose();
    }

    private void HoldMutex(string name)
    {
        using var mutex = new Mutex(initiallyOwned: false, name);
        bool ownsMutex = false;
        try
        {
            try
            {
                ownsMutex = mutex.WaitOne(TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                // The previous process exited unexpectedly. Its OS-owned lock
                // is now ours; all of its TCP connections have already closed.
                ownsMutex = true;
            }
            if (!ownsMutex)
                throw new InvalidOperationException(
                    "Another KnightServer instance is already running. " +
                    "This deployment is configured for one server instance.");
        }
        catch (Exception exception)
        {
            _acquireFailure = exception;
        }
        finally
        {
            _ready.Set();
        }

        if (!ownsMutex)
            return;
        _release.Wait();
        mutex.ReleaseMutex();
    }
}
