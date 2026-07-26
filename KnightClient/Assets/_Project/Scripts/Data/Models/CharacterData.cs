using System;
using System.Collections.Generic;
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
        public int SlotIndex { get; set; }
        public string ClassDefinitionId { get; set; }
        public string BodyTypeDefinitionId { get; set; }
        public IReadOnlyList<AppearanceSelectionData> AppearanceSelections
            { get; set; } = Array.Empty<AppearanceSelectionData>();

        // --- Stats ---
        public int Level { get; set; }
        public int MaxHp { get; set; }
        public int CurrentHp { get; set; }
        /// <summary>Tốc độ di chuyển (units/second). Default 4f.</summary>
        public float MoveSpeed { get; set; }

        // --- World ---
        /// <summary>Vị trí spawn. Default Vector2.zero đến khi DB cung cấp.</summary>
        public Vector2 SpawnPosition { get; set; } = Vector2.zero;
        public string CurrentMapDefinitionId { get; set; }
        public string CurrentSpawnPointId { get; set; }

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

    public sealed class AppearanceSelectionData
    {
        public string SlotDefinitionId { get; set; }
        public string OptionDefinitionId { get; set; }
    }

    public sealed class CharacterClassDefinitionData
    {
        public string DefinitionId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<string> AllowedBodyTypeIds { get; set; }
        public string PreviewAssetAddress { get; set; }
    }

    public sealed class BodyTypeDefinitionData
    {
        public string DefinitionId { get; set; }
        public string DisplayName { get; set; }
    }

    public sealed class AppearanceDefinitionData
    {
        public string DefinitionId { get; set; }
        public string SlotDefinitionId { get; set; }
        public string DisplayName { get; set; }
        public IReadOnlyList<string> AllowedBodyTypeIds { get; set; }
        public IReadOnlyList<string> AllowedClassDefinitionIds { get; set; }
        public string AssetAddress { get; set; }
        public bool IsStarterOption { get; set; }
    }

    public sealed class CharacterCreationCatalogData
    {
        public int CatalogVersion { get; set; }
        public string ServerId { get; set; }
        public IReadOnlyList<CharacterClassDefinitionData> Classes { get; set; }
        public IReadOnlyList<BodyTypeDefinitionData> BodyTypes { get; set; }
        public IReadOnlyList<AppearanceDefinitionData> AppearanceOptions { get; set; }
    }

    public sealed class CharacterCreationDraftData
    {
        public Guid RequestId { get; set; }
        public string ServerId { get; set; }
        public int SlotIndex { get; set; }
        public string CharacterName { get; set; }
        public string ClassDefinitionId { get; set; }
        public string BodyTypeDefinitionId { get; set; }
        public IReadOnlyList<AppearanceSelectionData> AppearanceSelections
            { get; set; }
        public int CatalogVersion { get; set; }
    }
}
