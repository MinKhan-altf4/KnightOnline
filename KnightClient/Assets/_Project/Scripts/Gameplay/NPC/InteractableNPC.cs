using System.Collections.Generic;
using KnightOnline.Client.Gameplay.Targeting;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.NPC
{
    public enum NpcActionType { Close, Shop, Quest }

    [System.Serializable]
    public class NpcOption
    {
        public string OptionText;       
        public NpcActionType Action;    
    }

    public class InteractableNPC : MonoBehaviour, ITargetable
    {
        [Header("NPC Info")]
        [SerializeField] private string _npcName = "NPC Name";
        [TextArea(3, 5)] [SerializeField] private string _greetingText = "Xin chào!";
        [SerializeField] private float _interactionRange = 3f;
        [SerializeField] private int _level = 1;
        [SerializeField] private Transform _markerAnchor;

        [Header("Interaction Options")]
        [SerializeField] private List<NpcOption> _options = new List<NpcOption>();

        public string NpcName => _npcName;
        public float InteractionRange => _interactionRange;
        public string GreetingText => _greetingText;
        public List<NpcOption> Options => _options;
        public int TargetId => GetEntityId().GetHashCode();
        public TargetType TargetType => KnightOnline.Client.Gameplay.Targeting.TargetType.Npc;
        public string DisplayName => _npcName;
        public int Level => _level;
        public int CurrentHealth => 0;
        public int MaximumHealth => 0;
        public bool ShowsHealth => false;
        public Transform MarkerAnchor => _markerAnchor != null ? _markerAnchor : transform;

        public NpcInteractionRequestedEvent CreateInteractionRequest()
        {
            var optionSnapshot = new NpcOptionData[_options.Count];

            for (int index = 0; index < _options.Count; index++)
            {
                NpcOption option = _options[index];
                optionSnapshot[index] = new NpcOptionData(option.OptionText, option.Action);
            }

            return new NpcInteractionRequestedEvent(
                this,
                _npcName,
                _greetingText,
                optionSnapshot);
        }
    }
}
