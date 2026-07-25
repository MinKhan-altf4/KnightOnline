#nullable enable

namespace KnightOnline.Client.Shared.Packets
{
    public enum SelectCharacterResult : byte
    {
        Success = 0,
        CharacterNotFound = 1,
        AlreadySelected = 2,
        CharacterAlreadyOnline = 3,
    }

    public sealed class SelectCharacterRequestPacket
    {
        public int CharacterId { get; }
        public SelectCharacterRequestPacket(int characterId) => CharacterId = characterId;
    }

    public sealed class SelectedCharacterPacket
    {
        public int CharacterId { get; }
        public string CharacterName { get; }
        public int Level { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public float MoveSpeed { get; }
        public float PositionX { get; }
        public float PositionY { get; }

        public SelectedCharacterPacket(
            int characterId,
            string characterName,
            int level,
            int currentHealth,
            int maximumHealth,
            float moveSpeed,
            float positionX,
            float positionY)
        {
            CharacterId = characterId;
            CharacterName = characterName;
            Level = level;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            MoveSpeed = moveSpeed;
            PositionX = positionX;
            PositionY = positionY;
        }
    }

    public sealed class SelectCharacterResponsePacket
    {
        public SelectCharacterResult Result { get; }
        public string Message { get; }
        public SelectedCharacterPacket? Character { get; }

        public SelectCharacterResponsePacket(
            SelectCharacterResult result,
            string message,
            SelectedCharacterPacket? character = null)
        {
            Result = result;
            Message = message;
            Character = character;
        }
    }

    public sealed class PlayerMoveInputPacket
    {
        public float DirectionX { get; }
        public float DirectionY { get; }

        public PlayerMoveInputPacket(float directionX, float directionY)
        {
            DirectionX = directionX;
            DirectionY = directionY;
        }
    }

    public enum AttackResultStatus : byte
    {
        Success = 0,
        NoSelectedCharacter = 1,
        MonsterNotFound = 2,
        MonsterDead = 3,
        OutOfRange = 4,
        Cooldown = 5,
        PlayerDead = 6,
    }

    public sealed class AttackResultPacket
    {
        public AttackResultStatus Status { get; }
        public int MonsterId { get; }
        public int AppliedDamage { get; }
        public int CooldownRemainingMilliseconds { get; }

        public AttackResultPacket(
            AttackResultStatus status,
            int monsterId,
            int appliedDamage,
            int cooldownRemainingMilliseconds = 0)
        {
            Status = status;
            MonsterId = monsterId;
            AppliedDamage = appliedDamage;
            CooldownRemainingMilliseconds = cooldownRemainingMilliseconds;
        }
    }
}
