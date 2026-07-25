using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Networking;

namespace KnightOnline.Server.Accounts;

/// <summary>Temporary identity source until login tokens are implemented.</summary>
public sealed class DevelopmentAccountIdentityProvider(string accountKey)
    : IAccountIdentityProvider
{
    public string ResolveAccountKey(
        ClientConnection connection,
        ConnectRequestPacket request) => accountKey;
}
