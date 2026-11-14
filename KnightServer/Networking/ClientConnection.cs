using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Players;

namespace KnightOnline.Server.Networking;

/// <summary>
/// One TCP client. Reads packets on <see cref="RunAsync"/> and writes through a
/// bounded FIFO queue drained by a single writer task, so a broadcast or the
/// world tick never awaits the network (see World_Simulation_P1_Design §4).
/// </summary>
public sealed class ClientConnection : IAsyncDisposable
{
    public const int DefaultSendQueueCapacity = 256;
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(2);

    private readonly TcpClient _tcpClient;
    private readonly PacketDispatcher _dispatcher;
    private readonly int _maximumPacketSize;
    private readonly NetworkStream _stream;
    private readonly Channel<byte[]> _sendQueue;
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _writerCancellation = new();
    private readonly TimeSpan _anonymousTimeout;
    private long _anonymousSinceTimestamp = Stopwatch.GetTimestamp();
    private int _disposeState;
    private int _handshakeCompleted;

    /// <param name="anonymousTimeout">
    /// How long a connection may stay without an attached account before it is
    /// closed, so idle TCP connections cannot hold transport capacity (B4).
    /// Null or <see cref="Timeout.InfiniteTimeSpan"/> disables the limit.
    /// </param>
    /// <param name="sendQueueCapacity">
    /// Maximum queued outgoing packets. A broadcast that finds the queue full
    /// closes the connection as a slow consumer.
    /// </param>
    public ClientConnection(
        TcpClient tcpClient,
        PacketDispatcher dispatcher,
        int maximumPacketSize,
        TimeSpan? anonymousTimeout = null,
        int sendQueueCapacity = DefaultSendQueueCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sendQueueCapacity, 1);
        _tcpClient = tcpClient;
        _dispatcher = dispatcher;
        _maximumPacketSize = maximumPacketSize;
        _anonymousTimeout = anonymousTimeout ?? Timeout.InfiniteTimeSpan;
        _stream = tcpClient.GetStream();
        RemoteAddress = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?
            .Address.ToString() ?? "unknown";
        _sendQueue = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(sendQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
        _writerTask = Task.Run(RunWriterAsync);
    }

    public PlayerSession? PlayerSession { get; private set; }
    public Guid ConnectionId { get; } = Guid.NewGuid();
    public string? AccountKey { get; private set; }
    public Guid AccountSessionGeneration { get; private set; }
    public bool IsGuest { get; private set; }
    public string RemoteAddress { get; }

    /// <summary>True once a supported ConnectRequest was accepted (B10).</summary>
    public bool HasCompletedHandshake =>
        Volatile.Read(ref _handshakeCompleted) == 1;

    public bool IsClosed => Volatile.Read(ref _disposeState) != 0;

    public void MarkHandshakeCompleted() =>
        Interlocked.Exchange(ref _handshakeCompleted, 1);

    public bool TryAttachAccount(
        string accountKey,
        Guid sessionGeneration,
        bool isGuest = false)
    {
        if (AccountKey != null ||
            string.IsNullOrWhiteSpace(accountKey) ||
            sessionGeneration == Guid.Empty)
            return false;

        AccountKey = accountKey;
        AccountSessionGeneration = sessionGeneration;
        IsGuest = isGuest;
        return true;
    }

    // Development bypass compatibility only. This path does not establish a
    // server lease and must remain disabled in the checked-in configuration.
    public bool TryAttachAccount(string accountKey, bool isGuest = false) =>
        TryAttachAccount(accountKey, Guid.NewGuid(), isGuest);

    public bool TryDetachAccount()
    {
        if (AccountKey == null || PlayerSession != null)
            return false;

        AccountKey = null;
        AccountSessionGeneration = Guid.Empty;
        IsGuest = false;
        // Logged out back to the entry screen: the anonymous budget restarts.
        Interlocked.Exchange(ref _anonymousSinceTimestamp,
            Stopwatch.GetTimestamp());
        return true;
    }

    public void MarkAccountRegistered()
    {
        if (AccountKey == null)
            throw new InvalidOperationException(
                "Cannot register an anonymous connection.");

        IsGuest = false;
    }

    public bool TryAttachPlayerSession(PlayerSession session)
    {
        if (PlayerSession != null)
            return false;

        PlayerSession = session;
        return true;
    }

    public bool TryDetachPlayerSession(out PlayerSession? session)
    {
        session = PlayerSession;
        if (session == null)
            return false;

        PlayerSession = null;
        return true;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                PacketEnvelope? envelope =
                    await ReadEnvelopeWithAnonymousTimeoutAsync(
                        cancellationToken);

                if (envelope == null)
                    break;

                await _dispatcher.DispatchAsync(
                    this,
                    envelope,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
            // Client disconnected or reset the TCP connection.
        }
        catch (ObjectDisposedException)
        {
            // Forced disconnect closed the stream.
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"[Server] Client processing error: {exception.Message}");
        }
        finally
        {
            await DisposeAsync();
        }
    }

    public async Task ForceDisconnectAsync(
        ForcedDisconnectReason reason,
        string message)
    {
        // Never wait for queue space here: a stuck client must still be closed.
        if (!TryEnqueue(
                PacketType.ForcedDisconnect,
                new ForcedDisconnectPacket(reason, message)))
        {
            Console.WriteLine(
                "[Network][Warning] Forced-disconnect packet could not be " +
                $"queued for {ConnectionId}; closing anyway.");
        }

        await CloseAfterFlushAsync();
    }

    /// <summary>
    /// Queues a packet, waiting for queue space (backpressure). Use from the
    /// connection's own request handlers. Throws
    /// <see cref="ObjectDisposedException"/> if the connection is closed.
    /// </summary>
    public async Task SendAsync<T>(
        PacketType packetType,
        T payload,
        CancellationToken cancellationToken = default)
    {
        byte[] frame = Serialize(packetType, payload);
        try
        {
            await _sendQueue.Writer.WriteAsync(frame, cancellationToken);
        }
        catch (ChannelClosedException)
        {
            throw new ObjectDisposedException(nameof(ClientConnection));
        }
    }

    /// <summary>
    /// Queues a packet without waiting. Use for broadcasts and the world tick.
    /// A full queue means the client cannot keep up: it is closed as a slow
    /// consumer and false is returned.
    /// </summary>
    public bool TryEnqueue<T>(PacketType packetType, T payload) =>
        !IsClosed && TryEnqueueFrame(packetType, Serialize(packetType, payload));

    /// <summary>
    /// Like <see cref="TryEnqueue{T}"/> for a payload that is already JSON
    /// (hot paths that build JSON once and reuse fragments).
    /// </summary>
    public bool TryEnqueuePayloadJson(PacketType packetType, string payloadJson) =>
        !IsClosed && TryEnqueueFrame(packetType, Frame(packetType, payloadJson));

    private bool TryEnqueueFrame(PacketType packetType, byte[] frame)
    {
        if (_sendQueue.Writer.TryWrite(frame))
            return true;

        if (!IsClosed)
        {
            Console.WriteLine(
                "[Network][Warning] Closing slow consumer " +
                $"{ConnectionId} ({RemoteAddress}): send queue full at " +
                $"{packetType}.");
            _ = DisposeAsync().AsTask();
        }

        return false;
    }

    /// <summary>
    /// Stops accepting packets, lets the writer deliver what is queued (up to
    /// <see cref="FlushTimeout"/>), then closes the socket.
    /// </summary>
    public async Task CloseAfterFlushAsync()
    {
        _sendQueue.Writer.TryComplete();
        try
        {
            await _writerTask.WaitAsync(FlushTimeout);
        }
        catch (TimeoutException)
        {
            // Client is not reading; close without waiting further.
        }

        await DisposeAsync();
    }

    private static byte[] Serialize<T>(PacketType packetType, T payload) =>
        Frame(packetType, JsonSerializer.Serialize(payload));

    private static byte[] Frame(PacketType packetType, string payloadJson)
    {
        string envelopeJson = JsonSerializer.Serialize(
            new PacketEnvelope(packetType, payloadJson));
        byte[] body = Encoding.UTF8.GetBytes(envelopeJson);
        var frame = new byte[sizeof(int) + body.Length];
        BitConverter.GetBytes(body.Length).CopyTo(frame, 0);
        body.CopyTo(frame, sizeof(int));
        return frame;
    }

    private async Task RunWriterAsync()
    {
        CancellationToken token = _writerCancellation.Token;
        try
        {
            while (await _sendQueue.Reader.WaitToReadAsync(token))
            {
                while (_sendQueue.Reader.TryRead(out byte[]? frame))
                    await _stream.WriteAsync(frame, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Connection disposed.
        }
        catch (Exception exception) when (
            exception is IOException or ObjectDisposedException or SocketException)
        {
            // Client went away; the read loop or dispose cleans up.
            _ = DisposeAsync().AsTask();
        }
    }

    private async Task<PacketEnvelope?> ReadEnvelopeWithAnonymousTimeoutAsync(
        CancellationToken cancellationToken)
    {
        if (AccountKey != null || _anonymousTimeout == Timeout.InfiniteTimeSpan)
            return await ReadEnvelopeAsync(cancellationToken);

        TimeSpan remaining = _anonymousTimeout - Stopwatch.GetElapsedTime(
            Interlocked.Read(ref _anonymousSinceTimestamp));
        if (remaining <= TimeSpan.Zero)
        {
            LogAnonymousTimeout();
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(remaining);
        try
        {
            return await ReadEnvelopeAsync(timeout.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            LogAnonymousTimeout();
            return null;
        }
    }

    private void LogAnonymousTimeout() =>
        Console.WriteLine(
            "[Network][Info] Closing unauthenticated connection " +
            $"{ConnectionId} after {_anonymousTimeout.TotalSeconds:0}s " +
            "without login.");

    private async Task<PacketEnvelope?> ReadEnvelopeAsync(
        CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[sizeof(int)];
        if (!await ReadExactlyAsync(lengthBuffer, cancellationToken))
            return null;

        int length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length <= 0 || length > _maximumPacketSize)
            throw new InvalidDataException($"Invalid packet length: {length}.");

        var payloadBuffer = new byte[length];
        if (!await ReadExactlyAsync(payloadBuffer, cancellationToken))
            return null;

        return JsonSerializer.Deserialize<PacketEnvelope>(
            Encoding.UTF8.GetString(payloadBuffer));
    }

    private async Task<bool> ReadExactlyAsync(
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = await _stream.ReadAsync(
                buffer.AsMemory(totalRead),
                cancellationToken);

            if (read == 0)
                return false;

            totalRead += read;
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        _sendQueue.Writer.TryComplete();
        _writerCancellation.Cancel();
        await _stream.DisposeAsync();
        _tcpClient.Dispose();
        // _writerCancellation is intentionally not disposed: the writer task
        // may still observe its token while unwinding.
    }
}
