using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Combat;
using KnightOnline.Server.Monsters;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class AttackMonsterPacketHandler(
    MonsterCombatService combatService,
    MonsterService monsterService,
    ConnectionRegistry connections) : IPacketHandler
{
    public PacketType PacketType => PacketType.AttackMonsterRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        AttackMonsterRequestPacket? request =
            JsonSerializer.Deserialize<AttackMonsterRequestPacket>(payload);

        if (request == null)
            return;

        MonsterAttackResolution resolution = combatService.Attack(
            connection,
            request.MonsterId,
            DateTime.UtcNow);

        MonsterDamageResult? result = resolution.DamageResult;
        await connection.SendAsync(
            PacketType.AttackResult,
            new AttackResultPacket(
                resolution.Status,
                resolution.MonsterId,
                result?.AppliedDamage ?? 0,
                (int)Math.Ceiling(
                    resolution.CooldownRemaining?.TotalMilliseconds ?? 0)),
            cancellationToken);

        if (resolution.Status != AttackResultStatus.Success ||
            result == null)
            return;

        MonsterSnapshot? snapshot =
            monsterService.GetSnapshot(result.MonsterId);

        if (snapshot == null)
            return;

        await connections.BroadcastAsync(
            PacketType.MonsterHealthChanged,
            new MonsterHealthChangedPacket(
                result.MonsterId,
                result.RemainingHealth,
                snapshot.MaximumHealth));

        if (result.WasKilled)
        {
            await connections.BroadcastAsync(
                PacketType.MonsterDied,
                new MonsterDiedPacket(result.MonsterId));
        }
    }
}
