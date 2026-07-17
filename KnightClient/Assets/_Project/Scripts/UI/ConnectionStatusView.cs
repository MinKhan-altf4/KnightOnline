using UnityEngine;
using TMPro;
using VContainer;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Client.UI
{
    public class ConnectionStatusView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statusText;

        private IEventBus _eventBus;
        private System.IDisposable _connectionSubscription;
        private System.IDisposable _disconnectionSubscription;

        [Inject]
        public void Construct(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        // Đổi từ Awake() sang Start() - VContainer đảm bảo [Inject] Construct()
        // đã chạy xong trước khi Start() được gọi, với RegisterComponentInHierarchy.
        // Awake() KHÔNG có đảm bảo này vì nó chạy theo vòng đời tự nhiên của Unity,
        // có thể sớm hơn thời điểm VContainer build xong container.
        private void Start()
        {
            if (_statusText == null)
            {
                _statusText = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            _connectionSubscription = _eventBus.Subscribe<ServerConnectionResultEvent>(OnConnectionResult);
            _disconnectionSubscription = _eventBus.Subscribe<ServerDisconnectedEvent>(OnDisconnected);

            SetStatus("[Success] Chào mừng đến với KnightOnline!");
        }

        private void OnConnectionResult(ServerConnectionResultEvent e)
        {
            if (e.Result == ConnectResult.Success)
            {
                SetStatus("[Success] Chào mừng đến với KnightOnline!");
                return;
            }

            SetStatus($"[{e.Result}] {e.Message}");
        }

        private void OnDisconnected(ServerDisconnectedEvent e)
        {
            SetStatus("Mất kết nối với Server.");
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }

            Debug.Log($"<color=magenta>[UI]</color> Trạng thái: {message}");
        }

        private void OnDestroy()
        {
            _connectionSubscription?.Dispose();
            _disconnectionSubscription?.Dispose();
        }
    }
}