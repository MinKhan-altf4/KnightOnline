using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Networking.Handlers;
using KnightOnline.Server.Time;

namespace KnightServer.Tests.Networking;

public sealed class ProtocolAndSendQueueTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CurrentProtocol_CompletesHandshake()
    {
        await using Pair pair = await Pair.CreateAsync(requireHandshake: true);
        Task run = pair.Server.RunAsync();

        await pair.SendAsync(PacketType.ConnectRequest,
            new ConnectRequestPacket("test", PacketProtocol.Version));
        ConnectResponsePacket response =
            await pair.ReadAsync<ConnectResponsePacket>(PacketType.ConnectResponse);

        Assert.Equal(ConnectResult.Success, response.Result);
        Assert.True(pair.Server.HasCompletedHandshake);
    }

    [Fact]
    public async Task OldClientWithoutProtocolVersion_IsRejectedAndClosed()
    {
        await using Pair pair = await Pair.CreateAsync(requireHandshake: true);
        Task run = pair.Server.RunAsync();

        // Protocol 1 clients sent only ClientVersion.
        await pair.SendRawAsync(PacketType.ConnectRequest,
            "{\"ClientVersion\":\"1.0.0\"}");
        ConnectResponsePacket response =
            await pair.ReadAsync<ConnectResponsePacket>(PacketType.ConnectResponse);

        Assert.Equal(ConnectResult.VersionMismatch, response.Result);
        Assert.Equal(0, await pair.ReadRawByteCountAsync());
        Assert.False(pair.Server.HasCompletedHandshake);
    }

    [Fact]
    public async Task PacketsBeforeHandshake_AreIgnored()
    {
        var recorder = new RecordingHandler();
        await using Pair pair = await Pair.CreateAsync(
            requireHandshake: true, extraHandler: recorder);
        Task run = pair.Server.RunAsync();

        await pair.SendAsync(PacketType.ListMonstersRequest,
            new ListMonstersRequestPacket());
        await pair.SendAsync(PacketType.ConnectRequest,
            new ConnectRequestPacket("test", PacketProtocol.Version));
        await pair.ReadAsync<ConnectResponsePacket>(PacketType.ConnectResponse);
        await pair.SendAsync(PacketType.ListMonstersRequest,
            new ListMonstersRequestPacket());
        await recorder.WaitForCallsAsync(1, Deadline);

        Assert.Equal(1, recorder.Calls);
    }

    [Fact]
    public async Task SendQueue_PreservesOrder()
    {
        await using Pair pair = await Pair.CreateAsync(requireHandshake: false);

        for (int index = 0; index < 50; index++)
            Assert.True(pair.Server.TryEnqueue(PacketType.ConnectResponse,
                new ConnectResponsePacket(ConnectResult.Success, index.ToString())));

        for (int index = 0; index < 50; index++)
        {
            ConnectResponsePacket packet =
                await pair.ReadAsync<ConnectResponsePacket>(PacketType.ConnectResponse);
            Assert.Equal(index.ToString(), packet.Message);
        }
    }

    [Fact]
    public async Task FullSendQueue_ClosesSlowConsumer_WithoutBlocking()
    {
        // Client never reads; tiny queue plus large frames fill the socket
        // buffers and then the queue.
        await using Pair pair = await Pair.CreateAsync(
            requireHandshake: false, sendQueueCapacity: 4);
        string big = new('x', 32 * 1024);

        bool rejected = false;
        var started = DateTime.UtcNow;
        for (int index = 0; index < 10_000 && !rejected; index++)
        {
            rejected = !pair.Server.TryEnqueue(PacketType.ConnectResponse,
                new ConnectResponsePacket(ConnectResult.Success, big));
        }

        Assert.True(rejected);
        Assert.True(DateTime.UtcNow - started < Deadline);
        Assert.True(pair.Server.IsClosed);
        Assert.False(pair.Server.TryEnqueue(PacketType.ConnectResponse,
            new ConnectResponsePacket(ConnectResult.Success, "after close")));
    }

    [Fact]
    public async Task SendAsync_AfterClose_ThrowsObjectDisposed()
    {
        await using Pair pair = await Pair.CreateAsync(requireHandshake: false);
        await pair.Server.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            pair.Server.SendAsync(PacketType.ConnectResponse,
                new ConnectResponsePacket(ConnectResult.Success, "x")));
    }

    private sealed class RecordingHandler : IPacketHandler
    {
        private int _calls;
        public PacketType PacketType => PacketType.ListMonstersRequest;
        public PacketAccessLevel RequiredAccess => PacketAccessLevel.Anonymous;
        public int Calls => Volatile.Read(ref _calls);

        public Task HandleAsync(ClientConnection connection, string payload,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.CompletedTask;
        }

        public async Task WaitForCallsAsync(int expected, TimeSpan timeout)
        {
            DateTime end = DateTime.UtcNow + timeout;
            while (Calls < expected && DateTime.UtcNow < end)
                await Task.Delay(10);
        }
    }

    private sealed class Pair : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;

        private Pair(TcpClient client, ClientConnection server)
        {
            _client = client;
            _stream = client.GetStream();
            Server = server;
        }

        public ClientConnection Server { get; }

        public static async Task<Pair> CreateAsync(bool requireHandshake,
            IPacketHandler? extraHandler = null,
            int sendQueueCapacity = ClientConnection.DefaultSendQueueCapacity)
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
                TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10),
                maximumActiveAccounts: 10);
            var handlers = new List<IPacketHandler>
            {
                new ConnectPacketHandler(
                    new DevelopmentAccountIdentityProvider("dev"),
                    developmentBypassEnabled: false),
            };
            if (extraHandler != null)
                handlers.Add(extraHandler);
            var dispatcher = new PacketDispatcher(handlers, store,
                new SystemServerClock(), requireHandshake);
            var server = new ClientConnection(accepted, dispatcher,
                maximumPacketSize: 64 * 1024,
                sendQueueCapacity: sendQueueCapacity);
            return new Pair(client, server);
        }

        public Task SendAsync<T>(PacketType type, T payload) =>
            SendRawAsync(type, JsonSerializer.Serialize(payload));

        public async Task SendRawAsync(PacketType type, string payloadJson)
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new PacketEnvelope(type, payloadJson)));
            await _stream.WriteAsync(BitConverter.GetBytes(body.Length));
            await _stream.WriteAsync(body);
        }

        public async Task<T> ReadAsync<T>(PacketType expected)
        {
            using var timeout = new CancellationTokenSource(Deadline);
            var header = new byte[4];
            await _stream.ReadExactlyAsync(header, timeout.Token);
            var body = new byte[BitConverter.ToInt32(header)];
            await _stream.ReadExactlyAsync(body, timeout.Token);
            PacketEnvelope envelope = JsonSerializer.Deserialize<PacketEnvelope>(
                Encoding.UTF8.GetString(body))!;
            Assert.Equal(expected, envelope.Type);
            return JsonSerializer.Deserialize<T>(envelope.Payload)!;
        }

        /// <summary>0 means the server closed the socket.</summary>
        public async Task<int> ReadRawByteCountAsync()
        {
            using var timeout = new CancellationTokenSource(Deadline);
            try
            {
                return await _stream.ReadAsync(new byte[1], timeout.Token);
            }
            catch (IOException)
            {
                return 0;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await Server.DisposeAsync();
        }
    }
}
