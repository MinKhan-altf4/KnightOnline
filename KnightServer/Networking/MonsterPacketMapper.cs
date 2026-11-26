using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Monsters;

namespace KnightOnline.Server.Networking;

/// <summary>Single place that turns monster domain state into wire packets.</summary>
public static class MonsterPacketMapper
{
    public static MonsterSnapshotPacket ToPacket(MonsterSnapshot snapshot) =>
        new(
            snapshot.MonsterId,
            snapshot.DefinitionId,
            snapshot.Name,
            snapshot.Level,
            snapshot.MapDefinitionId,
            snapshot.CurrentHealth,
            snapshot.MaximumHealth,
            snapshot.IsAlive,
            snapshot.Position.X,
            snapshot.Position.Y,
            snapshot.MoveTarget.X,
            snapshot.MoveTarget.Y,
            ToMilliseconds(snapshot.MoveRemaining));

    public static MonsterMovedPacket ToMovedPacket(MonsterMovementLeg leg) =>
        new(
            leg.MonsterId,
            leg.From.X,
            leg.From.Y,
            leg.To.X,
            leg.To.Y,
            ToMilliseconds(leg.Duration));

    private static int ToMilliseconds(TimeSpan value) =>
        value <= TimeSpan.Zero
            ? 0
            : (int)Math.Min(int.MaxValue, Math.Ceiling(value.TotalMilliseconds));
}
