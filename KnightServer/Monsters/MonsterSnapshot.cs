namespace KnightOnline.Server.Monsters;

public sealed record MonsterSnapshot(
    int MonsterId,
    int DefinitionId,
    string Name,
    int Level,
    string MapDefinitionId,
    int ExperienceReward,
    Guid LifeId,
    int CurrentHealth,
    int MaximumHealth,
    bool IsAlive,
    WorldPosition SpawnPosition,
    DateTime? RespawnAtUtc)
{
    /// <summary>Live position when the snapshot was taken (wandering monsters move).</summary>
    public WorldPosition Position { get; init; } = SpawnPosition;

    /// <summary>End of the current leg; equals <see cref="Position"/> when resting.</summary>
    public WorldPosition MoveTarget { get; init; } = SpawnPosition;

    /// <summary>Time left on the current leg; zero when resting.</summary>
    public TimeSpan MoveRemaining { get; init; }
}
