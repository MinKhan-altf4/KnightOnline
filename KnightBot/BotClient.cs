using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Bot;

/// <summary>Stage a bot reached; failures are reported per stage.</summary>
public enum BotStage
{
    Connect,
    Handshake,
    CreateGuest,
    Catalog,
    CreateCharacter,
    SelectCharacter,
    EnterWorld,
    InWorld,
}

public sealed class BotFailedException(BotStage stage, string reason)
    : Exception($"{stage}: {reason}")
{
    public BotStage Stage { get; } = stage;
}

/// <summary>
/// One simulated player speaking the real wire protocol: handshake, guest
/// account, character creation, select, enter world, then random movement
/// with account heartbeats until cancelled.
/// </summary>
public sealed class BotClient : IAsyncDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly int _index;
    private readonly BotOptions _options;
    private readonly BotMetrics _metrics;
    private readonly TcpClient _tcp = new() { NoDelay = true };
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<PacketType, TaskCompletionSource<string>> _waiting = new();
    private readonly ConcurrentDictionary<long, long> _moveSentAt = new();
    private readonly Random _random;
    private NetworkStream? _stream;
    private long _moveSequence;

    public BotClient(int index, BotOptions options, BotMetrics metrics)
    {
        _index = index;
        _options = options;
        _metrics = metrics;
        _random = new Random(options.Seed + index);
    }

    public BotStage Stage { get; private set; } = BotStage.Connect;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        await _tcp.ConnectAsync(_options.Host, _options.Port, cancellationToken);
        _stream = _tcp.GetStream();
        Task receive = ReceiveLoopAsync(cancellationToken);

        Stage = BotStage.Handshake;
        var connect = await RequestAsync<ConnectResponsePacket>(
            PacketType.ConnectRequest,
            new ConnectRequestPacket("knight-bot", PacketProtocol.Version),
            PacketType.ConnectResponse, cancellationToken);
        if (connect.Result != ConnectResult.Success)
            throw new BotFailedException(Stage, $"{connect.Result} {connect.Message}");

        Stage = BotStage.CreateGuest;
        var guest = await RequestAsync<AuthenticationResponsePacket>(
            PacketType.CreateGuestRequest,
            new CreateGuestRequestPacket($"knight-bot-{_options.RunId}-{_index}"),
            PacketType.CreateGuestResponse, cancellationToken);
        if (guest.Result != AuthenticationResultCode.Success)
            throw new BotFailedException(Stage, $"{guest.Result} {guest.Message}");
        Task heartbeat = HeartbeatLoopAsync(guest.SessionGeneration,
            TimeSpan.FromSeconds(Math.Max(1, guest.HeartbeatIntervalSeconds)),
            cancellationToken);

        Stage = BotStage.Catalog;
        var catalog = await RequestAsync<GetCharacterCreationCatalogResponsePacket>(
            PacketType.GetCharacterCreationCatalogRequest,
            new GetCharacterCreationCatalogRequestPacket(_options.ServerId),
            PacketType.GetCharacterCreationCatalogResponse, cancellationToken);

        Stage = BotStage.CreateCharacter;
        var created = await RequestAsync<CreateCharacterResponsePacket>(
            PacketType.CreateCharacterRequest,
            BuildCreateRequest(catalog),
            PacketType.CreateCharacterResponse, cancellationToken);
        if (created.Result != CreateCharacterResult.Success || created.Character == null)
            throw new BotFailedException(Stage, $"{created.Result} {created.Message}");

        Stage = BotStage.SelectCharacter;
        var selected = await RequestAsync<SelectCharacterResponsePacket>(
            PacketType.SelectCharacterRequest,
            new SelectCharacterRequestPacket(created.Character.CharacterId),
            PacketType.SelectCharacterResponse, cancellationToken);
        if (selected.Result != SelectCharacterResult.Success)
            throw new BotFailedException(Stage, $"{selected.Result} {selected.Message}");

        Stage = BotStage.EnterWorld;
        var entered = await RequestAsync<EnterWorldResponsePacket>(
            PacketType.EnterWorldRequest,
            new EnterWorldRequestPacket(selected.GameplaySessionId),
            PacketType.EnterWorldResponse, cancellationToken);
        if (entered.Result != EnterWorldResult.Success)
            throw new BotFailedException(Stage, $"{entered.Result} {entered.Message}");

        Stage = BotStage.InWorld;
        _metrics.RecordReady(Stopwatch.GetElapsedTime(started));
        await MoveLoopAsync(cancellationToken);
        await Task.WhenAll(receive, heartbeat).WaitAsync(TimeSpan.FromSeconds(2))
            .ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private CreateCharacterRequestPacket BuildCreateRequest(
        GetCharacterCreationCatalogResponsePacket catalog)
    {
        CharacterClassDefinitionPacket characterClass = catalog.Classes.FirstOrDefault()
            ?? throw new BotFailedException(Stage, "catalog has no classes");
        string bodyType = characterClass.AllowedBodyTypeIds.FirstOrDefault()
            ?? catalog.BodyTypes.FirstOrDefault()?.DefinitionId
            ?? throw new BotFailedException(Stage, "catalog has no body types");

        var appearance = catalog.AppearanceOptions
            .Where(option => option.IsStarterOption &&
                (option.AllowedBodyTypeIds.Count == 0 ||
                 option.AllowedBodyTypeIds.Contains(bodyType)) &&
                (option.AllowedClassDefinitionIds.Count == 0 ||
                 option.AllowedClassDefinitionIds.Contains(characterClass.DefinitionId)))
            .GroupBy(option => option.SlotDefinitionId)
            .Select(group => new AppearanceSelectionPacket(group.Key, group.First().DefinitionId))
            .ToArray();

        // Letters/digits only, 3-20 chars (CharacterNamePolicy).
        string name = $"Bot{_options.RunId}{_index:0000}";
        return new CreateCharacterRequestPacket(Guid.NewGuid(), _options.ServerId,
            slotIndex: 0, name, characterClass.DefinitionId, bodyType, appearance,
            catalog.CatalogVersion);
    }

    private async Task MoveLoopAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(_options.MoveIntervalMilliseconds);
        (float X, float Y) direction = (0, 0);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_random.NextDouble() < 0.2)
                {
                    double angle = _random.NextDouble() * Math.PI * 2;
                    direction = _random.NextDouble() < 0.15
                        ? (0f, 0f)
                        : ((float)Math.Cos(angle), (float)Math.Sin(angle));
                }

                long sequence = Interlocked.Increment(ref _moveSequence);
                _moveSentAt[sequence] = Stopwatch.GetTimestamp();
                await SendAsync(PacketType.PlayerMoveInput,
                    new PlayerMoveInputPacket(direction.X, direction.Y, sequence),
                    cancellationToken);
                await Task.Delay(interval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task HeartbeatLoopAsync(Guid generation, TimeSpan interval,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);
                await SendAsync(PacketType.AccountSessionHeartbeatRequest,
                    new AccountSessionHeartbeatRequestPacket(generation),
                    cancellationToken);
            }
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or IOException or ObjectDisposedException)
        {
        }
    }

    private async Task<T> RequestAsync<T>(PacketType requestType, object payload,
        PacketType responseType, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_waiting.TryAdd(responseType, completion))
            throw new InvalidOperationException($"Already waiting for {responseType}.");

        long started = Stopwatch.GetTimestamp();
        try
        {
            await SendAsync(requestType, payload, cancellationToken);
            string json = await completion.Task.WaitAsync(RequestTimeout, cancellationToken);
            _metrics.RecordRequest(requestType, Stopwatch.GetElapsedTime(started));
            return JsonSerializer.Deserialize<T>(json)
                ?? throw new BotFailedException(Stage, $"empty {responseType}");
        }
        catch (TimeoutException)
        {
            throw new BotFailedException(Stage, $"no {responseType} within {RequestTimeout.TotalSeconds}s");
        }
        finally
        {
            _waiting.TryRemove(responseType, out _);
        }
    }

    private async Task SendAsync(PacketType type, object payload,
        CancellationToken cancellationToken)
    {
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new PacketEnvelope(type, JsonSerializer.Serialize(payload, payload.GetType()))));
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _stream!.WriteAsync(BitConverter.GetBytes(body.Length), cancellationToken);
            await _stream.WriteAsync(body, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var header = new byte[4];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _stream!.ReadExactlyAsync(header, cancellationToken);
                int length = BitConverter.ToInt32(header);
                var body = new byte[length];
                await _stream.ReadExactlyAsync(body, cancellationToken);
                PacketEnvelope? envelope = JsonSerializer.Deserialize<PacketEnvelope>(
                    Encoding.UTF8.GetString(body));
                if (envelope == null)
                    continue;

                _metrics.RecordReceived(envelope.Type, length + 4);
                HandleUnsolicited(envelope);
                if (_waiting.TryGetValue(envelope.Type, out var waiter))
                    waiter.TrySetResult(envelope.Payload);
            }
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or IOException or
                EndOfStreamException or ObjectDisposedException)
        {
            if (!cancellationToken.IsCancellationRequested)
                _metrics.RecordDisconnect(Stage);
            foreach (var waiter in _waiting.Values)
                waiter.TrySetException(new BotFailedException(Stage, "connection closed"));
        }
    }

    private void HandleUnsolicited(PacketEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case PacketType.PlayerPositionSnapshot:
                var snapshot = JsonSerializer.Deserialize<PlayerPositionSnapshotPacket>(envelope.Payload);
                if (snapshot != null &&
                    _moveSentAt.TryRemove(snapshot.AcknowledgedSequence, out long sentAt))
                    _metrics.RecordMoveAck(Stopwatch.GetElapsedTime(sentAt));
                break;
            case PacketType.WorldEntitiesSnapshot:
                var entities = JsonSerializer.Deserialize<WorldEntitiesSnapshotPacket>(envelope.Payload);
                if (entities != null)
                    _metrics.RecordEntities(entities.Spawned.Count, entities.Moved.Count,
                        entities.Despawned.Count);
                break;
            case PacketType.ForcedDisconnect:
                var forced = JsonSerializer.Deserialize<ForcedDisconnectPacket>(envelope.Payload);
                _metrics.RecordForcedDisconnect(forced?.Reason.ToString() ?? "unknown");
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _tcp.Dispose();
        await Task.CompletedTask;
    }
}
