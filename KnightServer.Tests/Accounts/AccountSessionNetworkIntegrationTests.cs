using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Accounts;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Networking.Handlers;
using KnightOnline.Server.Time;

namespace KnightServer.Tests.Accounts;

public sealed class AccountSessionNetworkIntegrationTests
{
    private static readonly DateTime InitialUtc =
        new(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Heartbeat_TravelsThroughDispatcher_AndReturnsRenewal()
    {
        var clock = new MutableClock(InitialUtc);
        var store = CreateStore();
        await using TestConnection pair = await CreateConnectionAsync(
            store,
            clock);
        ActiveAccountLeaseClaim claim = await AttachLeaseAsync(pair, store);
        clock.UtcNow = InitialUtc.AddSeconds(5);

        await pair.DispatchAsync(
            PacketType.AccountSessionHeartbeatRequest,
            new AccountSessionHeartbeatRequestPacket(claim.Generation));
        PacketEnvelope response = await pair.ReadResponseAsync();
        AccountSessionHeartbeatResponsePacket? heartbeat =
            JsonSerializer.Deserialize<AccountSessionHeartbeatResponsePacket>(
                response.Payload);

        Assert.Equal(
            PacketType.AccountSessionHeartbeatResponse,
            response.Type);
        Assert.NotNull(heartbeat);
        Assert.True(heartbeat.Renewed);
        Assert.Equal(InitialUtc.AddSeconds(25), heartbeat.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task AuthenticatedGameplayTraffic_RenewsAccountLease()
    {
        var clock = new MutableClock(InitialUtc);
        var store = CreateStore();
        var handler = new RecordingAuthenticatedPacketHandler();
        await using TestConnection pair = await CreateConnectionAsync(
            store,
            clock,
            handler);
        ActiveAccountLeaseClaim claim = await AttachLeaseAsync(pair, store);
        clock.UtcNow = InitialUtc.AddSeconds(15);

        await pair.DispatchAsync(PacketType.PlayerMoveInput, new { });
        clock.UtcNow = InitialUtc.AddSeconds(34);

        Assert.True(handler.WasHandled);
        Assert.True(await store.IsOwnerAsync(
            "account-1",
            pair.Server.ConnectionId,
            claim.Generation,
            clock.UtcNow));
    }

    [Fact]
    public async Task ExpiredLease_IsRejectedAtDispatcherBoundary()
    {
        var clock = new MutableClock(InitialUtc);
        var store = CreateStore();
        await using TestConnection pair = await CreateConnectionAsync(
            store,
            clock);
        ActiveAccountLeaseClaim claim = await AttachLeaseAsync(pair, store);
        clock.UtcNow = InitialUtc.AddSeconds(21);

        await pair.DispatchAsync(
            PacketType.AccountSessionHeartbeatRequest,
            new AccountSessionHeartbeatRequestPacket(claim.Generation));
        PacketEnvelope response = await pair.ReadResponseAsync();
        ForcedDisconnectPacket? forced =
            JsonSerializer.Deserialize<ForcedDisconnectPacket>(
                response.Payload);

        Assert.Equal(PacketType.ForcedDisconnect, response.Type);
        Assert.NotNull(forced);
        Assert.Equal(
            ForcedDisconnectReason.SessionLeaseExpired,
            forced.Reason);
    }

    [Fact]
    public async Task OldConnection_IsRejected_AfterNewGenerationClaimsLease()
    {
        var clock = new MutableClock(InitialUtc);
        var store = CreateStore();
        await using TestConnection oldConnection =
            await CreateConnectionAsync(store, clock);
        ActiveAccountLeaseClaim oldClaim =
            await AttachLeaseAsync(oldConnection, store);
        clock.UtcNow = InitialUtc.AddSeconds(21);
        ActiveAccountLeaseClaim replacement = await store.TryClaimAsync(
            "account-1",
            Guid.NewGuid(),
            clock.UtcNow);

        await oldConnection.DispatchAsync(
            PacketType.AccountSessionHeartbeatRequest,
            new AccountSessionHeartbeatRequestPacket(oldClaim.Generation));
        PacketEnvelope response = await oldConnection.ReadResponseAsync();

        Assert.Equal(
            ActiveAccountLeaseClaimStatus.Acquired,
            replacement.Status);
        Assert.NotEqual(oldClaim.Generation, replacement.Generation);
        Assert.Equal(PacketType.ForcedDisconnect, response.Type);
    }

    [Fact]
    public async Task MultiDeviceScenario_RejectsB_UntilADisconnectGraceExpires()
    {
        var store = CreateStore();
        Guid connectionA = Guid.NewGuid();
        Guid connectionB = Guid.NewGuid();
        ActiveAccountLeaseClaim a = await store.TryClaimAsync(
            "account-1",
            connectionA,
            InitialUtc);

        ActiveAccountLeaseClaim bWhileAActive = await store.TryClaimAsync(
            "account-1",
            connectionB,
            InitialUtc.AddSeconds(5));
        await store.BeginDisconnectGraceAsync(
            "account-1",
            connectionA,
            a.Generation,
            InitialUtc.AddSeconds(6));
        ActiveAccountLeaseClaim bDuringGrace = await store.TryClaimAsync(
            "account-1",
            connectionB,
            InitialUtc.AddSeconds(15));
        ActiveAccountLeaseClaim bAfterGrace = await store.TryClaimAsync(
            "account-1",
            connectionB,
            InitialUtc.AddSeconds(17));
        ActiveAccountLeaseRenewal staleA = await store.RenewAsync(
            "account-1",
            connectionA,
            a.Generation,
            InitialUtc.AddSeconds(17));

        Assert.Equal(
            ActiveAccountLeaseClaimStatus.ActiveElsewhere,
            bWhileAActive.Status);
        Assert.Equal(
            ActiveAccountLeaseClaimStatus.ActiveElsewhere,
            bDuringGrace.Status);
        Assert.Equal(
            ActiveAccountLeaseClaimStatus.Acquired,
            bAfterGrace.Status);
        Assert.False(staleA.Renewed);
    }

    private static InMemoryActiveAccountLeaseStore CreateStore() =>
        new(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(10),
            maximumActiveAccounts: 500);

    private static async Task<ActiveAccountLeaseClaim> AttachLeaseAsync(
        TestConnection pair,
        IActiveAccountLeaseStore store)
    {
        ActiveAccountLeaseClaim claim = await store.TryClaimAsync(
            "account-1",
            pair.Server.ConnectionId,
            InitialUtc);
        Assert.True(pair.Server.TryAttachAccount(
            "account-1",
            claim.Generation));
        return claim;
    }

    private static async Task<TestConnection> CreateConnectionAsync(
        IActiveAccountLeaseStore store,
        IServerClock clock,
        IPacketHandler? additionalHandler = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new TcpClient();
        Task connect = client.ConnectAsync(IPAddress.Loopback, port);
        TcpClient accepted = await listener.AcceptTcpClientAsync();
        await connect;
        listener.Stop();

        IPacketHandler[] handlers = additionalHandler == null
            ? [new AccountSessionHeartbeatPacketHandler(store, clock)]
            : [
                new AccountSessionHeartbeatPacketHandler(store, clock),
                additionalHandler,
            ];
        var dispatcher = new PacketDispatcher(
            handlers,
            store,
            clock);
        var server = new ClientConnection(
            accepted,
            dispatcher,
            maximumPacketSize: 1024 * 1024);
        return new TestConnection(client, server, dispatcher);
    }

    private sealed class MutableClock(DateTime utcNow) : IServerClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class RecordingAuthenticatedPacketHandler : IPacketHandler
    {
        public PacketType PacketType => PacketType.PlayerMoveInput;
        public bool WasHandled { get; private set; }

        public Task HandleAsync(
            ClientConnection connection,
            string payload,
            CancellationToken cancellationToken)
        {
            WasHandled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TestConnection(
        TcpClient client,
        ClientConnection server,
        PacketDispatcher dispatcher) : IAsyncDisposable
    {
        public ClientConnection Server { get; } = server;

        public Task DispatchAsync<T>(PacketType type, T payload) =>
            dispatcher.DispatchAsync(
                Server,
                new PacketEnvelope(type, JsonSerializer.Serialize(payload)),
                CancellationToken.None);

        public async Task<PacketEnvelope> ReadResponseAsync()
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(3));
            NetworkStream stream = client.GetStream();
            var lengthBytes = new byte[sizeof(int)];
            await stream.ReadExactlyAsync(lengthBytes, timeout.Token);
            int length = BitConverter.ToInt32(lengthBytes);
            var payload = new byte[length];
            await stream.ReadExactlyAsync(payload, timeout.Token);
            return JsonSerializer.Deserialize<PacketEnvelope>(
                       Encoding.UTF8.GetString(payload))
                   ?? throw new InvalidDataException(
                       "Server returned an invalid packet envelope.");
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await Server.DisposeAsync();
        }
    }
}
