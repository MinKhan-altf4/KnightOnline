using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Player;
using TMPro;
using UnityEngine;
using VContainer;

namespace KnightOnline.Client.UI
{
    /// <summary>
    /// HUD tối thiểu cho InGame scene.
    /// Hiển thị tên nhân vật, trạng thái kết nối và vị trí debug.
    /// Inject CharacterData và PlayerController qua VContainer.
    /// </summary>
    public sealed class InGameHUD : MonoBehaviour
    {
        [Header("HUD Elements")]
        [SerializeField] private TextMeshProUGUI _characterNameText;
        [SerializeField] private TextMeshProUGUI _connectionStatusText;
        [SerializeField] private TextMeshProUGUI _positionDebugText;

        private CharacterData _characterData;
        private Transform _playerTransform;

        [Inject]
        public void Construct(CharacterData characterData, PlayerController playerController)
        {
            _characterData = characterData;
            _playerTransform = playerController.transform;
        }

        private void Start()
        {
            _characterNameText.text = _characterData?.CharacterName ?? "Unknown";
            SetConnectionStatus(true);
        }

        private void Update()
        {
            if (_playerTransform == null) return;
            var pos = _playerTransform.position;
            _positionDebugText.text = $"X: {pos.x:F1}  Y: {pos.y:F1}";
        }

        public void SetConnectionStatus(bool connected)
        {
            _connectionStatusText.text = connected ? "● Connected" : "● Disconnected";
            _connectionStatusText.color = connected ? Color.green : Color.red;
        }
    }
}