using System;
using System.Collections.Generic;
using KnightOnline.Client.Core.Events;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.NPC
{
    public readonly struct NpcOptionData
    {
        public NpcOptionData(string text, NpcActionType action)
        {
            Text = text;
            Action = action;
        }

        public string Text { get; }
        public NpcActionType Action { get; }
    }

    /// <summary>
    /// Snapshot dữ liệu cần thiết để presentation mở dialog NPC.
    /// Source chỉ được giữ để theo dõi lifetime; UI phải đóng dialog nếu Source bị hủy.
    /// </summary>
    public sealed class NpcInteractionRequestedEvent : IGameEvent
    {
        public NpcInteractionRequestedEvent(
            InteractableNPC source,
            string npcName,
            string greetingText,
            IReadOnlyList<NpcOptionData> options)
        {
            Source = source;
            NpcEntityId = source != null ? source.GetEntityId() : default;
            NpcName = npcName;
            GreetingText = greetingText;
            Options = options ?? Array.Empty<NpcOptionData>();
        }

        public InteractableNPC Source { get; }
        public EntityId NpcEntityId { get; }
        public string NpcName { get; }
        public string GreetingText { get; }
        public IReadOnlyList<NpcOptionData> Options { get; }
    }

    /// <summary>
    /// Ý định chọn chức năng NPC. Hệ thống Shop/Quest tương ứng sẽ subscribe
    /// và xử lý nghiệp vụ; NpcDialogUI không được chứa logic của các hệ thống đó.
    /// </summary>
    public sealed class NpcActionRequestedEvent : IGameEvent
    {
        public NpcActionRequestedEvent(EntityId npcEntityId, string npcName, NpcActionType action)
        {
            NpcEntityId = npcEntityId;
            NpcName = npcName;
            Action = action;
        }

        public EntityId NpcEntityId { get; }
        public string NpcName { get; }
        public NpcActionType Action { get; }
    }
}
