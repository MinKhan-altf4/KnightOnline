using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Networking;

namespace KnightOnline.Server.Accounts;

public interface IAccountIdentityProvider
{
    string ResolveAccountKey(
        ClientConnection connection,
        ConnectRequestPacket request);
}
