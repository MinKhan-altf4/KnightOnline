using KnightOnline.Server.Networking;

namespace KnightOnline.Server.Accounts;

public sealed class AccountSessionRegistry
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, ClientConnection> _sessions =
        new(StringComparer.Ordinal);

    public ClientConnection? Register(
        string accountKey,
        ClientConnection connection)
    {
        lock (_syncRoot)
        {
            if (!_sessions.TryGetValue(accountKey, out var existing))
            {
                _sessions.Add(accountKey, connection);
                return null;
            }

            if (ReferenceEquals(existing, connection))
                return null;

            // A collision invalidates the account session entirely. Neither
            // connection wins; a later clean login may establish a new session.
            _sessions.Remove(accountKey);
            return existing;
        }
    }

    public void Release(ClientConnection connection)
    {
        string? accountKey = connection.AccountKey;
        if (accountKey == null)
            return;

        lock (_syncRoot)
        {
            if (_sessions.TryGetValue(accountKey, out var current) &&
                ReferenceEquals(current, connection))
            {
                _sessions.Remove(accountKey);
            }
        }
    }
}
