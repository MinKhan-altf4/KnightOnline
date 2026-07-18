using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Models;
using System.Collections.Generic;

namespace KnightOnline.Client.Data.Events
{
    /// <summary>
    /// Kết quả 1 lần thao tác tạo nhân vật - hành động tức thời do UI
    /// chủ động kích hoạt (bấm nút), UI luôn đã subscribe trước khi thao tác
    /// xảy ra nên KHÔNG cần Sticky - dùng IGameEvent thường.
    /// </summary>
    public readonly struct CharacterCreationResultEvent : IGameEvent
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly CharacterData Character; // null nếu thất bại

        public CharacterCreationResultEvent(bool success, string message, CharacterData character)
        {
            Success = success;
            Message = message;
            Character = character;
        }
    }

    public readonly struct CharacterListReceivedEvent : IStickyGameEvent
    {
        public readonly IReadOnlyList<CharacterData> Characters;

        public CharacterListReceivedEvent(IReadOnlyList<CharacterData> characters)
        {
            Characters = characters;
        }
    }

    public readonly struct CharacterSelectedEvent : IGameEvent
    {
        public readonly CharacterData Character;

        public CharacterSelectedEvent(CharacterData character)
        {
            Character = character;
        }
    }
}
