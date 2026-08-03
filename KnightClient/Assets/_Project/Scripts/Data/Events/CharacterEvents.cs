using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Models;
using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Data.Events
{
    public readonly struct CharacterCreationRequestedEvent : IGameEvent
    {
        public readonly CharacterCreationDraftData Draft;

        public CharacterCreationRequestedEvent(
            CharacterCreationDraftData draft) =>
            Draft = draft;
    }

    public readonly struct CharacterCreationSlotRequestedEvent :
        IStickyGameEvent
    {
        public readonly int SlotIndex;

        public CharacterCreationSlotRequestedEvent(int slotIndex) =>
            SlotIndex = slotIndex;
    }

    public readonly struct CharacterCreationCancelledEvent : IGameEvent
    {
    }

    public readonly struct CharacterCreationCatalogReceivedEvent :
        IStickyGameEvent
    {
        public readonly CharacterCreationCatalogData Catalog;

        public CharacterCreationCatalogReceivedEvent(
            CharacterCreationCatalogData catalog) =>
            Catalog = catalog;
    }

    public readonly struct CharacterNameAvailabilityReceivedEvent : IGameEvent
    {
        public readonly bool IsAvailable;
        public readonly string Message;

        public CharacterNameAvailabilityReceivedEvent(
            bool isAvailable,
            string message)
        {
            IsAvailable = isAvailable;
            Message = message;
        }
    }

    public readonly struct CharacterNameCheckRequestedEvent : IGameEvent
    {
        public readonly string ServerId;
        public readonly string CharacterName;

        public CharacterNameCheckRequestedEvent(
            string serverId,
            string characterName)
        {
            ServerId = serverId;
            CharacterName = characterName;
        }
    }

    public readonly struct CharacterSelectionBackRequestedEvent : IGameEvent
    {
    }

    /// <summary>
    /// Kết quả 1 lần thao tác tạo nhân vật - hành động tức thời do UI
    /// chủ động kích hoạt (bấm nút), UI luôn đã subscribe trước khi thao tác
    /// xảy ra nên KHÔNG cần Sticky - dùng IGameEvent thường.
    /// </summary>
    public readonly struct CharacterCreationResultEvent : IGameEvent
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly CharacterData Character; // null nếu thất bại

        public CharacterCreationResultEvent(bool success, string message, CharacterData character)
        {
            Success = success;
            Message = message;
            Character = character;
        }
    }

    public readonly struct CharacterListReceivedEvent : IStickyGameEvent
    {
        public readonly IReadOnlyList<CharacterData> Characters;

        public CharacterListReceivedEvent(IReadOnlyList<CharacterData> characters)
        {
            Characters = characters;
        }
    }

    public readonly struct CharacterListFailedEvent : IGameEvent
    {
        public readonly string Message;

        public CharacterListFailedEvent(string message)
        {
            Message = message;
        }
    }

    public readonly struct CharacterSelectedEvent : IGameEvent
    {
        public readonly CharacterData Character;

        public CharacterSelectedEvent(CharacterData character)
        {
            Character = character;
        }
    }

    public readonly struct GameplaySessionReadyEvent : IGameEvent
    {
        public readonly Guid GameplaySessionId;

        public GameplaySessionReadyEvent(Guid gameplaySessionId)
        {
            GameplaySessionId = gameplaySessionId;
        }
    }

    public readonly struct EnterWorldFailedEvent : IGameEvent
    {
        public readonly string Message;

        public EnterWorldFailedEvent(string message)
        {
            Message = message;
        }
    }

    public readonly struct CharacterSelectionFailedEvent : IGameEvent
    {
        public readonly string Message;

        public CharacterSelectionFailedEvent(string message)
        {
            Message = message;
        }
    }

    public readonly struct CharacterProgressionChangedEvent : IGameEvent
    {
        public readonly Guid RequestId;
        public readonly long AppliedExperience;
        public readonly long TotalExperience;
        public readonly int Level;
        public readonly long ExperienceIntoLevel;
        public readonly long ExperienceToNextLevel;
        public readonly int CurrentHealth;
        public readonly int MaximumHealth;
        public readonly int CurrentMana;
        public readonly int MaximumMana;
        public readonly int Attack;
        public readonly int Defense;

        public CharacterProgressionChangedEvent(
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

    public enum CharacterVitalsReason : byte
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

    public readonly struct CharacterVitalsChangedEvent : IGameEvent
    {
        public readonly long Sequence;
        public readonly CharacterVitalsReason Reason;
        public readonly int CurrentHealth;
        public readonly int MaximumHealth;
        public readonly int CurrentMana;
        public readonly int MaximumMana;
        public readonly DateTime ServerTimeUtc;

        public CharacterVitalsChangedEvent(
            long sequence,
            CharacterVitalsReason reason,
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
