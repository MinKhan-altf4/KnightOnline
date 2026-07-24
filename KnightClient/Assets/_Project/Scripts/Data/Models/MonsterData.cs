using UnityEngine;

namespace KnightOnline.Client.Data.Models
{
    public sealed class MonsterData
    {
        public int MonsterId { get; set; }
        public int DefinitionId { get; set; }
        public string MonsterName { get; set; }
        public int Level { get; set; }
        public int CurrentHealth { get; set; }
        public int MaximumHealth { get; set; }
        public bool IsAlive { get; set; }
        public Vector2 Position { get; set; }
    }
}
