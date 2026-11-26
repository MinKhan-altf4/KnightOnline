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
        public string MapDefinitionId { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public bool IsAlive { get; }
        /// <summary>Live position when the snapshot was taken.</summary>
        public float PositionX { get; }
        public float PositionY { get; }
        /// <summary>End of the wander leg in progress (protocol 4).</summary>
        public float TargetX { get; }
        public float TargetY { get; }
        /// <summary>Time left on that leg; 0 when the monster is resting.</summary>
        public int MoveRemainingMilliseconds { get; }

        public MonsterSnapshotPacket(
            int monsterId,
            int definitionId,
            string monsterName,
            int level,
            string mapDefinitionId,
            int currentHealth,
            int maximumHealth,
            bool isAlive,
            float positionX,
            float positionY,
            float targetX = 0f,
            float targetY = 0f,
            int moveRemainingMilliseconds = 0)
        {
            MonsterId = monsterId;
            DefinitionId = definitionId;
            MonsterName = monsterName;
            Level = level;
            MapDefinitionId = mapDefinitionId ?? string.Empty;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            IsAlive = isAlive;
            PositionX = positionX;
            PositionY = positionY;
            TargetX = targetX;
            TargetY = targetY;
            MoveRemainingMilliseconds = moveRemainingMilliseconds;
        }
    }

    /// <summary>
    /// Server -> map (protocol 4): a monster started walking in a straight
    /// line from From to To, arriving after DurationMilliseconds. Clients
    /// replay the leg locally; the server stays authoritative.
    /// </summary>
    public sealed class MonsterMovedPacket
    {
        public int MonsterId { get; }
        public float FromX { get; }
        public float FromY { get; }
        public float ToX { get; }
        public float ToY { get; }
        public int DurationMilliseconds { get; }

        public MonsterMovedPacket(
            int monsterId,
            float fromX,
            float fromY,
            float toX,
            float toY,
            int durationMilliseconds)
        {
            MonsterId = monsterId;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            DurationMilliseconds = durationMilliseconds;
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
