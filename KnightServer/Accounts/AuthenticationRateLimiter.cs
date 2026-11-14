using System.Collections.Concurrent;

namespace KnightOnline.Server.Accounts;

/// <summary>
/// Fixed-window attempt counter per key.
///
/// Keys are partly client-controlled (device id, refresh token digest), so the
/// table must stay bounded (B3): windows that have expired are swept at most
/// once per window, and once <paramref name="maximumTrackedKeys"/> live keys
/// exist, new keys are rejected (fail closed) until the next sweep frees room.
/// Existing keys keep working while the table is full.
/// </summary>
public sealed class AuthenticationRateLimiter
{
    public const int DefaultMaximumTrackedKeys = 100_000;

    private readonly ConcurrentDictionary<string, AttemptWindow> _windows =
        new(StringComparer.Ordinal);
    private readonly int _maximumAttempts;
    private readonly TimeSpan _window;
    private readonly int _maximumTrackedKeys;
    private readonly object _sweepLock = new();
    private DateTime _nextSweepUtc = DateTime.MinValue;

    public AuthenticationRateLimiter(
        int maximumAttempts,
        TimeSpan window,
        int maximumTrackedKeys = DefaultMaximumTrackedKeys)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumTrackedKeys, 1);
        _maximumAttempts = maximumAttempts;
        _window = window;
        _maximumTrackedKeys = maximumTrackedKeys;
    }

    /// <summary>Number of keys currently held in memory.</summary>
    public int TrackedKeyCount => _windows.Count;

    public bool TryAcquire(string key, DateTime utcNow)
    {
        SweepIfDue(utcNow);

        if (!_windows.TryGetValue(key, out AttemptWindow? attemptWindow))
        {
            if (_windows.Count >= _maximumTrackedKeys)
            {
                // Forced sweep once before failing closed.
                Sweep(utcNow);
                if (_windows.Count >= _maximumTrackedKeys)
                    return false;
            }

            attemptWindow = _windows.GetOrAdd(key, _ => new AttemptWindow(utcNow));
        }

        lock (attemptWindow)
        {
            if (attemptWindow.Removed)
            {
                // Lost a race with the sweeper; count against a fresh window.
                attemptWindow = _windows.GetOrAdd(key, _ => new AttemptWindow(utcNow));
                return TryCount(attemptWindow, utcNow);
            }

            return TryCountLocked(attemptWindow, utcNow);
        }
    }

    private bool TryCount(AttemptWindow attemptWindow, DateTime utcNow)
    {
        lock (attemptWindow)
            return TryCountLocked(attemptWindow, utcNow);
    }

    private bool TryCountLocked(AttemptWindow attemptWindow, DateTime utcNow)
    {
        if (utcNow - attemptWindow.StartedAtUtc >= _window)
        {
            attemptWindow.StartedAtUtc = utcNow;
            attemptWindow.Count = 0;
        }

        if (attemptWindow.Count >= _maximumAttempts)
            return false;

        attemptWindow.Count++;
        return true;
    }

    private void SweepIfDue(DateTime utcNow)
    {
        if (utcNow < _nextSweepUtc)
            return;
        Sweep(utcNow);
    }

    private void Sweep(DateTime utcNow)
    {
        if (!Monitor.TryEnter(_sweepLock))
            return; // another thread is sweeping
        try
        {
            _nextSweepUtc = utcNow + _window;
            foreach (KeyValuePair<string, AttemptWindow> entry in _windows)
            {
                AttemptWindow value = entry.Value;
                lock (value)
                {
                    if (utcNow - value.StartedAtUtc < _window)
                        continue;
                    value.Removed = true;
                    _windows.TryRemove(entry);
                }
            }
        }
        finally
        {
            Monitor.Exit(_sweepLock);
        }
    }

    private sealed class AttemptWindow(DateTime startedAtUtc)
    {
        public DateTime StartedAtUtc { get; set; } = startedAtUtc;
        public int Count { get; set; }
        public bool Removed { get; set; }
    }
}
