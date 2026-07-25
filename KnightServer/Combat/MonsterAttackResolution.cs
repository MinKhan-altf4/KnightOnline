using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Monsters;

namespace KnightOnline.Server.Combat;

public sealed record MonsterAttackResolution(
    AttackResultStatus Status,
    int MonsterId,
    MonsterDamageResult? DamageResult = null,
    TimeSpan? CooldownRemaining = null);
