using UnityEngine;

namespace KnightOnline.Client.Data.Models
{
    /// <summary>
    /// Dữ liệu thuần của 1 nhân vật - CHỈ chứa thuộc tính, không chứa logic
    /// xử lý (validate tên, tính toán stat...). Logic đó thuộc về Gameplay
    /// hoặc Network, không phải Data - giữ đúng ranh giới "pure data".
    /// </summary>
    public sealed class CharacterData
    {
        // --- Identity ---
        /// <summary>Server ID của nhân vật. 0 khi chưa có DB.</summary>
        public int CharacterId { get; set; }
        public string CharacterName { get; set; }

        // --- Stats ---
        public int Level { get; set; }
        public int MaxHp { get; set; }
        public int CurrentHp { get; set; }
        /// <summary>Tốc độ di chuyển (units/second). Default 4f.</summary>
        public float MoveSpeed { get; set; }

        // --- World ---
        /// <summary>Vị trí spawn. Default Vector2.zero đến khi DB cung cấp.</summary>
        public Vector2 SpawnPosition { get; set; } = Vector2.zero;

        public CharacterData(
            string characterName,
            int level,
            int maximumHealth,
            int currentHealth,
            float moveSpeed)
        {
            CharacterName = characterName;
            Level = level;
            MaxHp = maximumHealth;
            CurrentHp = currentHealth;
            MoveSpeed = moveSpeed;
        }
    }
}
