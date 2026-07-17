using System;

namespace KnightOnline.Client.Data.Models
{
    /// <summary>
    /// Dữ liệu thuần của 1 nhân vật - CHỈ chứa thuộc tính, không chứa logic
    /// xử lý (validate tên, tính toán stat...). Logic đó thuộc về Gameplay
    /// hoặc Network, không phải Data - giữ đúng ranh giới "pure data".
    /// </summary>
    public sealed class CharacterData
    {
        public string CharacterName { get; set; }

        public CharacterData(string characterName)
        {
            CharacterName = characterName;
        }
    }
}