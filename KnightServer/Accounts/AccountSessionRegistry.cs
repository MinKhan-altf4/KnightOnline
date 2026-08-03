namespace KnightOnline.Server.Accounts;

public enum ActiveAccountLeaseClaimStatus : byte
{
    Acquired = 0,
    AlreadyOwned = 1,
    ActiveElsewhere = 2,
    CapacityReached = 3,
    CoolingDown = 4,
}

public readonly record struct ActiveAccountLeaseClaim(
    ActiveAccountLeaseClaimStatus Status,
    Guid Generation,
    DateTime ExpiresAtUtc);

public readonly record struct ActiveAccountLeaseRenewal(
    bool Renewed,
    DateTime ExpiresAtUtc);

public readonly record struct ActiveAccountLeaseCapacitySnapshot(
    int ActiveCount,
    int MaximumActiveAccounts);

public interface IActiveAccountLeaseStore
{
    ValueTask<ActiveAccountLeaseClaim> TryClaimAsync(
        string accountKey,
        Guid connectionId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsOwnerAsync(
        string accountKey,
        Guid connectionId,
        Guid generation,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    ValueTask<ActiveAccountLeaseRenewal> RenewAsync(
        string accountKey,
        Guid connectionId,
        Guid generation,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    ValueTask<bool> BeginDisconnectGraceAsync(
        string accountKey,
        Guid connectionId,
        Guid generation,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ReleaseAsync(
        string accountKey,
        Guid connectionId,
        Guid generation,
        CancellationToken cancellationToken = default);

    ValueTask<ActiveAccountLeaseCapacitySnapshot> GetCapacityAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Single-process adapter for Development and controlled local tests. Expired
/// entries are removed opportunistically. A distributed implementation must
/// provide the same generation ownership guarantees before multi-node rollout.
/// </summary>
public sealed class InMemoryActiveAccountLeaseStore :
    IActiveAccountLeaseStore
{
    private sealed record Lease(
        Guid ConnectionId,
        Guid Generation,
        DateTime ExpiresAtUtc,
        bool IsCoolingDown = false);

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, Lease> _leases =
        new(StringComparer.Ordinal);
    private readonly TimeSpan _leaseTtl;
    private readonly TimeSpan _disconnectGrace;
    private readonly int _maximumActiveAccounts;

    public InMemoryActiveAccountLeaseStore(
        TimeSpan leaseTtl,
        TimeSpan disconnectGrace,
        int maximumActiveAccounts)
    {
        if (leaseTtl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseTtl));
        if (disconnectGrace < TimeSpan.Zero || disconnectGrace >= leaseTtl)
            throw new ArgumentOutOfRangeException(nameof(disconnectGrace));
        if (maximumActiveAccounts <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumActiveAccounts));

        _leaseTtl = leaseTtl;
        _disconnectGrace = disconnectGrace;
        _maximumActiveAccounts = maximumActiveAccounts;
    }

    public ValueTask<ActiveAccountLeaseClaim> TryClaimAsync(
        string accountKey,
        Guid connectionId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(accountKey, connectionId, utcNow);

        lock (_syncRoot)
        {
            RemoveExpiredLeases(utcNow);
            if (_leases.TryGetValue(accountKey, out Lease? current) &&
                current.ExpiresAtUtc <= utcNow)
                current = null;

            if (current == null)
            {
                if (_leases.Count >= _maximumActiveAccounts)
                {
                    return ValueTask.FromResult(new ActiveAccountLeaseClaim(
                        ActiveAccountLeaseClaimStatus.CapacityReached,
                        Guid.Empty,
                        default));
                }

                var acquired = new Lease(
                    connectionId,
                    Guid.NewGuid(),
                    utcNow + _leaseTtl);
                _leases.Add(accountKey, acquired);
                return ValueTask.FromResult(new ActiveAccountLeaseClaim(
                    ActiveAccountLeaseClaimStatus.Acquired,
                    acquired.Generation,
                    acquired.ExpiresAtUtc));
            }

            return ValueTask.FromResult(new ActiveAccountLeaseClaim(
                current.IsCoolingDown
                    ? ActiveAccountLeaseClaimStatus.CoolingDown
                    : current.ConnectionId == connectionId
                        ? ActiveAccountLeaseClaimStatus.AlreadyOwned
                        : ActiveAccountLeaseClaimStatus.ActiveElsewhere,
                !current.IsCoolingDown && current.ConnectionId == connectionId
                    ? current.Generation
                    : Guid.Empty,
                current.ExpiresAtUtc));
        }
    }

    public ValueTask<bool> IsOwnerAsync(
        string accountKey,
        Guid connectionId,
        Guid generation,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return ValueTask.FromResult(
                TryGetOwnedLease(
                    accountKey,
                    connectionId,
                    generation,
                    utcNow,
                    out _));
        }
    }

    public ValueTask<ActiveAccountLeaseRenewal> RenewAsync(
        string accountKey,
        Guid connectionId,
        Guid generation,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (!TryGetOwnedLease(
                    accountKey,
                    connectionId,
                    generation,
                    utcNow,
                    out _))
            {
                return ValueTask.FromResult(
                    new ActiveAccountLeaseRenewal(false, default));
            }

            DateTime expiresAtUtc = utcNow + _leaseTtl;
            _leases[accountKey] = new Lease(
                connectionId,
                generation,
                expiresAtUtc);
            return ValueTask.FromResult(
                new ActiveAccountLeaseRenewal(true, expiresAtUtc));
        }
    }

    public ValueTask<bool> BeginDisconnectGraceAsync(
        string accountKey,
        Guid connectionId,
        Guid generation,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (!TryGetOwnedLease(
                    accountKey,
                    connectionId,
                    generation,
                    utcNow,
                    out Lease? current))
            {
                return ValueTask.FromResult(false);
            }

            DateTime graceExpiry = utcNow + _disconnectGrace;
            _leases[accountKey] = current! with
            {
                ExpiresAtUtc = graceExpiry < current.ExpiresAtUtc
                    ? graceExpiry
                    : current.ExpiresAtUtc,
                IsCoolingDown = true,
            };
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<bool> ReleaseAsync(
        string accountKey,
        Guid connectionId,
        Guid generation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            if (_leases.TryGetValue(accountKey, out Lease? current) &&
                current.ConnectionId == connectionId &&
                current.Generation == generation)
            {
                _leases.Remove(accountKey);
                return ValueTask.FromResult(true);
            }

            return ValueTask.FromResult(false);
        }
    }

    public ValueTask<ActiveAccountLeaseCapacitySnapshot> GetCapacityAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            RemoveExpiredLeases(utcNow);
            return ValueTask.FromResult(
                new ActiveAccountLeaseCapacitySnapshot(
                    _leases.Count,
                    _maximumActiveAccounts));
        }
    }

    private bool TryGetOwnedLease(
        string accountKey,
        Guid connectionId,
        Guid generation,
        DateTime utcNow,
        out Lease? lease)
    {
        if (!_leases.TryGetValue(accountKey, out lease))
            return false;
        if (lease.ExpiresAtUtc <= utcNow)
        {
            _leases.Remove(accountKey);
            lease = null;
            return false;
        }

        return !lease.IsCoolingDown &&
               lease.ConnectionId == connectionId &&
               lease.Generation == generation;
    }

    private static void Validate(
        string accountKey,
        Guid connectionId,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
            throw new ArgumentException("Account key is required.", nameof(accountKey));
        if (connectionId == Guid.Empty)
            throw new ArgumentException("Connection id is required.", nameof(connectionId));
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Lease time must be UTC.", nameof(utcNow));
    }

    private void RemoveExpiredLeases(DateTime utcNow)
    {
        foreach (string accountKey in _leases
                     .Where(pair => pair.Value.ExpiresAtUtc <= utcNow)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _leases.Remove(accountKey);
        }
    }
}
