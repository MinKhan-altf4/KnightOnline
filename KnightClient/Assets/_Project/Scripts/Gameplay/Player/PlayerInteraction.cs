using UnityEngine;
using UnityEngine.InputSystem; // Chuẩn New Input System
using KnightOnline.Client.Gameplay.NPC;

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

        private void Start()
        {
            _mainCam = Camera.main;
        }

        private void Update()
        {
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
                    float distance = Vector2.Distance(transform.position, npc.transform.position);

                    if (distance <= npc.InteractionRange)
                    {
                        npc.OnInteract();
                    }
                    else
                    {
                        Debug.Log($"[NPC] {_npcNameFormatted(npc)} ở quá xa! Hãy đi lại gần hơn.");
                    }
                }
            }
        }

        private string _npcNameFormatted(InteractableNPC npc) => npc != null ? npc.NpcName : "NPC";
    }
}