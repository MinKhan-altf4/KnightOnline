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
        Console.WriteLine($"[Server] Listening on port {Port}.");

        while (true)
            _ = HandleClientAsync(await listener.AcceptTcpClientAsync());
    }

    private static async Task HandleClientAsync(TcpClient tcpClient)
    {
        var characters = new Dictionary<string, CharacterSummaryPacket>(StringComparer.OrdinalIgnoreCase);
        var nextCharacterId = 1; // Đơn giản, tăng dần theo từng kết nối - đủ cho prototype in-memory.

        using (tcpClient)
        using (var stream = tcpClient.GetStream())
        {
            try
            {
                while (tcpClient.Connected)
                {
                    var envelope = await ReadEnvelopeAsync(stream);
                    if (envelope == null) break;
                    await HandlePacketAsync(stream, envelope, characters, () => nextCharacterId++);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Client processing error: {ex.Message}");
            }
        }
    }

    private static async Task HandlePacketAsync(
        NetworkStream stream,
        PacketEnvelope envelope,
        Dictionary<string, CharacterSummaryPacket> characters,
        Func<int> generateId)
    {
        switch (envelope.Type)
        {
            case PacketType.ConnectRequest:
                var connect = JsonSerializer.Deserialize<ConnectRequestPacket>(envelope.Payload);
                if (connect == null) return;
                await SendEnvelopeAsync(stream, PacketType.ConnectResponse,
                    new ConnectResponsePacket(ConnectResult.Success, "Welcome to KnightOnline!"));
                break;

            case PacketType.CreateCharacterRequest:
                var create = JsonSerializer.Deserialize<CreateCharacterRequestPacket>(envelope.Payload);
                if (create == null) return;

                var name = create.CharacterName?.Trim() ?? string.Empty;
                CreateCharacterResponsePacket createResponse;
                if (name.Length == 0)
                    createResponse = new(CreateCharacterResult.NameEmpty, "Character name cannot be empty.");
                else if (name.Length > 20)
                    createResponse = new(CreateCharacterResult.NameTooLong, "Character name is limited to 20 characters.");
                else if (characters.ContainsKey(name))
                    createResponse = new(CreateCharacterResult.NameAlreadyTaken, "That character name already exists.");
                else
                {
                    characters.Add(name, new CharacterSummaryPacket(name, generateId(), level: 1));
                    createResponse = new(CreateCharacterResult.Success, name);
                }

                await SendEnvelopeAsync(stream, PacketType.CreateCharacterResponse, createResponse);
                break;

            case PacketType.ListCharactersRequest:
                await SendEnvelopeAsync(stream, PacketType.ListCharactersResponse,
                    new ListCharactersResponsePacket(characters.Values.ToArray()));
                break;
        }
    }

    private static async Task<PacketEnvelope?> ReadEnvelopeAsync(NetworkStream stream)
    {
        var lengthBuffer = new byte[4];
        if (!await ReadExactlyAsync(stream, lengthBuffer)) return null;
        var length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length <= 0 || length > 1024 * 1024) throw new InvalidDataException("Invalid packet length.");

        var payloadBuffer = new byte[length];
        if (!await ReadExactlyAsync(stream, payloadBuffer)) return null;
        return JsonSerializer.Deserialize<PacketEnvelope>(Encoding.UTF8.GetString(payloadBuffer));
    }

    private static async Task<bool> ReadExactlyAsync(NetworkStream stream, byte[] buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead));
            if (read == 0) return false;
            totalRead += read;
        }
        return true;
    }

    private static async Task SendEnvelopeAsync<T>(NetworkStream stream, PacketType type, T payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var envelopeJson = JsonSerializer.Serialize(new PacketEnvelope(type, payloadJson));
        var bytes = Encoding.UTF8.GetBytes(envelopeJson);
        await stream.WriteAsync(BitConverter.GetBytes(bytes.Length));
        await stream.WriteAsync(bytes);
    }
}