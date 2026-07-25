using System.Collections.Concurrent;

namespace KnightOnline.Server.Accounts;

public sealed class AuthenticationRateLimiter(
    int maximumAttempts,
    TimeSpan window)
{
    private readonly ConcurrentDictionary<string, AttemptWindow> _windows =
        new(StringComparer.Ordinal);

    public bool TryAcquire(string key, DateTime utcNow)
    {
        AttemptWindow attemptWindow =
            _windows.GetOrAdd(key, _ => new AttemptWindow(utcNow));

        lock (attemptWindow)
        {
            if (utcNow - attemptWindow.StartedAtUtc >= window)
            {
                attemptWindow.StartedAtUtc = utcNow;
                attemptWindow.Count = 0;
            }

            if (attemptWindow.Count >= maximumAttempts)
                return false;

            attemptWindow.Count++;
            return true;
        }
    }

    private sealed class AttemptWindow(DateTime startedAtUtc)
    {
        public DateTime StartedAtUtc { get; set; } = startedAtUtc;
        public int Count { get; set; }
    }
}
