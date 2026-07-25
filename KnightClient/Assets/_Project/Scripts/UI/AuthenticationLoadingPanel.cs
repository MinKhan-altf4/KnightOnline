using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using TMPro;
using UnityEngine;
using VContainer;

namespace KnightOnline.Client.UI
{
    public sealed class AuthenticationLoadingPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _overlay;
        [SerializeField] private RectTransform _spinner;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField, Min(0f)] private float _rotationSpeed = 180f;

        private IEventBus _events;
        private IDisposable _loadingSubscription;

        [Inject]
        public void Construct(IEventBus events)
        {
            _events = events;
        }

        private void Start()
        {
            if (_events == null)
            {
                Debug.LogError(
                    "[AuthenticationLoadingPanel] VContainer injection failed.",
                    this);
                enabled = false;
                return;
            }

            _loadingSubscription =
                _events.Subscribe<AuthenticationLoadingEvent>(OnLoading);
            SetVisible(false);
        }

        private void Update()
        {
            if (_overlay == null || !_overlay.activeSelf || _spinner == null)
                return;

            _spinner.Rotate(
                0f,
                0f,
                -_rotationSpeed * Time.unscaledDeltaTime);
        }

        private void OnLoading(AuthenticationLoadingEvent state)
        {
            SetVisible(state.IsVisible);
            if (!state.IsVisible)
                return;

            if (_messageText != null)
                _messageText.text = state.Message ?? string.Empty;
            if (_countdownText != null)
                _countdownText.text =
                    $"{Mathf.CeilToInt(state.RemainingSeconds)}s";
        }

        private void SetVisible(bool visible)
        {
            if (_overlay != null)
                _overlay.SetActive(visible);
        }

        private void OnDestroy()
        {
            _loadingSubscription?.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_overlay == null || _spinner == null ||
                _messageText == null || _countdownText == null)
            {
                Debug.LogWarning(
                    "[AuthenticationLoadingPanel] Thiếu serialized reference.",
                    this);
            }
        }
#endif
    }
}
