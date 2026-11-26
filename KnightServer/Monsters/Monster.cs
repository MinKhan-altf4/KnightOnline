namespace KnightOnline.Server.Monsters;

public sealed class Monster
{
    public Monster(
        int monsterId,
        MonsterDefinition definition,
        string mapDefinitionId,
        WorldPosition spawnPosition)
    {
        if (monsterId <= 0)
            throw new ArgumentOutOfRangeException(nameof(monsterId));

        MonsterId = monsterId;
        Definition = definition ??
            throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(mapDefinitionId))
            throw new ArgumentException(
                "Monster map definition is required.",
                nameof(mapDefinitionId));
        MapDefinitionId = mapDefinitionId.Trim();
        SpawnPosition = spawnPosition;
        CurrentHealth = definition.MaximumHealth;
        LifeId = Guid.NewGuid();
        RestAt(spawnPosition);
    }

    // Current movement leg. Position is a pure function of time along it, so
    // collision, attack range and snapshots agree without a position tick.
    private WorldPosition _legFrom;
    private WorldPosition _legTo;
    private DateTime _legStartUtc;
    private TimeSpan _legDuration;
    /// <summary>When the next leg may start; default = not scheduled yet.</summary>
    private DateTime _nextLegUtc;

    public int MonsterId { get; }
    public MonsterDefinition Definition { get; }
    public string MapDefinitionId { get; }
    public WorldPosition SpawnPosition { get; }
    public int CurrentHealth { get; private set; }
    public DateTime? RespawnAtUtc { get; private set; }
    public bool IsAlive => CurrentHealth > 0;
    public Guid LifeId { get; private set; }

    public MonsterDamageResult ApplyDamage(int requestedDamage, DateTime utcNow)
    {
        if (requestedDamage <= 0)
        {
            return new MonsterDamageResult(
                MonsterDamageStatus.InvalidDamage,
                MonsterId,
                0,
                CurrentHealth,
                false,
                LifeId);
        }

        if (!IsAlive)
        {
            return new MonsterDamageResult(
                MonsterDamageStatus.MonsterAlreadyDead,
                MonsterId,
                0,
                0,
                false,
                LifeId);
        }

        int appliedDamage = Math.Min(requestedDamage, CurrentHealth);
        CurrentHealth -= appliedDamage;
        bool wasKilled = CurrentHealth == 0;

        if (wasKilled)
        {
            RespawnAtUtc = utcNow + Definition.RespawnDelay;
            // Stop where it fell; the corpse does not keep walking.
            RestAt(PositionAt(utcNow));
        }

        return new MonsterDamageResult(
            MonsterDamageStatus.Success,
            MonsterId,
            appliedDamage,
            CurrentHealth,
            wasKilled,
            LifeId);
    }

    public bool TryRespawn(DateTime utcNow)
    {
        if (IsAlive || RespawnAtUtc == null || utcNow < RespawnAtUtc)
            return false;

        CurrentHealth = Definition.MaximumHealth;
        LifeId = Guid.NewGuid();
        RespawnAtUtc = null;
        RestAt(SpawnPosition);
        return true;
    }

    /// <summary>Where the monster is at <paramref name="utcNow"/>.</summary>
    public WorldPosition PositionAt(DateTime utcNow)
    {
        if (_legDuration <= TimeSpan.Zero || utcNow >= _legStartUtc + _legDuration)
            return _legTo;
        if (utcNow <= _legStartUtc)
            return _legFrom;

        float t = (float)((utcNow - _legStartUtc).TotalSeconds / _legDuration.TotalSeconds);
        return new WorldPosition(
            _legFrom.X + (_legTo.X - _legFrom.X) * t,
            _legFrom.Y + (_legTo.Y - _legFrom.Y) * t);
    }

    /// <summary>
    /// Starts the next wander leg when the monster is alive, resting and its
    /// rest is over. The first call only schedules a random rest, so monsters
    /// loaded together do not all set off on the same tick.
    /// </summary>
    /// <param name="clamp">Keeps the target inside the map's walkable bounds.</param>
    public MonsterMovementLeg? TryStartWanderLeg(
        DateTime utcNow,
        MonsterWanderPolicy policy,
        Random random,
        Func<string, WorldPosition, WorldPosition> clamp)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(clamp);

        if (!IsAlive || utcNow < _legStartUtc + _legDuration)
            return null;
        if (_nextLegUtc == default)
        {
            _nextLegUtc = utcNow + policy.NextIdle(random);
            return null;
        }
        if (utcNow < _nextLegUtc)
            return null;

        WorldPosition from = PositionAt(utcNow);
        // Uniform point in the disc around the spawn (sqrt keeps it uniform).
        double angle = random.NextDouble() * Math.PI * 2;
        double distance = policy.Radius * Math.Sqrt(random.NextDouble());
        WorldPosition target = clamp(MapDefinitionId, new WorldPosition(
            SpawnPosition.X + (float)(Math.Cos(angle) * distance),
            SpawnPosition.Y + (float)(Math.Sin(angle) * distance)));

        float dx = target.X - from.X;
        float dy = target.Y - from.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        TimeSpan duration = TimeSpan.FromSeconds(length / policy.Speed);
        _nextLegUtc = utcNow + duration + policy.NextIdle(random);
        if (length < MonsterWanderPolicy.MinimumLegDistance)
            return null;

        _legFrom = from;
        _legTo = target;
        _legStartUtc = utcNow;
        _legDuration = duration;
        return new MonsterMovementLeg(MonsterId, MapDefinitionId, from, target, duration);
    }

    private void RestAt(WorldPosition position)
    {
        _legFrom = position;
        _legTo = position;
        _legStartUtc = default;
        _legDuration = TimeSpan.Zero;
        _nextLegUtc = default;
    }

    public MonsterSnapshot CreateSnapshot() => CreateSnapshot(DateTime.UtcNow);

    public MonsterSnapshot CreateSnapshot(DateTime utcNow)
    {
        TimeSpan remaining = _legStartUtc + _legDuration - utcNow;
        if (remaining < TimeSpan.Zero || _legDuration <= TimeSpan.Zero)
            remaining = TimeSpan.Zero;
        return new MonsterSnapshot(
            MonsterId,
            Definition.DefinitionId,
            Definition.Name,
            Definition.Level,
            MapDefinitionId,
            Definition.ExperienceReward,
            LifeId,
            CurrentHealth,
            Definition.MaximumHealth,
            IsAlive,
            SpawnPosition,
            RespawnAtUtc)
        {
            Position = PositionAt(utcNow),
            MoveTarget = _legTo,
            MoveRemaining = remaining,
        };
    }
}
