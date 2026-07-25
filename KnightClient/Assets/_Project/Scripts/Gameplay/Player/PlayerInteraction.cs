using KnightOnline.Client.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using KnightOnline.Client.Gameplay.NPC;
using KnightOnline.Client.Gameplay.Targeting;
using VContainer;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Quản lý việc click chuột tương tác với NPC/Vật thể trong thế giới 2D.
    /// Sử dụng Unity New Input System.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private LayerMask _npcLayer; 

        private Camera _mainCam;
        private IEventBus _eventBus;
        private bool _interactionEnabled = true;
        private TargetSelectionService _targetSelection;

        [Inject]
        public void Construct(IEventBus eventBus, TargetSelectionService targetSelection)
        {
            _eventBus = eventBus;
            _targetSelection = targetSelection;
        }

        private void Start()
        {
            _mainCam = Camera.main;
        }

        private void Update()
        {
            if (!_interactionEnabled)
                return;

            // Kiểm tra thiết bị chuột
            if (Mouse.current == null) return;

            // Đọc sự kiện click chuột trái theo New Input System
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryInteractWithNPC();
            }
        }

        private void TryInteractWithNPC()
        {
            if (_mainCam == null) _mainCam = Camera.main;

            // Đọc vị trí con trỏ chuột theo New Input System
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector2 mouseWorldPos = _mainCam.ScreenToWorldPoint(mouseScreenPos);

            // Bắn Raycast 2D tại vị trí con trỏ chuột
            RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero, 0f, _npcLayer);

            if (hit.collider != null)
            {
                if (hit.collider.TryGetComponent<InteractableNPC>(out var npc))
                {
                    _targetSelection.Select(npc);
                    float distance = Vector2.Distance(transform.position, npc.transform.position);

                    if (distance <= npc.InteractionRange)
                    {
                        _eventBus.Publish(npc.CreateInteractionRequest());
                    }
                    else
                    {
                        Debug.Log($"[NPC] {_npcNameFormatted(npc)} ở quá xa! Hãy đi lại gần hơn.");
                    }
                }
            }
        }

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
        }

        private string _npcNameFormatted(InteractableNPC npc) => npc != null ? npc.NpcName : "NPC";
    }
}
