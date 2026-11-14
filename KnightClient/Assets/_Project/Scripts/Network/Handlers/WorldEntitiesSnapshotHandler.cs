using System.Text.Json;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Shared.Packets;
using UnityEngine;

namespace KnightOnline.Client.Network.Handlers
{
    /// <summary>
    /// Maps protocol-2 WorldEntitiesSnapshot packets into the
    /// <see cref="RemotePlayerDirectory"/> so Gameplay never sees packets.
    /// </summary>
    public sealed class WorldEntitiesSnapshotHandler : IClientPacketHandler
    {
        private readonly RemotePlayerDirectory _directory;

        public WorldEntitiesSnapshotHandler(RemotePlayerDirectory directory) =>
            _directory = directory;

        public PacketType PacketType => PacketType.WorldEntitiesSnapshot;

        public void Handle(string payload)
        {
            WorldEntitiesSnapshotPacket packet =
                JsonSerializer.Deserialize<WorldEntitiesSnapshotPacket>(payload);
            if (packet == null || !_directory.TryBeginSnapshot(packet.ServerTick))
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            foreach (int characterId in packet.Despawned)
                _directory.Despawn(characterId);

            foreach (RemotePlayerSpawnPacket spawn in packet.Spawned)
            {
                if (!IsFinite(spawn.X, spawn.Y))
                    continue;
                _directory.Spawn(spawn.CharacterId, spawn.Name, spawn.Level,
                    spawn.ClassDefinitionId, new Vector2(spawn.X, spawn.Y),
                    new Vector2(spawn.DirectionX, spawn.DirectionY), now);
            }

            foreach (RemotePlayerMovePacket move in packet.Moved)
            {
                if (!IsFinite(move.X, move.Y))
                    continue;
                _directory.Move(move.CharacterId, new Vector2(move.X, move.Y),
                    new Vector2(move.DirectionX, move.DirectionY), now);
            }
        }

        private static bool IsFinite(float x, float y) =>
            float.IsFinite(x) && float.IsFinite(y);
    }
}
