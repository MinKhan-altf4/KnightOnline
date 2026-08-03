#nullable enable
using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Shared.Packets
{
    public enum SelectCharacterResult : byte
    {
        Success = 0,
        CharacterNotFound = 1,
        AlreadySelected = 2,
        CharacterAlreadyOnline = 3,
        Unauthorized = 4,
        MalformedRequest = 5,
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
        public long TotalExperience { get; }
        public long ExperienceIntoLevel { get; }
        public long ExperienceToNextLevel { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public int CurrentMana { get; }
        public int MaximumMana { get; }
        public int Attack { get; }
        public int Defense { get; }
        public float MoveSpeed { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public int SlotIndex { get; }
        public string ClassDefinitionId { get; }
        public string BodyTypeDefinitionId { get; }
        public string MapDefinitionId { get; }
        public string SpawnPointId { get; }
        public IReadOnlyList<AppearanceSelectionPacket> AppearanceSelections
            { get; }

        public SelectedCharacterPacket(
            int characterId,
            string characterName,
            int level,
            int currentHealth,
            int maximumHealth,
            float moveSpeed,
            float positionX,
            float positionY,
            int slotIndex = 0,
            string classDefinitionId = "",
            string bodyTypeDefinitionId = "",
            string mapDefinitionId = "",
            string spawnPointId = "",
            IReadOnlyList<AppearanceSelectionPacket>? appearanceSelections = null,
            long totalExperience = 0,
            long experienceIntoLevel = 0,
            long experienceToNextLevel = 0,
            int currentMana = 0,
            int maximumMana = 0,
            int attack = 0,
            int defense = 0)
        {
            CharacterId = characterId;
            CharacterName = characterName;
            Level = level;
            TotalExperience = totalExperience;
            ExperienceIntoLevel = experienceIntoLevel;
            ExperienceToNextLevel = experienceToNextLevel;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            CurrentMana = currentMana;
            MaximumMana = maximumMana;
            Attack = attack;
            Defense = defense;
            MoveSpeed = moveSpeed;
            PositionX = positionX;
            PositionY = positionY;
            SlotIndex = slotIndex;
            ClassDefinitionId = classDefinitionId;
            BodyTypeDefinitionId = bodyTypeDefinitionId;
            MapDefinitionId = mapDefinitionId;
            SpawnPointId = spawnPointId;
            AppearanceSelections =
                appearanceSelections ?? Array.Empty<AppearanceSelectionPacket>();
        }
    }

    public sealed class SelectCharacterResponsePacket
    {
        public SelectCharacterResult Result { get; }
        public string Message { get; }
        public SelectedCharacterPacket? Character { get; }
        public Guid GameplaySessionId { get; }

        public SelectCharacterResponsePacket(
            SelectCharacterResult result,
            string message,
            SelectedCharacterPacket? character = null,
            Guid gameplaySessionId = default)
        {
            Result = result;
            Message = message;
            Character = character;
            GameplaySessionId = gameplaySessionId;
        }
    }

    public enum EnterWorldResult : byte
    {
        Success = 0,
        Unauthorized = 1,
        NoGameplaySession = 2,
        SessionMismatch = 3,
        MalformedRequest = 4,
    }

    public sealed class EnterWorldRequestPacket
    {
        public Guid GameplaySessionId { get; }

        public EnterWorldRequestPacket(Guid gameplaySessionId) =>
            GameplaySessionId = gameplaySessionId;
    }

    public sealed class EnterWorldSnapshotPacket
    {
        public Guid GameplaySessionId { get; }
        public long SnapshotVersion { get; }
        public DateTime ServerUtc { get; }
        public SelectedCharacterPacket Character { get; }

        public EnterWorldSnapshotPacket(
            Guid gameplaySessionId,
            long snapshotVersion,
            DateTime serverUtc,
            SelectedCharacterPacket character)
        {
            GameplaySessionId = gameplaySessionId;
            SnapshotVersion = snapshotVersion;
            ServerUtc = serverUtc;
            Character = character;
        }
    }

    public sealed class EnterWorldResponsePacket
    {
        public EnterWorldResult Result { get; }
        public string Message { get; }
        public EnterWorldSnapshotPacket? Snapshot { get; }

        public EnterWorldResponsePacket(
            EnterWorldResult result,
            string message,
            EnterWorldSnapshotPacket? snapshot = null)
        {
            Result = result;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
        }
    }

    public sealed class PlayerMoveInputPacket
    {
        public float DirectionX { get; }
        public float DirectionY { get; }
        public long ClientSequence { get; }

        public PlayerMoveInputPacket(
            float directionX,
            float directionY,
            long clientSequence)
        {
            DirectionX = directionX;
            DirectionY = directionY;
            ClientSequence = clientSequence;
        }
    }

    public enum PlayerPositionSnapshotReason : byte
    {
        MovementAccepted = 0,
        MovementRejected = 1,
        RespawnDisplacement = 2,
    }

    public sealed class PlayerPositionSnapshotPacket
    {
        public long ServerSequence { get; }
        public long AcknowledgedSequence { get; }
        public PlayerPositionSnapshotReason Reason { get; }
        public bool InputAccepted =>
            Reason != PlayerPositionSnapshotReason.MovementRejected;
        public float PositionX { get; }
        public float PositionY { get; }
        public DateTime ServerTimeUtc { get; }

        public PlayerPositionSnapshotPacket(
            long serverSequence,
            long acknowledgedSequence,
            PlayerPositionSnapshotReason reason,
            float positionX,
            float positionY,
            DateTime serverTimeUtc)
        {
            ServerSequence = serverSequence;
            AcknowledgedSequence = acknowledgedSequence;
            Reason = reason;
            PositionX = positionX;
            PositionY = positionY;
            ServerTimeUtc = serverTimeUtc;
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
        WrongMap = 7,
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

    public sealed class CharacterProgressionChangedPacket
    {
        public Guid RequestId { get; }
        public long AppliedExperience { get; }
        public long TotalExperience { get; }
        public int Level { get; }
        public long ExperienceIntoLevel { get; }
        public long ExperienceToNextLevel { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public int CurrentMana { get; }
        public int MaximumMana { get; }
        public int Attack { get; }
        public int Defense { get; }

        public CharacterProgressionChangedPacket(
            Guid requestId,
            long appliedExperience,
            long totalExperience,
            int level,
            long experienceIntoLevel,
            long experienceToNextLevel,
            int currentHealth,
            int maximumHealth,
            int currentMana,
            int maximumMana,
            int attack,
            int defense)
        {
            RequestId = requestId;
            AppliedExperience = appliedExperience;
            TotalExperience = totalExperience;
            Level = level;
            ExperienceIntoLevel = experienceIntoLevel;
            ExperienceToNextLevel = experienceToNextLevel;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            CurrentMana = currentMana;
            MaximumMana = maximumMana;
            Attack = attack;
            Defense = defense;
        }
    }

    public enum CharacterVitalsChangeReason : byte
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

    public sealed class CharacterVitalsSnapshotPacket
    {
        public long Sequence { get; }
        public CharacterVitalsChangeReason Reason { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public int CurrentMana { get; }
        public int MaximumMana { get; }
        public DateTime ServerTimeUtc { get; }

        public CharacterVitalsSnapshotPacket(
            long sequence,
            CharacterVitalsChangeReason reason,
            int currentHealth,
            int maximumHealth,
            int currentMana,
            int maximumMana,
            DateTime serverTimeUtc)
        {
            Sequence = sequence;
            Reason = reason;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            CurrentMana = currentMana;
            MaximumMana = maximumMana;
            ServerTimeUtc = serverTimeUtc;
        }
    }
}
