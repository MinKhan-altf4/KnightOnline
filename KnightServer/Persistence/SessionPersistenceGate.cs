using Npgsql;

namespace KnightOnline.Server.Persistence;

/// <summary>
/// Bounds and retries session-lifecycle database writes (snapshot save,
/// character release) so a burst of disconnects cannot exhaust PostgreSQL
/// connections (load test 14-11-2026: SQLSTATE 53300 when ~500 players left
/// at once).
///
/// Only wrap operations that are safe to repeat: the session snapshot write is
/// fenced by generation/version (a repeated write after a lost commit is
/// rejected, not applied twice) and release is idempotent.
/// </summary>
public sealed class SessionPersistenceGate
{
    private readonly SemaphoreSlim _slots;
    private readonly int _maximumAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public SessionPersistenceGate(
        int maximumConcurrentOperations,
        int maximumAttempts = 4,
        TimeSpan? baseDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumConcurrentOperations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
        _slots = new SemaphoreSlim(maximumConcurrentOperations, maximumConcurrentOperations);
        _maximumAttempts = maximumAttempts;
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(100);
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Operations currently holding a slot (diagnostics/tests).</summary>
    public int InFlight { get; private set; }

    public async Task<T> RunAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; ; attempt++)
        {
            await _slots.WaitAsync(cancellationToken);
            Exception failure;
            try
            {
                lock (_slots)
                    InFlight++;
                return await work(cancellationToken);
            }
            catch (Exception exception) when (
                attempt < _maximumAttempts && IsTransient(exception) &&
                !cancellationToken.IsCancellationRequested)
            {
                failure = exception;
            }
            finally
            {
                lock (_slots)
                    InFlight--;
                _slots.Release();
            }

            // Back off outside the slot so other operations can proceed.
            TimeSpan wait = _baseDelay * Math.Pow(2, attempt - 1) +
                            TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50));
            Console.WriteLine(
                $"[Persistence][Warning] {operation} attempt {attempt}/{_maximumAttempts} " +
                $"hit a transient failure ({Describe(failure)}); retrying in " +
                $"{wait.TotalMilliseconds:0} ms.");
            await _delay(wait, cancellationToken);
        }
    }

    public Task RunAsync(
        string operation,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        RunAsync(operation, async token =>
        {
            await work(token);
            return true;
        }, cancellationToken);

    /// <summary>
    /// True for failures where retrying later can succeed: PostgreSQL
    /// out-of-connections/startup/serialization errors and Npgsql errors it
    /// marks transient, including when EF Core wraps them.
    /// </summary>
    public static bool IsTransient(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            switch (current)
            {
                case PostgresException postgres when postgres.SqlState is
                    PostgresErrorCodes.TooManyConnections or
                    PostgresErrorCodes.ConfigurationLimitExceeded or
                    PostgresErrorCodes.CannotConnectNow or
                    PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.DeadlockDetected:
                    return true;
                case NpgsqlException npgsql when npgsql.IsTransient:
                    return true;
                case TimeoutException:
                    return true;
            }
        }
        return false;
    }

    private static string Describe(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
                return $"SQLSTATE {postgres.SqlState}";
            if (current.InnerException == null)
                return current.GetType().Name;
        }
        return exception.GetType().Name;
    }
}
