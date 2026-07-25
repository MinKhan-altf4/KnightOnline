using System.Numerics;

namespace KnightOnline.Server.Players;

public sealed class PlayerSession
{
    private readonly double _maximumMovementDeltaSeconds;
    private Vector2 _movementDirection;
    private DateTime _lastMovementUpdateUtc;

    public PlayerSession(
        int characterId,
        string characterName,
        int level,
        int currentHealth,
        int maximumHealth,
        float moveSpeed,
        Vector2 spawnPosition,
        int baseAttack,
        TimeSpan maximumMovementDelta,
        DateTime utcNow)
    {
        CharacterId = characterId;
        CharacterName = characterName;
        Level = level;
        CurrentHealth = currentHealth;
        MaximumHealth = maximumHealth;
        MoveSpeed = moveSpeed;
        Position = spawnPosition;
        BaseAttack = baseAttack;
        _maximumMovementDeltaSeconds = maximumMovementDelta.TotalSeconds;
        _lastMovementUpdateUtc = utcNow;
    }

    public int CharacterId { get; }
    public string CharacterName { get; }
    public int Level { get; }
    public int CurrentHealth { get; private set; }
    public int MaximumHealth { get; }
    public float MoveSpeed { get; }
    public int BaseAttack { get; }
    public Vector2 Position { get; private set; }
    public DateTime NextAttackAtUtc { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    public void SetMovement(Vector2 direction, DateTime utcNow)
    {
        AdvancePosition(utcNow);
        _movementDirection = direction.LengthSquared() > 1f
            ? Vector2.Normalize(direction)
            : direction;
    }

    public void AdvancePosition(DateTime utcNow)
    {
        double elapsedSeconds = Math.Clamp(
            (utcNow - _lastMovementUpdateUtc).TotalSeconds,
            0,
            _maximumMovementDeltaSeconds);

        Position += _movementDirection * MoveSpeed * (float)elapsedSeconds;
        _lastMovementUpdateUtc = utcNow;
    }

    public TimeSpan GetAttackCooldownRemaining(DateTime utcNow) =>
        NextAttackAtUtc > utcNow
            ? NextAttackAtUtc - utcNow
            : TimeSpan.Zero;

    public void CommitAttack(DateTime utcNow, TimeSpan cooldown) =>
        NextAttackAtUtc = utcNow + cooldown;
}
