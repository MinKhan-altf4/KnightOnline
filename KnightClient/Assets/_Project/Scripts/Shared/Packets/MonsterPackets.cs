using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Shared.Packets
{
    public sealed class ListMonstersRequestPacket { }

    public sealed class AttackMonsterRequestPacket
    {
        public int MonsterId { get; }

        public AttackMonsterRequestPacket(int monsterId)
        {
            MonsterId = monsterId;
        }
    }

    public sealed class MonsterHealthChangedPacket
    {
        public int MonsterId { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }

        public MonsterHealthChangedPacket(
            int monsterId,
            int currentHealth,
            int maximumHealth)
        {
            MonsterId = monsterId;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
        }
    }

    public sealed class MonsterDiedPacket
    {
        public int MonsterId { get; }

        public MonsterDiedPacket(int monsterId)
        {
            MonsterId = monsterId;
        }
    }

    public sealed class MonsterRespawnedPacket
    {
        public MonsterSnapshotPacket Monster { get; }

        public MonsterRespawnedPacket(MonsterSnapshotPacket monster)
        {
            Monster = monster;
        }
    }

    public sealed class MonsterSnapshotPacket
    {
        public int MonsterId { get; }
        public int DefinitionId { get; }
        public string MonsterName { get; }
        public int Level { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public bool IsAlive { get; }
        public float PositionX { get; }
        public float PositionY { get; }

        public MonsterSnapshotPacket(
            int monsterId,
            int definitionId,
            string monsterName,
            int level,
            int currentHealth,
            int maximumHealth,
            bool isAlive,
            float positionX,
            float positionY)
        {
            MonsterId = monsterId;
            DefinitionId = definitionId;
            MonsterName = monsterName;
            Level = level;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            IsAlive = isAlive;
            PositionX = positionX;
            PositionY = positionY;
        }
    }

    public sealed class ListMonstersResponsePacket
    {
        public IReadOnlyList<MonsterSnapshotPacket> Monsters { get; }

        public ListMonstersResponsePacket(
            IReadOnlyList<MonsterSnapshotPacket> monsters)
        {
            Monsters = monsters ?? Array.Empty<MonsterSnapshotPacket>();
        }
    }
}
