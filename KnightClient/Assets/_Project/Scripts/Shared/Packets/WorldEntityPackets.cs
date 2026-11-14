using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Shared.Packets
{
    /// <summary>Another player entering the recipient's area of interest.</summary>
    public sealed class RemotePlayerSpawnPacket
    {
        public int CharacterId { get; }
        public string Name { get; }
        public int Level { get; }
        public string ClassDefinitionId { get; }
        public float X { get; }
        public float Y { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        /// <summary>World units per second along Direction (protocol 3).</summary>
        public float Speed { get; }

        public RemotePlayerSpawnPacket(int characterId, string name, int level,
            string classDefinitionId, float x, float y, float directionX,
            float directionY, float speed)
        {
            Speed = speed;
            CharacterId = characterId;
            Name = name;
            Level = level;
            ClassDefinitionId = classDefinitionId;
            X = x;
            Y = y;
            DirectionX = directionX;
            DirectionY = directionY;
        }
    }

    /// <summary>
    /// Authoritative movement state of an already spawned player. Protocol 3:
    /// sent only when the client's extrapolation (position + direction *
    /// speed * elapsed) would be wrong, plus a periodic correction.
    /// </summary>
    public sealed class RemotePlayerMovePacket
    {
        public int CharacterId { get; }
        public float X { get; }
        public float Y { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float Speed { get; }

        public RemotePlayerMovePacket(int characterId, float x, float y,
            float directionX, float directionY, float speed)
        {
            Speed = speed;
            CharacterId = characterId;
            X = x;
            Y = y;
            DirectionX = directionX;
            DirectionY = directionY;
        }
    }

    /// <summary>
    /// Protocol 2: per-recipient delta of visible players, produced by the
    /// server world tick. Sent only when something changed. Clients drop
    /// snapshots whose ServerTick is not newer than the last applied one.
    /// </summary>
    public sealed class WorldEntitiesSnapshotPacket
    {
        public long ServerTick { get; }
        public DateTime ServerTimeUtc { get; }
        public IReadOnlyList<RemotePlayerSpawnPacket> Spawned { get; }
        public IReadOnlyList<RemotePlayerMovePacket> Moved { get; }
        public IReadOnlyList<int> Despawned { get; }

        public WorldEntitiesSnapshotPacket(long serverTick, DateTime serverTimeUtc,
            IReadOnlyList<RemotePlayerSpawnPacket> spawned,
            IReadOnlyList<RemotePlayerMovePacket> moved,
            IReadOnlyList<int> despawned)
        {
            ServerTick = serverTick;
            ServerTimeUtc = serverTimeUtc;
            Spawned = spawned ?? Array.Empty<RemotePlayerSpawnPacket>();
            Moved = moved ?? Array.Empty<RemotePlayerMovePacket>();
            Despawned = despawned ?? Array.Empty<int>();
        }
    }
}
