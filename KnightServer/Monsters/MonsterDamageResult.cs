namespace KnightOnline.Server.Monsters;

public enum MonsterDamageStatus
{
    Success,
    MonsterNotFound,
    MonsterAlreadyDead,
    InvalidDamage,
}

public sealed record MonsterDamageResult(
    MonsterDamageStatus Status,
    int MonsterId,
    int AppliedDamage,
    int RemainingHealth,
    bool WasKilled,
    Guid MonsterLifeId = default);
