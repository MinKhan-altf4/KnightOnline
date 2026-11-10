using UnityEngine;
using TMPro;
using VContainer;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;

namespace KnightOnline.Client.UI
{
    public class ConnectionStatusView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statusText;

        private IEventBus _eventBus;
        private System.IDisposable _connectionSubscription;
        private System.IDisposable _disconnectionSubscription;
        private System.IDisposable _accountReadySubscription;
        private System.IDisposable _characterListSubscription;

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
            _accountReadySubscription =
                _eventBus.Subscribe<AccountReadyEvent>(_ => HideStatus());
            _characterListSubscription =
                _eventBus.Subscribe<CharacterListReceivedEvent>(
                    _ => HideStatus());
            HideStatus();
        }

        private void OnConnectionResult(ServerConnectionResultEvent _)
        {
            // AuthenticationEntryPanel owns connection feedback in Bootstrap.
            // Keeping a second floating status label here causes overlapping UI.
            HideStatus();
        }

        private void OnDisconnected(ServerDisconnectedEvent _) => HideStatus();

        private void HideStatus()
        {
            if (_statusText != null)
            {
                _statusText.text = string.Empty;
                _statusText.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _connectionSubscription?.Dispose();
            _disconnectionSubscription?.Dispose();
            _accountReadySubscription?.Dispose();
            _characterListSubscription?.Dispose();
        }
    }
}
