using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Shared.Packets;
using UnityEngine;
using VContainer;

namespace KnightOnline.Client.Network
{
    public class NetworkClient : MonoBehaviour
    {
        private const string ServerIp = "127.0.0.1";
        private const int Port = 7777;

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isDisconnecting;
        private IEventBus _eventBus;

        [Inject]
        public void Construct(IEventBus eventBus) => _eventBus = eventBus;

        public async UniTask ConnectAsync()
        {
            try
            {
                _isDisconnecting = false;
                _tcpClient = new TcpClient();
                _cts = new CancellationTokenSource();
                await _tcpClient.ConnectAsync(ServerIp, Port);
                _stream = _tcpClient.GetStream();
                _ = ReceiveLoopAsync(_cts.Token);
                await SendPacketAsync(PacketType.ConnectRequest, new ConnectRequestPacket("1.0.0"));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Connection error: {ex.Message}");
                _eventBus.Publish(new ServerConnectionResultEvent(ConnectResult.NetworkError, ex.Message));
            }
        }

        public UniTask SendCreateCharacterRequestAsync(string characterName) =>
            SendPacketAsync(PacketType.CreateCharacterRequest, new CreateCharacterRequestPacket(characterName));

        public UniTask SendListCharactersRequestAsync() =>
            SendPacketAsync(PacketType.ListCharactersRequest, new ListCharactersRequestPacket());

        private async UniTask ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (_tcpClient != null && _tcpClient.Connected && !ct.IsCancellationRequested)
                {
                    var envelope = await ReadEnvelopeAsync(ct);
                    if (envelope == null) break;
                    HandlePacket(envelope);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (IOException ex) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested) Debug.LogError($"[Network] Receive error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private async UniTask<PacketEnvelope> ReadEnvelopeAsync(CancellationToken ct)
        {
            var lengthBuffer = new byte[4];
            if (await ReadExactlyAsync(lengthBuffer, ct) == false) return null;
            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > 1024 * 1024) throw new InvalidDataException("Invalid packet length.");

            var payloadBuffer = new byte[length];
            if (await ReadExactlyAsync(payloadBuffer, ct) == false) return null;
            return JsonSerializer.Deserialize<PacketEnvelope>(Encoding.UTF8.GetString(payloadBuffer));
        }

        private async UniTask<bool> ReadExactlyAsync(byte[] buffer, CancellationToken ct)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await _stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, ct);
                if (read == 0) return false;
                totalRead += read;
            }
            return true;
        }

        private async UniTask SendPacketAsync<T>(PacketType type, T payload)
        {
            if (_stream == null) throw new InvalidOperationException("Not connected to server.");
            var payloadJson = JsonSerializer.Serialize(payload);
            var envelopeJson = JsonSerializer.Serialize(new PacketEnvelope(type, payloadJson));
            var bytes = Encoding.UTF8.GetBytes(envelopeJson);
            await _stream.WriteAsync(BitConverter.GetBytes(bytes.Length), 0, 4);
            await _stream.WriteAsync(bytes, 0, bytes.Length);
        }

        private void HandlePacket(PacketEnvelope envelope)
        {
            switch (envelope.Type)
            {
                case PacketType.ConnectResponse:
                    var connect = JsonSerializer.Deserialize<ConnectResponsePacket>(envelope.Payload);
                    if (connect != null)
                        _eventBus.Publish(new ServerConnectionResultEvent(connect.Result, connect.Message));
                    break;

                case PacketType.CreateCharacterResponse:
                    var create = JsonSerializer.Deserialize<CreateCharacterResponsePacket>(envelope.Payload);
                    if (create != null)
                    {
                        var success = create.Result == CreateCharacterResult.Success;
                        _eventBus.Publish(new CharacterCreationResultEvent(success, create.Message,
                            success ? new CharacterData(create.Message) : null));
                    }
                    break;

                case PacketType.ListCharactersResponse:
                    var list = JsonSerializer.Deserialize<ListCharactersResponsePacket>(envelope.Payload);
                    if (list != null)
                    {
                        var characters = new List<CharacterData>(list.Characters.Count);
                        foreach (var entry in list.Characters)
                            if (!string.IsNullOrWhiteSpace(entry.CharacterName))
                                characters.Add(new CharacterData(entry.CharacterName));
                        _eventBus.Publish(new CharacterListReceivedEvent(characters));
                    }
                    break;
            }
        }

        public void Disconnect()
        {
            if (_isDisconnecting) return;
            _isDisconnecting = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _stream?.Dispose();
            _stream = null;
            _tcpClient?.Dispose();
            _tcpClient = null;
            _eventBus?.Publish(new ServerDisconnectedEvent());
        }

        private void OnDestroy() => Disconnect();
    }
}
