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
using KnightOnline.Client.Network.Handlers;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Client.Data.Models;
using VContainer;
using UnityEngine;

namespace KnightOnline.Client.Network
{
    public class NetworkClient : MonoBehaviour
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isDisconnecting;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private IEventBus _eventBus;
        private NetworkSettings _settings;
        private IReadOnlyDictionary<PacketType, IClientPacketHandler> _packetHandlers;
        public bool IsConnected =>
            !_isDisconnecting &&
            _tcpClient != null &&
            _tcpClient.Connected &&
            _stream != null;

        [Inject]
        public void Construct(
            IEventBus eventBus,
            NetworkSettings settings,
            IEnumerable<IClientPacketHandler> packetHandlers)
        {
            _eventBus = eventBus;
            _settings = settings;
            var handlers = new Dictionary<PacketType, IClientPacketHandler>();
            foreach (IClientPacketHandler handler in packetHandlers)
            {
                if (!handlers.TryAdd(handler.PacketType, handler))
                    throw new InvalidOperationException(
                        $"Duplicate client packet handler: {handler.PacketType}.");
            }

            _packetHandlers = handlers;
        }

        public async UniTask ConnectAsync()
        {
            try
            {
                _isDisconnecting = false;
                _tcpClient = new TcpClient();
                _cts = new CancellationTokenSource();
                await _tcpClient.ConnectAsync(_settings.Host, _settings.Port);
                _stream = _tcpClient.GetStream();
                _ = ReceiveLoopAsync(_cts.Token);
                await SendPacketAsync(PacketType.ConnectRequest, new ConnectRequestPacket("1.0.0"));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Connection error: {ex.Message}");
                _eventBus.Publish(new ServerConnectionResultEvent(
                    ConnectionOutcome.NetworkError,
                    ex.Message));
            }
        }

        public UniTask SendCreateCharacterRequestAsync(
            CharacterCreationDraftData draft) =>
            SendPacketAsync(
                PacketType.CreateCharacterRequest,
                new CreateCharacterRequestPacket(
                    draft.RequestId,
                    draft.ServerId,
                    draft.SlotIndex,
                    draft.CharacterName,
                    draft.ClassDefinitionId,
                    draft.BodyTypeDefinitionId,
                    ToAppearancePackets(draft.AppearanceSelections),
                    draft.CatalogVersion));

        public UniTask SendListCharactersRequestAsync() =>
            SendPacketAsync(PacketType.ListCharactersRequest, new ListCharactersRequestPacket());

        public UniTask SendCharacterCreationCatalogRequestAsync(string serverId) =>
            SendPacketAsync(
                PacketType.GetCharacterCreationCatalogRequest,
                new GetCharacterCreationCatalogRequestPacket(serverId));

        public UniTask SendCheckCharacterNameRequestAsync(
            string serverId,
            string characterName) =>
            SendPacketAsync(
                PacketType.CheckCharacterNameRequest,
                new CheckCharacterNameRequestPacket(serverId, characterName));

        public UniTask SendListMonstersRequestAsync() =>
            SendPacketAsync(PacketType.ListMonstersRequest, new ListMonstersRequestPacket());

        public UniTask SendAttackMonsterRequestAsync(int monsterId) =>
            SendPacketAsync(
                PacketType.AttackMonsterRequest,
                new AttackMonsterRequestPacket(monsterId));

        public UniTask SendSelectCharacterRequestAsync(int characterId) =>
            SendPacketAsync(
                PacketType.SelectCharacterRequest,
                new SelectCharacterRequestPacket(characterId));

        public UniTask SendPlayerMoveInputAsync(Vector2 direction) =>
            SendPacketAsync(
                PacketType.PlayerMoveInput,
                new PlayerMoveInputPacket(direction.x, direction.y));

        public UniTask SendCreateGuestRequestAsync(string deviceId) =>
            SendPacketAsync(
                PacketType.CreateGuestRequest,
                new CreateGuestRequestPacket(deviceId));

        public UniTask SendResumeAccountRequestAsync(
            string refreshToken,
            string deviceId) =>
            SendPacketAsync(
                PacketType.ResumeAccountRequest,
                new ResumeAccountRequestPacket(refreshToken, deviceId));

        public UniTask SendLoginRequestAsync(
            string username,
            string password,
            string deviceId,
            string guestRefreshToken) =>
            SendPacketAsync(
                PacketType.LoginRequest,
                new LoginRequestPacket(
                    username,
                    password,
                    deviceId,
                    guestRefreshToken));

        public UniTask SendLeaveAccountSessionRequestAsync() =>
            SendPacketAsync(
                PacketType.LeaveAccountSessionRequest,
                new LeaveAccountSessionRequestPacket());

        public UniTask SendBeginRegistrationRequestAsync(
            Guid requestId,
            string guestRefreshToken,
            string deviceId,
            string pkceChallenge) =>
            SendPacketAsync(
                PacketType.BeginRegistrationRequest,
                new BeginRegistrationRequestPacket(
                    requestId,
                    guestRefreshToken,
                    deviceId,
                    pkceChallenge));

        public UniTask SendCompleteDevelopmentRegistrationRequestAsync(
            Guid transactionId,
            string authorizationCode,
            string pkceVerifier,
            string username,
            string password) =>
            SendPacketAsync(
                PacketType.CompleteDevelopmentRegistrationRequest,
                new CompleteDevelopmentRegistrationRequestPacket(
                    transactionId,
                    authorizationCode,
                    pkceVerifier,
                    username,
                    password));

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
            catch (IOException) when (ct.IsCancellationRequested) { } // ← fix: bỏ 'ex' không dùng
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
            if (length <= 0 || length > _settings.MaximumPacketBytes)
                throw new InvalidDataException($"Invalid packet length: {length}.");

            var payloadBuffer = new byte[length];
            if (await ReadExactlyAsync(payloadBuffer, ct) == false) return null;
            return JsonSerializer.Deserialize<PacketEnvelope>(Encoding.UTF8.GetString(payloadBuffer));
        }

        private async UniTask<bool> ReadExactlyAsync(byte[] buffer, CancellationToken ct)
        {
            NetworkStream stream = _stream;
            if (stream == null)
                return false;

            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(
                    buffer,
                    totalRead,
                    buffer.Length - totalRead,
                    ct);
                if (read == 0) return false;
                totalRead += read;
            }
            return true;
        }

        private async UniTask SendPacketAsync<T>(PacketType type, T payload)
        {
            if (_stream == null)
            {
                if (_isDisconnecting)
                    return;

                throw new InvalidOperationException("Not connected to server.");
            }

            var payloadJson = JsonSerializer.Serialize(payload);
            var envelopeJson = JsonSerializer.Serialize(new PacketEnvelope(type, payloadJson));
            var bytes = Encoding.UTF8.GetBytes(envelopeJson);
            await _sendLock.WaitAsync();
            try
            {
                NetworkStream stream = _stream;
                if (stream == null)
                {
                    if (_isDisconnecting)
                        return;

                    throw new InvalidOperationException(
                        "Connection closed before the packet could be sent.");
                }

                await stream.WriteAsync(
                    BitConverter.GetBytes(bytes.Length),
                    0,
                    4);
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (ObjectDisposedException) when (_isDisconnecting)
            {
                // Expected when Unity exits Play Mode during an in-flight send.
            }
            catch (IOException) when (_isDisconnecting)
            {
                // Expected when shutdown closes the socket during a write.
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private void HandlePacket(PacketEnvelope envelope)
        {
            if (_packetHandlers.TryGetValue(envelope.Type, out var handler))
            {
                handler.Handle(envelope.Payload);
                return;
            }

            Debug.LogWarning($"[Network] No client handler for packet {envelope.Type}.");
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

        private static IReadOnlyList<AppearanceSelectionPacket>
            ToAppearancePackets(
                IReadOnlyList<AppearanceSelectionData> selections)
        {
            if (selections == null)
                return Array.Empty<AppearanceSelectionPacket>();

            var packets = new AppearanceSelectionPacket[selections.Count];
            for (var index = 0; index < selections.Count; index++)
            {
                AppearanceSelectionData selection = selections[index];
                packets[index] = new AppearanceSelectionPacket(
                    selection.SlotDefinitionId,
                    selection.OptionDefinitionId);
            }
            return packets;
        }
    }
}
