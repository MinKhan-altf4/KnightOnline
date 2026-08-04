using System.Numerics;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Networking;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Combat;

public sealed class MonsterCombatService(
    MonsterService monsters,
    ICombatStatsProvider statsProvider,
    IDamageCalculator damageCalculator,
    IWorldMovementResolver movementResolver,
    CombatOptions options)
{
    private readonly TimeSpan _attackCooldown =
        TimeSpan.FromMilliseconds(options.AttackCooldownMilliseconds);

    public MonsterAttackResolution Attack(
        ClientConnection attacker,
        int monsterId,
        DateTime utcNow)
    {
        var session = attacker.PlayerSession;
        if (session == null)
            return new MonsterAttackResolution(
                AttackResultStatus.NoSelectedCharacter,
                monsterId);

        if (!session.IsAlive)
            return new MonsterAttackResolution(
                AttackResultStatus.PlayerDead,
                monsterId);

        MonsterSnapshot? monster = monsters.GetSnapshot(monsterId);
        if (monster == null)
            return new MonsterAttackResolution(
                AttackResultStatus.MonsterNotFound,
                monsterId);

        if (!string.Equals(
                session.MapDefinitionId,
                monster.MapDefinitionId,
                StringComparison.Ordinal))
            return new MonsterAttackResolution(
                AttackResultStatus.WrongMap,
                monsterId);

        if (!monster.IsAlive)
            return new MonsterAttackResolution(
                AttackResultStatus.MonsterDead,
                monsterId);

        session.AdvancePosition(utcNow, movementResolver);
        var monsterPosition = new Vector2(
            monster.SpawnPosition.X,
            monster.SpawnPosition.Y);
        if (Vector2.Distance(session.Position, monsterPosition) >
            options.AttackRange)
            return new MonsterAttackResolution(
                AttackResultStatus.OutOfRange,
                monsterId);

        TimeSpan cooldownRemaining =
            session.GetAttackCooldownRemaining(utcNow);
        if (cooldownRemaining > TimeSpan.Zero)
            return new MonsterAttackResolution(
                AttackResultStatus.Cooldown,
                monsterId,
                CooldownRemaining: cooldownRemaining);

        CombatStats stats = statsProvider.GetFor(attacker);
        int damage = damageCalculator.Calculate(stats);

        MonsterDamageResult result =
            monsters.ApplyDamage(monsterId, damage, utcNow);

        if (result.Status == MonsterDamageStatus.Success)
            session.CommitAttack(utcNow, _attackCooldown);

        return result.Status switch
        {
            MonsterDamageStatus.Success => new MonsterAttackResolution(
                AttackResultStatus.Success,
                monsterId,
                result),
            MonsterDamageStatus.MonsterNotFound => new MonsterAttackResolution(
                AttackResultStatus.MonsterNotFound,
                monsterId),
            MonsterDamageStatus.MonsterAlreadyDead => new MonsterAttackResolution(
                AttackResultStatus.MonsterDead,
                monsterId),
            _ => new MonsterAttackResolution(
                AttackResultStatus.MonsterNotFound,
                monsterId),
        };
    }
}
