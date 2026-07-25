using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace KnightOnline.Client.UI
{
    public sealed class AuthenticationPopupPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _overlay;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _closeButton;

        private IDisposable _subscription;
        private IEventBus _events;
        private bool _reconnectOnClose;

        [Inject]
        public void Construct(IEventBus events) => _events = events;

        private void Start()
        {
            if (_events == null)
            {
                Debug.LogError(
                    "[AuthenticationPopupPanel] VContainer injection failed.",
                    this);
                enabled = false;
                return;
            }

            _closeButton?.onClick.AddListener(Hide);
            _subscription =
                _events.Subscribe<AuthenticationPopupRequestedEvent>(Show);
            Hide();
        }

        private void Show(AuthenticationPopupRequestedEvent popup)
        {
            if (_messageText != null)
                _messageText.text = popup.Message;
            _reconnectOnClose = popup.ReconnectOnClose;
            if (_overlay != null)
                _overlay.SetActive(true);
        }

        private void Hide()
        {
            bool shouldNotify = _overlay != null && _overlay.activeSelf;
            if (_overlay != null)
                _overlay.SetActive(false);
            if (shouldNotify)
            {
                _events?.Publish(new AuthenticationPopupDismissedEvent(
                    _reconnectOnClose));
            }
            _reconnectOnClose = false;
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);
            _subscription?.Dispose();
        }
    }
}
