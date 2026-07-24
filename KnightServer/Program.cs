using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KnightOnline.Server;

public static class Program
{
    private const int Port = 7777;

    public static async Task Main(string[] args)
    {
        var configuration = DatabaseConfiguration.Build();
        var connectionString =
            DatabaseConfiguration.GetRequiredConnectionString(configuration);
        var databaseOptions = new DbContextOptionsBuilder<KnightDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var db = new KnightDbContext(databaseOptions))
            await db.Database.MigrateAsync();

        var characterRepository = new CharacterRepository(
            databaseOptions,
            DatabaseConfiguration.DevelopmentAccountKey);
        await characterRepository.EnsureAccountExistsAsync();

        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        Console.WriteLine($"[Server] Listening on port {Port}.");

        while (true)
            _ = HandleClientAsync(
                await listener.AcceptTcpClientAsync(),
                characterRepository);
    }

    private static async Task HandleClientAsync(
        TcpClient tcpClient,
        CharacterRepository characterRepository)
    {
        using (tcpClient)
        using (var stream = tcpClient.GetStream())
        {
            try
            {
                while (tcpClient.Connected)
                {
                    var envelope = await ReadEnvelopeAsync(stream);
                    if (envelope == null) break;
                    await HandlePacketAsync(stream, envelope, characterRepository);
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
        CharacterRepository characterRepository)
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
                else
                    createResponse = await characterRepository.CreateAsync(name);

                await SendEnvelopeAsync(stream, PacketType.CreateCharacterResponse, createResponse);
                break;

            case PacketType.ListCharactersRequest:
                var characters = await characterRepository.ListAsync();
                await SendEnvelopeAsync(stream, PacketType.ListCharactersResponse,
                    new ListCharactersResponsePacket(characters));
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
