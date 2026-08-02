using System.Numerics;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Players;

public sealed class PlayerSession
{
    private readonly double _maximumMovementDeltaSeconds;
    private Vector2 _movementDirection;
    private DateTime _lastMovementUpdateUtc;

    public PlayerSession(
        PlayerSessionProfile profile,
        int currentHealth,
        int maximumHealth,
        float moveSpeed,
        Vector2 spawnPosition,
        int baseAttack,
        TimeSpan maximumMovementDelta,
        DateTime utcNow)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        SessionId = Guid.NewGuid();
        CurrentHealth = currentHealth;
        MaximumHealth = maximumHealth;
        MoveSpeed = moveSpeed;
        Position = spawnPosition;
        BaseAttack = baseAttack;
        _maximumMovementDeltaSeconds = maximumMovementDelta.TotalSeconds;
        _lastMovementUpdateUtc = utcNow;
    }

    public Guid SessionId { get; }
    public PlayerSessionProfile Profile { get; }
    public int CharacterId => Profile.CharacterId;
    public string CharacterName => Profile.CharacterName;
    public int Level => Profile.Level;
    public int CurrentHealth { get; private set; }
    public int MaximumHealth { get; }
    public float MoveSpeed { get; }
    public int BaseAttack { get; }
    public Vector2 Position { get; private set; }
    public DateTime NextAttackAtUtc { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    public void SetMovement(
        Vector2 direction,
        DateTime utcNow,
        IWorldMovementResolver movementResolver)
    {
        AdvancePosition(utcNow, movementResolver);
        _movementDirection = direction.LengthSquared() > 1f
            ? Vector2.Normalize(direction)
            : direction;
    }

    public void AdvancePosition(
        DateTime utcNow,
        IWorldMovementResolver movementResolver)
    {
        double elapsedSeconds = Math.Clamp(
            (utcNow - _lastMovementUpdateUtc).TotalSeconds,
            0,
            _maximumMovementDeltaSeconds);

        Vector2 desiredPosition = Position +
            _movementDirection * MoveSpeed * (float)elapsedSeconds;
        Position = movementResolver.Resolve(Position, desiredPosition);
        _lastMovementUpdateUtc = utcNow;
    }

    public TimeSpan GetAttackCooldownRemaining(DateTime utcNow) =>
        NextAttackAtUtc > utcNow
            ? NextAttackAtUtc - utcNow
            : TimeSpan.Zero;

    public void CommitAttack(DateTime utcNow, TimeSpan cooldown) =>
        NextAttackAtUtc = utcNow + cooldown;
}
