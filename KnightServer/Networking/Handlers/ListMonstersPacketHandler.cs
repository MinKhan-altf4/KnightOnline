using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Monsters;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class ListMonstersPacketHandler(
    MonsterService monsterService) : IPacketHandler
{
    public PacketType PacketType => PacketType.ListMonstersRequest;

    public Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        string mapDefinitionId =
            connection.PlayerSession?.MapDefinitionId ?? string.Empty;
        MonsterSnapshotPacket[] monsters = monsterService.GetSnapshots()
            .Where(snapshot =>
                snapshot.MapDefinitionId == mapDefinitionId)
            .Select(snapshot => new MonsterSnapshotPacket(
                snapshot.MonsterId,
                snapshot.DefinitionId,
                snapshot.Name,
                snapshot.Level,
                snapshot.MapDefinitionId,
                snapshot.CurrentHealth,
                snapshot.MaximumHealth,
                snapshot.IsAlive,
                snapshot.SpawnPosition.X,
                snapshot.SpawnPosition.Y))
            .ToArray();

        return connection.SendAsync(
            PacketType.ListMonstersResponse,
            new ListMonstersResponsePacket(monsters),
            cancellationToken);
    }
}
