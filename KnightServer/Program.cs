using System.Net;
using System.Net.Sockets;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Networking;
using KnightOnline.Server.Networking.Handlers;
using KnightOnline.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KnightOnline.Server;

public static class Program
{
    private const int Port = 7777;

    public static async Task Main(string[] args)
    {
        DbContextOptions<KnightDbContext> databaseOptions =
            await ConfigureDatabaseAsync();

        var characterRepository = new CharacterRepository(
            databaseOptions,
            DatabaseConfiguration.DevelopmentAccountKey);
        await characterRepository.EnsureAccountExistsAsync();

        MonsterService monsterService = CreateMonsterWorld();

        var packetDispatcher = new PacketDispatcher(
        [
            new ConnectPacketHandler(),
            new CreateCharacterPacketHandler(characterRepository),
            new ListCharactersPacketHandler(characterRepository),
            new ListMonstersPacketHandler(monsterService),
        ]);

        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        Console.WriteLine($"[Server] Listening on port {Port}.");
        Console.WriteLine(
            $"[Monster] Loaded {monsterService.GetSnapshots().Count} monster instance(s).");

        while (true)
        {
            TcpClient tcpClient = await listener.AcceptTcpClientAsync();
            var connection = new ClientConnection(tcpClient, packetDispatcher);
            _ = connection.RunAsync();
        }
    }

    private static async Task<DbContextOptions<KnightDbContext>>
        ConfigureDatabaseAsync()
    {
        var configuration = DatabaseConfiguration.Build();
        string connectionString =
            DatabaseConfiguration.GetRequiredConnectionString(configuration);
        var databaseOptions = new DbContextOptionsBuilder<KnightDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new KnightDbContext(databaseOptions);
        await db.Database.MigrateAsync();

        return databaseOptions;
    }

    private static MonsterService CreateMonsterWorld()
    {
        var monsterService = new MonsterService();
        monsterService.Spawn(
            new MonsterDefinition(
                definitionId: 1,
                name: "Training Wolf",
                level: 1,
                maximumHealth: 50,
                respawnDelay: TimeSpan.FromSeconds(10)),
            new WorldPosition(2f, 2f));

        return monsterService;
    }
}
