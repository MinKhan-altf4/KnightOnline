using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Players;

namespace KnightOnline.Server.Networking;

public sealed class ClientConnection(
    TcpClient tcpClient,
    PacketDispatcher dispatcher,
    int maximumPacketSize) : IAsyncDisposable
{
    private readonly NetworkStream _stream = tcpClient.GetStream();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _disposeState;

    public PlayerSession? PlayerSession { get; private set; }
    public Guid ConnectionId { get; } = Guid.NewGuid();
    public string? AccountKey { get; private set; }
    public Guid AccountSessionGeneration { get; private set; }
    public bool IsGuest { get; private set; }
    public string RemoteAddress { get; } =
        (tcpClient.Client.RemoteEndPoint as IPEndPoint)?
            .Address.ToString() ?? "unknown";

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
                    await ReadEnvelopeAsync(cancellationToken);

                if (envelope == null)
                    break;

                await dispatcher.DispatchAsync(
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
        try
        {
            await SendAsync(
                PacketType.ForcedDisconnect,
                new ForcedDisconnectPacket(reason, message));
        }
        catch (Exception exception)
        {
            // Closing the transport is still required if delivery fails.
            Console.WriteLine(
                $"[Network][Warning] Forced-disconnect packet delivery failed: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            await DisposeAsync();
        }
    }

    public async Task SendAsync<T>(
        PacketType packetType,
        T payload,
        CancellationToken cancellationToken = default)
    {
        string payloadJson = JsonSerializer.Serialize(payload);
        string envelopeJson = JsonSerializer.Serialize(
            new PacketEnvelope(packetType, payloadJson));
        byte[] bytes = Encoding.UTF8.GetBytes(envelopeJson);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _stream.WriteAsync(
                BitConverter.GetBytes(bytes.Length),
                cancellationToken);
            await _stream.WriteAsync(bytes, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<PacketEnvelope?> ReadEnvelopeAsync(
        CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[sizeof(int)];
        if (!await ReadExactlyAsync(lengthBuffer, cancellationToken))
            return null;

        int length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length <= 0 || length > maximumPacketSize)
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

        await _stream.DisposeAsync();
        tcpClient.Dispose();
    }
}
