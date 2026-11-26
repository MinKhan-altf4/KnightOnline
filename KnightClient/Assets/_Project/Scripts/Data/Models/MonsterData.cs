using UnityEngine;

namespace KnightOnline.Client.Data.Models
{
    public sealed class MonsterData
    {
        public int MonsterId { get; set; }
        public int DefinitionId { get; set; }
        public string MonsterName { get; set; }
        public int Level { get; set; }
        public string MapDefinitionId { get; set; }
        public int CurrentHealth { get; set; }
        public int MaximumHealth { get; set; }
        public bool IsAlive { get; set; }
        /// <summary>Server position when this data was received.</summary>
        public Vector2 Position { get; set; }
        /// <summary>End of the wander leg in progress (equals Position when resting).</summary>
        public Vector2 MoveTarget { get; set; }
        /// <summary>Seconds left on that leg when received; 0 when resting.</summary>
        public float MoveRemainingSeconds { get; set; }
    }
}
