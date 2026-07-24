using System; // Bắt buộc thêm dòng này để dùng Action
using System.Collections.Generic;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.NPC
{
    public enum NpcActionType { Close, Shop, Quest, Talk }

    [System.Serializable]
    public class NpcOption
    {
        public string OptionText;       
        public NpcActionType Action;    
    }

    public class InteractableNPC : MonoBehaviour
    {
        // 1. Tạo một sự kiện (Event) tĩnh để báo hiệu NPC bị click
        public static event Action<InteractableNPC> OnNpcClicked;

        [Header("NPC Info")]
        [SerializeField] private string _npcName = "NPC Name";
        [TextArea(3, 5)] [SerializeField] private string _greetingText = "Xin chào!";
        [SerializeField] private float _interactionRange = 3f;

        [Header("Interaction Options")]
        [SerializeField] private List<NpcOption> _options = new List<NpcOption>();

        public string NpcName => _npcName;
        public float InteractionRange => _interactionRange;
        public string GreetingText => _greetingText;
        public List<NpcOption> Options => _options;

        public void OnInteract()
        {
            // 2. Thay vì gọi thẳng UI, NPC chỉ phát tín hiệu (phát loa) ra ngoài
            OnNpcClicked?.Invoke(this);
        }
    }
}