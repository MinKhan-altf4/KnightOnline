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
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
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
            IReadOnlyList<AppearanceSelectionPacket>? appearanceSelections = null)
        {
            CharacterId = characterId;
            CharacterName = characterName;
            Level = level;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
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
