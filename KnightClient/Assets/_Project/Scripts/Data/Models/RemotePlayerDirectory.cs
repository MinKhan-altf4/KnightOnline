using System;
using System.Collections.Generic;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using UnityEngine;

namespace KnightOnline.Client.Data.Models
{
    public sealed class RemotePlayerState
    {
        public RemotePlayerState(int characterId, string name, int level,
            string classDefinitionId)
        {
            CharacterId = characterId;
            Name = name;
            Level = level;
            ClassDefinitionId = classDefinitionId;
        }

        public int CharacterId { get; }
        public string Name { get; }
        public int Level { get; }
        public string ClassDefinitionId { get; }
        public Vector2 Direction { get; internal set; }
        public InterpolationBuffer Positions { get; } = new();
    }

    /// <summary>
    /// App-lifetime view of other players visible to this client, fed by the
    /// network layer (WorldEntitiesSnapshot) and read every frame by the
    /// in-game presenter. Survives the Bootstrap→InGame scene load, so spawns
    /// received while the scene is loading are not lost. Cleared on disconnect.
    /// Main-thread only.
    /// </summary>
    public sealed class RemotePlayerDirectory : IDisposable
    {
        private readonly Dictionary<int, RemotePlayerState> _players = new();
        private readonly IDisposable _disconnectSubscription;

        public RemotePlayerDirectory(IEventBus events)
        {
            _disconnectSubscription =
                events?.Subscribe<ServerDisconnectedEvent>(_ => Clear());
        }

        /// <summary>Increments whenever a player is added or removed.</summary>
        public int MembershipVersion { get; private set; }

        public long LastAppliedTick { get; private set; }

        public IReadOnlyCollection<RemotePlayerState> Players => _players.Values;

        /// <summary>
        /// Returns false for a snapshot that is not newer than the last one
        /// applied; the caller must then ignore it.
        /// </summary>
        public bool TryBeginSnapshot(long serverTick)
        {
            if (serverTick <= LastAppliedTick)
                return false;
            LastAppliedTick = serverTick;
            return true;
        }

        public void Spawn(int characterId, string name, int level,
            string classDefinitionId, Vector2 position, Vector2 direction,
            double receivedAt)
        {
            var state = new RemotePlayerState(characterId, name, level,
                classDefinitionId)
            {
                Direction = direction,
            };
            state.Positions.Add(receivedAt, position);
            _players[characterId] = state;
            MembershipVersion++;
        }

        public void Move(int characterId, Vector2 position, Vector2 direction,
            double receivedAt)
        {
            if (!_players.TryGetValue(characterId, out RemotePlayerState state))
                return; // move before spawn: ignore, the next spawn is complete
            state.Direction = direction;
            state.Positions.Add(receivedAt, position);
        }

        public void Despawn(int characterId)
        {
            if (_players.Remove(characterId))
                MembershipVersion++;
        }

        public void Clear()
        {
            if (_players.Count > 0)
                MembershipVersion++;
            _players.Clear();
            LastAppliedTick = 0;
        }

        public bool TryGet(int characterId, out RemotePlayerState state) =>
            _players.TryGetValue(characterId, out state);

        public void Dispose() => _disconnectSubscription?.Dispose();
    }
}
