using System.Numerics;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Players;

public sealed class PlayerSession
{
    private readonly object _syncRoot = new();
    private readonly double _maximumMovementDeltaSeconds;
    private Vector2 _movementDirection;
    private Vector2 _position;
    private DateTime _lastMovementUpdateUtc;
    private long _lastProcessedMovementSequence;
    private long _positionSnapshotSequence;
    private int _level;
    private long _totalExperience;
    private long _experienceIntoLevel;
    private long _experienceToNextLevel;
    private long _vitalsSequence;

    public PlayerSession(
        PlayerSessionProfile profile,
        int currentHealth,
        int maximumHealth,
        float moveSpeed,
        Vector2 spawnPosition,
        int baseAttack,
        TimeSpan maximumMovementDelta,
        DateTime utcNow,
        long totalExperience = 0,
        long experienceIntoLevel = 0,
        long experienceToNextLevel = 0,
        int maximumMana = 0,
        int defense = 0)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        SessionId = Guid.NewGuid();
        CurrentHealth = currentHealth;
        MaximumHealth = maximumHealth;
        MoveSpeed = moveSpeed;
        _position = spawnPosition;
        BaseAttack = baseAttack;
        Defense = defense;
        MaximumMana = maximumMana;
        CurrentMana = maximumMana;
        _level = profile.Level;
        _totalExperience = totalExperience;
        _experienceIntoLevel = experienceIntoLevel;
        _experienceToNextLevel = experienceToNextLevel;
        _maximumMovementDeltaSeconds = maximumMovementDelta.TotalSeconds;
        _lastMovementUpdateUtc = utcNow;
    }

    public Guid SessionId { get; }
    public PlayerSessionProfile Profile { get; }
    public int CharacterId => Profile.CharacterId;
    public string CharacterName => Profile.CharacterName;
    public int Level { get { lock (_syncRoot) return _level; } }
    public long TotalExperience
        { get { lock (_syncRoot) return _totalExperience; } }
    public long ExperienceIntoLevel
        { get { lock (_syncRoot) return _experienceIntoLevel; } }
    public long ExperienceToNextLevel
        { get { lock (_syncRoot) return _experienceToNextLevel; } }
    public int CurrentHealth { get; private set; }
    public int MaximumHealth { get; private set; }
    public int CurrentMana { get; private set; }
    public int MaximumMana { get; private set; }
    public float MoveSpeed { get; }
    public int BaseAttack { get; private set; }
    public int Defense { get; private set; }
    public Vector2 Position
    {
        get
        {
            lock (_syncRoot)
                return _position;
        }
    }
    public DateTime NextAttackAtUtc { get; private set; }
    public bool IsAlive => CurrentHealth > 0;
    public long LastProcessedMovementSequence
    {
        get
        {
            lock (_syncRoot)
                return _lastProcessedMovementSequence;
        }
    }

    public bool TrySetMovement(
        Vector2 direction,
        long clientSequence,
        DateTime utcNow,
        IWorldMovementResolver movementResolver)
    {
        lock (_syncRoot)
        {
            if (clientSequence <= _lastProcessedMovementSequence)
                return false;

            AdvancePositionUnsafe(utcNow, movementResolver);
            _movementDirection = direction.LengthSquared() > 1f
                ? Vector2.Normalize(direction)
                : direction;
            _lastProcessedMovementSequence = clientSequence;
            return true;
        }
    }

    public void AdvancePosition(
        DateTime utcNow,
        IWorldMovementResolver movementResolver)
    {
        lock (_syncRoot)
            AdvancePositionUnsafe(utcNow, movementResolver);
    }

    public bool TryResolveAuthoritativePosition(
        Func<Vector2, Vector2> resolver,
        out Vector2 resolvedPosition)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        lock (_syncRoot)
        {
            resolvedPosition = resolver(_position);
            if (resolvedPosition == _position)
                return false;

            _position = resolvedPosition;
            return true;
        }
    }

    public PlayerPositionState CapturePositionSnapshot()
    {
        lock (_syncRoot)
        {
            return new PlayerPositionState(
                ++_positionSnapshotSequence,
                _lastProcessedMovementSequence,
                _position);
        }
    }

    public PlayerVitalsState ApplyProgression(
        int level,
        long totalExperience,
        long experienceIntoLevel,
        long experienceToNextLevel,
        CharacterStats stats)
    {
        lock (_syncRoot)
        {
            int healthIncrease = Math.Max(
                0,
                stats.MaximumHealth - MaximumHealth);
            int manaIncrease = Math.Max(
                0,
                stats.MaximumMana - MaximumMana);
            _level = level;
            _totalExperience = totalExperience;
            _experienceIntoLevel = experienceIntoLevel;
            _experienceToNextLevel = experienceToNextLevel;
            MaximumHealth = stats.MaximumHealth;
            MaximumMana = stats.MaximumMana;
            CurrentHealth = Math.Min(
                MaximumHealth,
                CurrentHealth + healthIncrease);
            CurrentMana = Math.Min(
                MaximumMana,
                CurrentMana + manaIncrease);
            BaseAttack = stats.Attack;
            Defense = stats.Defense;
            return CaptureVitalsSnapshotUnsafe(PlayerVitalsChange.Progression);
        }
    }

    public PlayerVitalsState ApplyDamage(int requestedDamage)
    {
        lock (_syncRoot)
        {
            int damage = Math.Max(0, requestedDamage);
            if (damage == 0)
            {
                return CaptureVitalsSnapshotUnsafe(
                    PlayerVitalsChange.Damage,
                    incrementSequence: false);
            }
            CurrentHealth = Math.Max(0, CurrentHealth - damage);
            return CaptureVitalsSnapshotUnsafe(PlayerVitalsChange.Damage);
        }
    }

    public PlayerVitalsState ApplyHealing(int requestedHealing)
    {
        lock (_syncRoot)
        {
            int healing = Math.Max(0, requestedHealing);
            if (healing == 0)
            {
                return CaptureVitalsSnapshotUnsafe(
                    PlayerVitalsChange.Healing,
                    incrementSequence: false);
            }
            CurrentHealth += Math.Min(healing, MaximumHealth - CurrentHealth);
            return CaptureVitalsSnapshotUnsafe(PlayerVitalsChange.Healing);
        }
    }

    public bool TrySpendMana(
        int requestedMana,
        out PlayerVitalsState snapshot)
    {
        lock (_syncRoot)
        {
            if (requestedMana < 0 || CurrentMana < requestedMana)
            {
                snapshot = CaptureVitalsSnapshotUnsafe(
                    PlayerVitalsChange.Correction,
                    incrementSequence: false);
                return false;
            }

            if (requestedMana == 0)
            {
                snapshot = CaptureVitalsSnapshotUnsafe(
                    PlayerVitalsChange.ManaSpent,
                    incrementSequence: false);
                return true;
            }

            CurrentMana -= requestedMana;
            snapshot = CaptureVitalsSnapshotUnsafe(
                PlayerVitalsChange.ManaSpent);
            return true;
        }
    }

    public PlayerVitalsState RestoreMana(int requestedMana)
    {
        lock (_syncRoot)
        {
            int restoredMana = Math.Max(0, requestedMana);
            if (restoredMana == 0)
            {
                return CaptureVitalsSnapshotUnsafe(
                    PlayerVitalsChange.ManaRestored,
                    incrementSequence: false);
            }
            CurrentMana += Math.Min(restoredMana, MaximumMana - CurrentMana);
            return CaptureVitalsSnapshotUnsafe(PlayerVitalsChange.ManaRestored);
        }
    }

    public PlayerVitalsState CaptureVitalsSnapshot(
        PlayerVitalsChange reason = PlayerVitalsChange.Correction)
    {
        lock (_syncRoot)
            return CaptureVitalsSnapshotUnsafe(reason);
    }

    private PlayerVitalsState CaptureVitalsSnapshotUnsafe(
        PlayerVitalsChange reason,
        bool incrementSequence = true) =>
        new(
            incrementSequence ? ++_vitalsSequence : _vitalsSequence,
            reason,
            CurrentHealth,
            MaximumHealth,
            CurrentMana,
            MaximumMana);

    private void AdvancePositionUnsafe(
        DateTime utcNow,
        IWorldMovementResolver movementResolver)
    {
        double elapsedSeconds = Math.Clamp(
            (utcNow - _lastMovementUpdateUtc).TotalSeconds,
            0,
            _maximumMovementDeltaSeconds);

        Vector2 desiredPosition = _position +
            _movementDirection * MoveSpeed * (float)elapsedSeconds;
        _position = movementResolver.Resolve(
            Profile.MapDefinitionId,
            _position,
            desiredPosition);
        _lastMovementUpdateUtc = utcNow;
    }

    public TimeSpan GetAttackCooldownRemaining(DateTime utcNow) =>
        NextAttackAtUtc > utcNow
            ? NextAttackAtUtc - utcNow
            : TimeSpan.Zero;

    public void CommitAttack(DateTime utcNow, TimeSpan cooldown) =>
        NextAttackAtUtc = utcNow + cooldown;
}

public readonly record struct PlayerPositionState(
    long ServerSequence,
    long AcknowledgedClientSequence,
    Vector2 Position);

public enum PlayerVitalsChange : byte
{
    InitialSnapshot = 0,
    Progression = 1,
    Damage = 2,
    Healing = 3,
    ManaSpent = 4,
    ManaRestored = 5,
    Respawn = 6,
    Correction = 7,
}

public readonly record struct PlayerVitalsState(
    long Sequence,
    PlayerVitalsChange Reason,
    int CurrentHealth,
    int MaximumHealth,
    int CurrentMana,
    int MaximumMana);
