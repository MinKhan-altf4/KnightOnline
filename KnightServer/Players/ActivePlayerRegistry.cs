using System.Collections.Concurrent;
using KnightOnline.Server.Networking;

namespace KnightOnline.Server.Players;

public sealed class ActivePlayerRegistry
{
    private readonly ConcurrentDictionary<int, ClientConnection> _byCharacterId = new();

    public bool TryClaim(int characterId, ClientConnection connection) =>
        _byCharacterId.TryAdd(characterId, connection);

    public void Release(int characterId, ClientConnection connection) =>
        _byCharacterId.TryRemove(
            new KeyValuePair<int, ClientConnection>(
                characterId,
                connection));

    public void Release(ClientConnection connection)
    {
        int? characterId = connection.PlayerSession?.CharacterId;
        if (characterId.HasValue)
            Release(characterId.Value, connection);
    }
}
