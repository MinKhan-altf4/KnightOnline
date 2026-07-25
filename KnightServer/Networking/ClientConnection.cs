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
    public string? AccountKey { get; private set; }
    public bool IsGuest { get; private set; }

    public bool TryAttachAccount(string accountKey, bool isGuest = false)
    {
        if (AccountKey != null || string.IsNullOrWhiteSpace(accountKey))
            return false;

        AccountKey = accountKey;
        IsGuest = isGuest;
        return true;
    }

    public bool TryAttachPlayerSession(PlayerSession session)
    {
        if (PlayerSession != null)
            return false;

        PlayerSession = session;
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
        catch (Exception)
        {
            // Closing the transport is still required if delivery fails.
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
