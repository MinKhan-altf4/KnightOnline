using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Client.Network
{
    public class NetworkClient : MonoBehaviour
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isDisconnecting = false;
        private const string ServerIp = "127.0.0.1";
        private const int Port = 7777;

        private IEventBus _eventBus;

        // MonoBehaviour không dùng được constructor injection.
        // VContainer gọi method này tự động ngay sau khi Instantiate,
        // trước khi bất kỳ MonoBehaviour lifecycle nào (Awake/Start) chạy.
        [Inject]
        public void Construct(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public async UniTask ConnectAsync()
        {
            try
            {
                Debug.Log($"<color=cyan>[Network]</color> Đang kết nối tới Server {ServerIp}:{Port}...");
                _tcpClient = new TcpClient();
                _cts = new CancellationTokenSource();

                await _tcpClient.ConnectAsync(ServerIp, Port);
                _stream = _tcpClient.GetStream();

                Debug.Log("<color=cyan>[Network]</color> Kết nối thành công! Đang gửi ConnectRequest...");

                _ = ReceiveLoopAsync(_cts.Token);

                var request = new ConnectRequestPacket { ClientVersion = "1.0.0" };
                await SendPacketAsync(PacketType.ConnectRequest, request);
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>[Network]</color> Lỗi kết nối: {ex.Message}");

                // Publish để UI biết kết nối thất bại ngay từ bước đầu,
                // không chỉ log rồi im lặng.
                _eventBus.Publish(new ServerConnectionResultEvent(
                    ConnectResult.ServerFull, // tạm dùng, sẽ có ConnectResult.NetworkError riêng khi mở rộng enum
                    $"Không thể kết nối: {ex.Message}"));
            }
        }

        private async UniTask ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (_tcpClient != null && _tcpClient.Connected && !ct.IsCancellationRequested)
                {
                    var envelope = await ReadEnvelopeAsync(ct);
                    if (envelope == null)
                    {
                        Debug.LogWarning("<color=orange>[Network]</color> Server ngắt kết nối.");
                        break;
                    }

                    HandlePacket(envelope);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("<color=yellow>[Network]</color> ReceiveLoop đã bị hủy.");
            }
            catch (ObjectDisposedException)
            {
                Debug.Log("<color=yellow>[Network]</color> Stream đã bị đóng.");
            }
            catch (IOException ioEx)
            {
                if (!ct.IsCancellationRequested)
                    Debug.LogError($"<color=red>[Network]</color> I/O Error: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Debug.LogError($"<color=red>[Network]</color> Mất kết nối tới Server: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private async UniTask<PacketEnvelope> ReadEnvelopeAsync(CancellationToken ct)
        {
            try
            {
                var lengthBuffer = new byte[4];
                int read = await _stream.ReadAsync(lengthBuffer, 0, 4, ct);
                if (read < 4) return null;

                int length = BitConverter.ToInt32(lengthBuffer, 0);
                var payloadBuffer = new byte[length];
                int totalRead = 0;

                while (totalRead < length)
                {
                    int bytesRead = await _stream.ReadAsync(payloadBuffer, totalRead, length - totalRead, ct);
                    if (bytesRead == 0) return null;
                    totalRead += bytesRead;
                }

                var json = Encoding.UTF8.GetString(payloadBuffer);
                return JsonSerializer.Deserialize<PacketEnvelope>(json);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (IOException ioEx)
            {
                if (!_isDisconnecting)
                    Debug.LogError($"<color=red>[Network]</color> Lỗi I/O bất ngờ: {ioEx.Message}");
                return null;
            }
        }

        // Không còn try-catch nuốt lỗi ở đây - để exception bay lên cho
        // caller (ConnectAsync) quyết định xử lý, tránh tình trạng
        // "gửi thất bại nhưng vẫn log như đã gửi thành công".
        private async UniTask SendPacketAsync<T>(PacketType type, T payload)
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            var envelope = new PacketEnvelope { Type = type, Payload = payloadJson };
            var envelopeJson = JsonSerializer.Serialize(envelope);
            var envelopeBytes = Encoding.UTF8.GetBytes(envelopeJson);
            var lengthPrefix = BitConverter.GetBytes(envelopeBytes.Length);

            await _stream.WriteAsync(lengthPrefix, 0, 4);
            await _stream.WriteAsync(envelopeBytes, 0, envelopeBytes.Length);
        }

        private void HandlePacket(PacketEnvelope envelope)
        {
            switch (envelope.Type)
            {
                case PacketType.ConnectResponse:
                    var response = JsonSerializer.Deserialize<ConnectResponsePacket>(envelope.Payload);

                    if (response is null)
                    {
                        Debug.LogWarning("<color=orange>[Network]</color> ConnectResponse payload không hợp lệ.");
                        return;
                    }

                    Debug.Log($"<color=green>[Network]</color> Server phản hồi: {response.Message} ({response.Result})");

                    // Đây là điểm mấu chốt: publish qua EventBus thay vì chỉ log.
                    // UI sẽ subscribe ServerConnectionResultEvent để cập nhật màn hình Login.
                    _eventBus.Publish(new ServerConnectionResultEvent(response.Result, response.Message));
                    break;

                default:
                    Debug.Log($"<color=yellow>[Network]</color> Nhận packet chưa được xử lý: {envelope.Type}");
                    break;
            }
        }

        public void Disconnect()
        {
            _isDisconnecting = true;

            try
            {
                try
                {
                    if (_cts != null && !_cts.IsCancellationRequested)
                        _cts.Cancel();
                }
                catch (ObjectDisposedException) { }
                finally
                {
                    _cts?.Dispose();
                }

                _stream?.Dispose();
                _stream = null;

                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;

                Debug.Log("<color=yellow>[Network]</color> Đã ngắt kết nối.");

                // Publish để UI biết mất kết nối, không chỉ Client tự biết.
                _eventBus?.Publish(new ServerDisconnectedEvent());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=orange>[Network]</color> Exception khi disconnect: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}