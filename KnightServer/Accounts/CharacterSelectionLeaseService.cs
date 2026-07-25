using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Networking;

namespace KnightOnline.Server.Accounts;

public sealed class CharacterSelectionLeaseService(
    IActiveAccountLeaseStore sessions,
    TimeSpan timeout)
{
    public void Start(
        ClientConnection connection,
        string accountKey)
    {
        _ = ExpireAsync(connection, accountKey);
    }

    private async Task ExpireAsync(
        ClientConnection connection,
        string accountKey)
    {
        try
        {
            await Task.Delay(timeout);
            if (connection.PlayerSession != null ||
                !await sessions.IsOwnerAsync(
                    accountKey,
                    connection.ConnectionId))
                return;

            await sessions.ReleaseAsync(
                accountKey,
                connection.ConnectionId);
            await connection.ForceDisconnectAsync(
                ForcedDisconnectReason.CharacterSelectionTimeout,
                "Bạn bị mất kết nối máy chủ");
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[Authentication][Warning] Selection lease cleanup failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
