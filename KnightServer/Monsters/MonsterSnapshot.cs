namespace KnightOnline.Server.Monsters;

public sealed record MonsterSnapshot(
    int MonsterId,
    int DefinitionId,
    string Name,
    int Level,
    int CurrentHealth,
    int MaximumHealth,
    bool IsAlive,
    WorldPosition SpawnPosition,
    DateTime? RespawnAtUtc);
