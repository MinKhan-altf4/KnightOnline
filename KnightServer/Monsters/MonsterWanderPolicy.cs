namespace KnightOnline.Server.Monsters;

/// <summary>
/// How alive monsters roam around their spawn point: walk in a straight line
/// to a random point within <see cref="Radius"/> of the spawn, rest for a
/// random time between the idle bounds, repeat. Server-authoritative; clients
/// only replay the legs they are sent.
/// </summary>
public sealed record MonsterWanderPolicy
{
    /// <summary>Legs shorter than this are skipped (the monster keeps resting).</summary>
    public const float MinimumLegDistance = 0.3f;

    public MonsterWanderPolicy(
        float radius,
        float speed,
        TimeSpan minimumIdle,
        TimeSpan maximumIdle)
    {
        if (!float.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(speed) || speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (minimumIdle < TimeSpan.Zero || maximumIdle < minimumIdle)
            throw new ArgumentOutOfRangeException(nameof(maximumIdle));

        Radius = radius;
        Speed = speed;
        MinimumIdle = minimumIdle;
        MaximumIdle = maximumIdle;
    }

    public float Radius { get; }
    /// <summary>World units per second.</summary>
    public float Speed { get; }
    public TimeSpan MinimumIdle { get; }
    public TimeSpan MaximumIdle { get; }

    public TimeSpan NextIdle(Random random) =>
        MinimumIdle + TimeSpan.FromTicks(
            (long)((MaximumIdle - MinimumIdle).Ticks * random.NextDouble()));
}

/// <summary>A straight move a monster just started (broadcast as MonsterMoved).</summary>
public readonly record struct MonsterMovementLeg(
    int MonsterId,
    string MapDefinitionId,
    WorldPosition From,
    WorldPosition To,
    TimeSpan Duration);
