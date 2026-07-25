using System.Collections.Generic;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Models;

namespace KnightOnline.Client.Data.Events
{
    public readonly struct MonsterListReceivedEvent : IStickyGameEvent
    {
        public readonly IReadOnlyList<MonsterData> Monsters;

        public MonsterListReceivedEvent(IReadOnlyList<MonsterData> monsters)
        {
            Monsters = monsters;
        }
    }

    public readonly struct MonsterSelectedEvent : IGameEvent
    {
        public readonly MonsterData Monster;

        public MonsterSelectedEvent(MonsterData monster)
        {
            Monster = monster;
        }
    }

    public readonly struct MonsterHealthChangedEvent : IGameEvent
    {
        public readonly int MonsterId;
        public readonly int CurrentHealth;
        public readonly int MaximumHealth;

        public MonsterHealthChangedEvent(
            int monsterId,
            int currentHealth,
            int maximumHealth)
        {
            MonsterId = monsterId;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
        }
    }

    public readonly struct MonsterDiedEvent : IGameEvent
    {
        public readonly int MonsterId;

        public MonsterDiedEvent(int monsterId)
        {
            MonsterId = monsterId;
        }
    }

    public readonly struct MonsterRespawnedEvent : IGameEvent
    {
        public readonly MonsterData Monster;

        public MonsterRespawnedEvent(MonsterData monster)
        {
            Monster = monster;
        }
    }

    public enum AttackOutcome
    {
        Success,
        NoSelectedCharacter,
        MonsterNotFound,
        MonsterDead,
        OutOfRange,
        Cooldown,
        PlayerDead
    }

    public readonly struct AttackResultEvent : IGameEvent
    {
        public readonly AttackOutcome Status;
        public readonly int MonsterId;
        public readonly int AppliedDamage;
        public readonly int CooldownRemainingMilliseconds;

        public AttackResultEvent(
            AttackOutcome status,
            int monsterId,
            int appliedDamage,
            int cooldownRemainingMilliseconds)
        {
            Status = status;
            MonsterId = monsterId;
            AppliedDamage = appliedDamage;
            CooldownRemainingMilliseconds = cooldownRemainingMilliseconds;
        }
    }
}
