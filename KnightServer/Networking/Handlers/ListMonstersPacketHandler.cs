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
        MonsterSnapshotPacket[] monsters = monsterService.GetSnapshots()
            .Select(snapshot => new MonsterSnapshotPacket(
                snapshot.MonsterId,
                snapshot.DefinitionId,
                snapshot.Name,
                snapshot.Level,
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
