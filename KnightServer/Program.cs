using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server;

public static class Program
{
    private const int Port = 7777;

    public static async Task Main(string[] args)
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();

        Console.WriteLine($"[Server] Đang lắng nghe tại port {Port}...");

        // Vòng lặp chính: chấp nhận kết nối mới liên tục.
        // Mỗi client được xử lý trên 1 Task riêng, không chặn việc nhận client tiếp theo.
        while (true)
        {
            var tcpClient = await listener.AcceptTcpClientAsync();
            Console.WriteLine("[Server] Có client mới kết nối.");

            // "Fire and forget" có kiểm soát - không await ở đây để không chặn vòng lặp Accept.
            _ = HandleClientAsync(tcpClient);
        }
    }

    private static async Task HandleClientAsync(TcpClient tcpClient)
    {
        using (tcpClient)
        {
            var stream = tcpClient.GetStream();

            try
            {
                // Vòng lặp liên tục để xử lý nhiều packet từ client
                while (tcpClient.Connected)
                {
                    var envelope = await ReadEnvelopeAsync(stream);

                    if (envelope is null)
                    {
                        Console.WriteLine("[Server] Không đọc được envelope, đóng kết nối.");
                        break;
                    }

                    await HandlePacketAsync(stream, envelope);
                }
            }
            catch (Exception ex)
            {
                // Bắt lỗi ở tầng ngoài cùng - 1 client gửi dữ liệu hỏng
                // không được phép làm sập toàn bộ server.
                Console.WriteLine($"[Server] Lỗi xử lý client: {ex.Message}");
            }
        }

        Console.WriteLine("[Server] Client đã ngắt kết nối.");
    }

    private static async Task HandlePacketAsync(NetworkStream stream, PacketEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case PacketType.ConnectRequest:
                var request = JsonSerializer.Deserialize<ConnectRequestPacket>(envelope.Payload);

                if (request is null)
                {
                    Console.WriteLine("[Server] ConnectRequest payload không hợp lệ.");
                    return;
                }

                Console.WriteLine($"[Server] Nhận ConnectRequest từ client version {request.ClientVersion}");

                var response = new ConnectResponsePacket
                {
                    Result = ConnectResult.Success,
                    Message = "Chào mừng đến với KnightOnline!"
                };

                await SendEnvelopeAsync(stream, PacketType.ConnectResponse, response);
                break;

            default:
                Console.WriteLine($"[Server] Nhận packet chưa được xử lý: {envelope.Type}");
                break;
        }
    }

    private static async Task<PacketEnvelope?> ReadEnvelopeAsync(NetworkStream stream)
    {
        // Giao thức đơn giản cho giai đoạn học tập: đọc 4 byte đầu là độ dài (int),
        // sau đó đọc đúng số byte đó làm nội dung JSON của PacketEnvelope.
        // Đây gọi là "length-prefixed framing" - cách phổ biến nhất để biết
        // khi nào 1 message kết thúc trên luồng TCP (vốn không có khái niệm "message", chỉ có byte stream).
        var lengthBuffer = new byte[4];
        int read = await stream.ReadAsync(lengthBuffer, 0, 4);
        if (read < 4) return null;

        int length = BitConverter.ToInt32(lengthBuffer, 0);
        var payloadBuffer = new byte[length];
        int totalRead = 0;

        while (totalRead < length)
        {
            int bytesRead = await stream.ReadAsync(payloadBuffer, totalRead, length - totalRead);
            if (bytesRead == 0) return null;
            totalRead += bytesRead;
        }

        var json = Encoding.UTF8.GetString(payloadBuffer);
        return JsonSerializer.Deserialize<PacketEnvelope>(json);
    }

    private static async Task SendEnvelopeAsync<T>(NetworkStream stream, PacketType type, T payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var envelope = new PacketEnvelope { Type = type, Payload = payloadJson };
        var envelopeJson = JsonSerializer.Serialize(envelope);
        var envelopeBytes = Encoding.UTF8.GetBytes(envelopeJson);

        var lengthPrefix = BitConverter.GetBytes(envelopeBytes.Length);

        await stream.WriteAsync(lengthPrefix, 0, 4);
        await stream.WriteAsync(envelopeBytes, 0, envelopeBytes.Length);
    }
}