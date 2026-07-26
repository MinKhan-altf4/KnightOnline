namespace KnightOnline.Server.Accounts;

public enum ActiveAccountLeaseClaimResult : byte
{
    Acquired = 0,
    AlreadyOwned = 1,
    ActiveElsewhere = 2,
}

public interface IActiveAccountLeaseStore
{
    ValueTask<ActiveAccountLeaseClaimResult> TryClaimAsync(
        string accountKey,
        Guid connectionId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsOwnerAsync(
        string accountKey,
        Guid connectionId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ReleaseAsync(
        string accountKey,
        Guid connectionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Single-process adapter used for local development and alpha testing.
/// Production can replace it with a Redis/distributed lease implementation
/// without changing authentication or gameplay handlers.
/// </summary>
public sealed class InMemoryActiveAccountLeaseStore :
    IActiveAccountLeaseStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, Guid> _owners =
        new(StringComparer.Ordinal);

    public ValueTask<ActiveAccountLeaseClaimResult> TryClaimAsync(
        string accountKey,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (!_owners.TryGetValue(accountKey, out Guid current))
            {
                _owners.Add(accountKey, connectionId);
                return ValueTask.FromResult(
                    ActiveAccountLeaseClaimResult.Acquired);
            }

            return ValueTask.FromResult(
                current == connectionId
                    ? ActiveAccountLeaseClaimResult.AlreadyOwned
                    : ActiveAccountLeaseClaimResult.ActiveElsewhere);
        }
    }

    public ValueTask<bool> IsOwnerAsync(
        string accountKey,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return ValueTask.FromResult(
                _owners.TryGetValue(accountKey, out Guid current) &&
                current == connectionId);
        }
    }

    public ValueTask<bool> ReleaseAsync(
        string accountKey,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (_owners.TryGetValue(accountKey, out Guid current) &&
                current == connectionId)
            {
                _owners.Remove(accountKey);
                return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromResult(false);
    }
}
