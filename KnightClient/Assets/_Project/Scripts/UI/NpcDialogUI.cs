using System.Collections.Generic;
using TMPro; 
using UnityEngine;
using UnityEngine.UI;
using KnightOnline.Client.Gameplay.NPC; // UI nhìn vào Gameplay (Một chiều, rất an toàn)

namespace KnightOnline.Client.UI 
{
    public class NpcDialogUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _dialogPanel; 
        [SerializeField] private TextMeshProUGUI _npcNameText; 
        [SerializeField] private TextMeshProUGUI _greetingText; 
        [SerializeField] private Transform _buttonContainer; 

        [Header("Prefabs")]
        [SerializeField] private GameObject _optionButtonPrefab; 

        private void Awake()
        {
            _dialogPanel.SetActive(false);
        }

        // Bật tai nghe: Lắng nghe tín hiệu khi UI được bật
        private void OnEnable()
        {
            InteractableNPC.OnNpcClicked += ShowDialog;
        }

        // Tắt tai nghe: Hủy lắng nghe khi UI bị tắt để tránh lỗi tràn bộ nhớ
        private void OnDisable()
        {
            InteractableNPC.OnNpcClicked -= ShowDialog;
        }

        // Hàm này sẽ tự động chạy khi NPC phát loa
        private void ShowDialog(InteractableNPC npc)
        {
            _dialogPanel.SetActive(true);
            _npcNameText.text = npc.NpcName;
            _greetingText.text = npc.GreetingText;

            // Xóa các nút cũ
            foreach (Transform child in _buttonContainer)
            {
                Destroy(child.gameObject);
            }

            // Sinh ra các nút mới
            foreach (var option in npc.Options)
            {
                GameObject btnObj = Instantiate(_optionButtonPrefab, _buttonContainer);
                btnObj.GetComponentInChildren<TextMeshProUGUI>().text = option.OptionText;
                
                Button btn = btnObj.GetComponent<Button>();
                btn.onClick.AddListener(() => OnOptionClicked(option.Action));
            }
        }

        private void OnOptionClicked(NpcActionType action)
        {
            Debug.Log($"[UI] Người chơi đã chọn hành động: {action}");
            
            if (action == NpcActionType.Close)
            {
                CloseDialog();
            }
            else
            {
                Debug.Log($"[UI] Chức năng {action} đang được phát triển!");
            }
        }

        public void CloseDialog()
        {
            _dialogPanel.SetActive(false);
        }
    }
}