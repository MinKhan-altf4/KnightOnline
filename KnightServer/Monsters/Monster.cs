namespace KnightOnline.Server.Monsters;

public sealed class Monster
{
    public Monster(
        int monsterId,
        MonsterDefinition definition,
        WorldPosition spawnPosition)
    {
        if (monsterId <= 0)
            throw new ArgumentOutOfRangeException(nameof(monsterId));

        MonsterId = monsterId;
        Definition = definition ??
            throw new ArgumentNullException(nameof(definition));
        SpawnPosition = spawnPosition;
        CurrentHealth = definition.MaximumHealth;
    }

    public int MonsterId { get; }
    public MonsterDefinition Definition { get; }
    public WorldPosition SpawnPosition { get; }
    public int CurrentHealth { get; private set; }
    public DateTime? RespawnAtUtc { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    public MonsterDamageResult ApplyDamage(int requestedDamage, DateTime utcNow)
    {
        if (requestedDamage <= 0)
        {
            return new MonsterDamageResult(
                MonsterDamageStatus.InvalidDamage,
                MonsterId,
                0,
                CurrentHealth,
                false);
        }

        if (!IsAlive)
        {
            return new MonsterDamageResult(
                MonsterDamageStatus.MonsterAlreadyDead,
                MonsterId,
                0,
                0,
                false);
        }

        int appliedDamage = Math.Min(requestedDamage, CurrentHealth);
        CurrentHealth -= appliedDamage;
        bool wasKilled = CurrentHealth == 0;

        if (wasKilled)
            RespawnAtUtc = utcNow + Definition.RespawnDelay;

        return new MonsterDamageResult(
            MonsterDamageStatus.Success,
            MonsterId,
            appliedDamage,
            CurrentHealth,
            wasKilled);
    }

    public bool TryRespawn(DateTime utcNow)
    {
        if (IsAlive || RespawnAtUtc == null || utcNow < RespawnAtUtc)
            return false;

        CurrentHealth = Definition.MaximumHealth;
        RespawnAtUtc = null;
        return true;
    }

    public MonsterSnapshot CreateSnapshot()
    {
        return new MonsterSnapshot(
            MonsterId,
            Definition.DefinitionId,
            Definition.Name,
            Definition.Level,
            CurrentHealth,
            Definition.MaximumHealth,
            IsAlive,
            SpawnPosition,
            RespawnAtUtc);
    }
}
