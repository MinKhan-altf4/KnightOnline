using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using KnightOnline.Client.Shared.Packets; // Sử dụng chung cấu trúc gói tin với Server

namespace KnightOnline.Client.Network
{
    public class NetworkClient : MonoBehaviour
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isDisconnecting = false; // Flag để đánh dấu disconnection đang xảy ra
        private const string ServerIp = "127.0.0.1"; // Chạy giả lập trên cùng máy tính
        private const int Port = 7777;

        // Khởi tạo kết nối tới Server
        public async UniTask ConnectAsync()
        {
            try
            {
                Debug.Log($"<color=cyan>[Network]</color> Đang kết nối tới Server {ServerIp}:{Port}...");
                _tcpClient = new TcpClient();
                _cts = new CancellationTokenSource();
                
                // Kết nối bất đồng bộ, không làm đơ giật màn hình Unity
                await _tcpClient.ConnectAsync(ServerIp, Port);
                _stream = _tcpClient.GetStream();
                
                Debug.Log("<color=cyan>[Network]</color> Kết nối thành công! Đang gửi ConnectRequest...");

                // Bắt đầu vòng lặp lắng nghe phản hồi từ Server (chạy ngầm fire-and-forget)
                Debug.Log("<color=cyan>[Network]</color> Khởi động ReceiveLoop (fire-and-forget)...");
                _ = ReceiveLoopAsync(_cts.Token);

                // Gửi thử 1 packet yêu cầu kết nối
                var request = new ConnectRequestPacket { ClientVersion = "1.0.0" };
                Debug.Log("<color=cyan>[Network]</color> Gửi ConnectRequest...");
                await SendPacketAsync(PacketType.ConnectRequest, request);
                Debug.Log("<color=green>[Network]</color> ConnectRequest đã gửi xong.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>[Network]</color> Lỗi kết nối: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        // Vòng lặp nhận dữ liệu liên tục từ Server
        private async UniTask ReceiveLoopAsync(CancellationToken ct)
        {
            Debug.Log("<color=cyan>[Network]</color> ReceiveLoop đã bắt đầu...");
            try
            {
                while (_tcpClient != null && _tcpClient.Connected && !ct.IsCancellationRequested)
                {
                    Debug.Log("<color=cyan>[Network]</color> Chờ dữ liệu từ Server...");
                    var envelope = await ReadEnvelopeAsync(ct);
                    if (envelope == null)
                    {
                        Debug.LogWarning("<color=orange>[Network]</color> Server ngắt kết nối (hoặc gửi dữ liệu rỗng).");
                        break;
                    }

                    Debug.Log($"<color=cyan>[Network]</color> Nhận packet type: {envelope.Type}");
                    HandlePacket(envelope);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("<color=yellow>[Network]</color> ReceiveLoop đã bị hủy (cancellation token)");
            }
            catch (ObjectDisposedException)
            {
                Debug.Log("<color=yellow>[Network]</color> Stream đã bị đóng");
            }
            catch (IOException ioEx)
            {
                // I/O errors khi đang shutdown là normal
                if (!ct.IsCancellationRequested)
                {
                    Debug.LogError($"<color=red>[Network]</color> I/O Error: {ioEx.Message}");
                }
                else
                {
                    Debug.Log("<color=yellow>[Network]</color> I/O error khi shutdown (normal)");
                }
            }
            catch (Exception ex)
            {
                // Nếu đang shutdown, ignore exception
                if (!ct.IsCancellationRequested)
                {
                    Debug.LogError($"<color=red>[Network]</color> Mất kết nối tới Server: {ex.Message}\nStackTrace: {ex.StackTrace}");
                }
            }
            finally
            {
                Debug.Log("<color=yellow>[Network]</color> ReceiveLoop kết thúc, đang disconnect...");
                Disconnect();
            }
        }

        // Đọc dữ liệu (Length-prefixed framing giống hệt cơ chế Server)
        private async UniTask<PacketEnvelope> ReadEnvelopeAsync(CancellationToken ct)
        {
            try
            {
                var lengthBuffer = new byte[4];
                int read = await _stream.ReadAsync(lengthBuffer, 0, 4, ct);
                Debug.Log($"<color=cyan>[Network]</color> ReadAsync trả về {read} bytes cho length header");
                
                if (read < 4)
                {
                    Debug.LogWarning($"<color=orange>[Network]</color> Chỉ đọc được {read} bytes, kỳ vọng 4 bytes. Kết nối bị đóng?");
                    return null;
                }

                int length = BitConverter.ToInt32(lengthBuffer, 0);
                Debug.Log($"<color=cyan>[Network]</color> Packet length: {length} bytes");
                
                var payloadBuffer = new byte[length];
                int totalRead = 0;

                while (totalRead < length)
                {
                    int bytesRead = await _stream.ReadAsync(payloadBuffer, totalRead, length - totalRead, ct);
                    if (bytesRead == 0)
                    {
                        Debug.LogWarning($"<color=orange>[Network]</color> Không đọc được payload, bytesRead = 0");
                        return null;
                    }
                    totalRead += bytesRead;
                    Debug.Log($"<color=cyan>[Network]</color> Đọc {bytesRead} bytes, tổng cộng {totalRead}/{length}");
                }

                var json = Encoding.UTF8.GetString(payloadBuffer);
                Debug.Log($"<color=cyan>[Network]</color> JSON nhận được: {json}");
                return JsonSerializer.Deserialize<PacketEnvelope>(json);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("<color=yellow>[Network]</color> ReadEnvelopeAsync bị cancel");
                return null;
            }
            catch (ObjectDisposedException)
            {
                Debug.Log("<color=yellow>[Network]</color> Stream đã bị dispose");
                return null;
            }
            catch (IOException ioEx)
            {
                // Nếu đang disconnect, IOException này là expected (stream bị dispose)
                if (_isDisconnecting)
                {
                    Debug.Log("<color=yellow>[Network]</color> I/O error khi disconnect (expected): " + ioEx.Message);
                }
                else
                {
                    // Nếu KHÔNG disconnect, đây là lỗi thực sự
                    Debug.LogError($"<color=red>[Network]</color> Lỗi I/O bất ngờ: {ioEx.Message}\nStackTrace: {ioEx.StackTrace}");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>[Network]</color> Lỗi trong ReadEnvelopeAsync: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return null;
            }
        }

        // Đóng gói và gửi dữ liệu lên Server
        private async UniTask SendPacketAsync<T>(PacketType type, T payload)
        {
            try
            {
                var payloadJson = JsonSerializer.Serialize(payload);
                Debug.Log($"<color=cyan>[Network]</color> Payload JSON: {payloadJson}");
                
                var envelope = new PacketEnvelope { Type = type, Payload = payloadJson };
                var envelopeJson = JsonSerializer.Serialize(envelope);
                Debug.Log($"<color=cyan>[Network]</color> Envelope JSON: {envelopeJson}");
                
                var envelopeBytes = Encoding.UTF8.GetBytes(envelopeJson);
                var lengthPrefix = BitConverter.GetBytes(envelopeBytes.Length);

                Debug.Log($"<color=cyan>[Network]</color> Gửi length prefix: {envelopeBytes.Length} bytes");
                await _stream.WriteAsync(lengthPrefix, 0, 4);
                
                Debug.Log($"<color=cyan>[Network]</color> Gửi envelope payload...");
                await _stream.WriteAsync(envelopeBytes, 0, envelopeBytes.Length);
                
                Debug.Log($"<color=green>[Network]</color> Packet đã gửi thành công!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>[Network]</color> Lỗi khi gửi packet: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        // Xử lý các loại packet Server gửi về
        private void HandlePacket(PacketEnvelope envelope)
        {
            switch (envelope.Type)
            {
                case PacketType.ConnectResponse:
                    var response = JsonSerializer.Deserialize<ConnectResponsePacket>(envelope.Payload);
                    Debug.Log($"<color=green>[Network]</color> Server phản hồi: {response.Message} (Trạng thái: {response.Result})");
                    break;
                default:
                    Debug.Log($"<color=yellow>[Network]</color> Nhận packet chưa được xử lý: {envelope.Type}");
                    break;
            }
        }

        public void Disconnect()
        {
            Debug.Log("<color=yellow>[Network]</color> Disconnect được gọi, cleanup resources...");
            
            // Set flag trước để ReadEnvelopeAsync biết đang disconnect
            _isDisconnecting = true;
            
            try
            {
                // Cancel token để dừng ReceiveLoop
                try
                {
                    if (_cts != null && !_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // _cts đã được dispose, không sao
                    Debug.Log("<color=cyan>[Network]</color> _cts đã dispose rồi");
                }
                finally
                {
                    _cts?.Dispose();
                }
                
                // Dispose stream trước
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
                
                // Close TCP client
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient.Dispose();
                    _tcpClient = null;
                }
                
                Debug.Log("<color=yellow>[Network]</color> Đã ngắt kết nối.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=orange>[Network]</color> Exception khi disconnect: {ex.Message}");
            }
        }

        // Cleanup khi scene unload hoặc app quit
        private void OnDestroy()
        {
            Debug.Log("<color=yellow>[Network]</color> NetworkClient OnDestroy, cleanup...");
            Disconnect();
        }
    }
}