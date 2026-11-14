using System.Net;
using System.Net.Sockets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Time;

namespace KnightServer.Tests.Networking;

public sealed class ClientConnectionAnonymousTimeoutTests
{
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task AnonymousConnection_IsClosed_AfterTimeout()
    {
        (TcpClient client, ClientConnection server) =
            await CreatePairAsync(TimeSpan.FromMilliseconds(200));
        using (client)
        {
            Task run = server.RunAsync();

            Task finished = await Task.WhenAny(run, Task.Delay(TestDeadline));

            Assert.Same(run, finished);
            await AssertRemoteClosedAsync(client);
        }
    }

    [Fact]
    public async Task AuthenticatedConnection_IsNotClosed_ByAnonymousTimeout()
    {
        (TcpClient client, ClientConnection server) =
            await CreatePairAsync(TimeSpan.FromMilliseconds(200));
        using (client)
        await using (server)
        {
            Assert.True(server.TryAttachAccount("account-1", Guid.NewGuid()));
            using var stop = new CancellationTokenSource();
            Task run = server.RunAsync(stop.Token);

            await Task.Delay(TimeSpan.FromMilliseconds(600));

            Assert.False(run.IsCompleted);
            stop.Cancel();
            await run.WaitAsync(TestDeadline);
        }
    }

    [Fact]
    public async Task NoTimeout_KeepsAnonymousConnectionOpen()
    {
        (TcpClient client, ClientConnection server) =
            await CreatePairAsync(anonymousTimeout: null);
        using (client)
        await using (server)
        {
            using var stop = new CancellationTokenSource();
            Task run = server.RunAsync(stop.Token);

            await Task.Delay(TimeSpan.FromMilliseconds(400));

            Assert.False(run.IsCompleted);
            stop.Cancel();
            await run.WaitAsync(TestDeadline);
        }
    }

    private static async Task AssertRemoteClosedAsync(TcpClient client)
    {
        var buffer = new byte[1];
        using var deadline = new CancellationTokenSource(TestDeadline);
        try
        {
            int read = await client.GetStream().ReadAsync(buffer, deadline.Token);
            Assert.Equal(0, read);
        }
        catch (IOException)
        {
            // A reset also proves the server closed the socket.
        }
    }

    private static async Task<(TcpClient Client, ClientConnection Server)>
        CreatePairAsync(TimeSpan? anonymousTimeout)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new TcpClient();
        Task connect = client.ConnectAsync(IPAddress.Loopback, port);
        TcpClient accepted = await listener.AcceptTcpClientAsync();
        await connect;
        listener.Stop();

        var store = new InMemoryActiveAccountLeaseStore(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            maximumActiveAccounts: 10);
        var dispatcher = new PacketDispatcher([], store, new SystemServerClock());
        var server = new ClientConnection(
            accepted,
            dispatcher,
            maximumPacketSize: 64 * 1024,
            anonymousTimeout);
        return (client, server);
    }
}
