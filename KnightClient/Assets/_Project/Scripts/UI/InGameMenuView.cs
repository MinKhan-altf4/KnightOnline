using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Gameplay.Player;
using KnightOnline.Client.Gameplay.Services;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace KnightOnline.Client.UI
{
    public sealed class InGameMenuView : MonoBehaviour
    {
        [Header("Menu")]
        [SerializeField] private Button _toggleButton;
        [SerializeField] private TMP_Text _toggleLabel;
        [SerializeField] private RectTransform _menuPanel;
        [SerializeField] private Button _logoutButton;
        [SerializeField] private GameObject[] _hudToHide = Array.Empty<GameObject>();
        [SerializeField] private string _entrySceneName = "Bootstrap";

        private AuthenticationFlowService _authentication;
        private PlayerController _playerController;
        private IDisposable _leaveSubscription;
        private bool _isExpanded;
        private bool _isLoggingOut;

        [Inject]
        public void Construct(
            AuthenticationFlowService authentication,
            PlayerController playerController,
            IEventBus events)
        {
            _authentication = authentication;
            _playerController = playerController;
            _leaveSubscription =
                events.Subscribe<AccountSessionLeaveResultEvent>(
                    OnAccountSessionLeaveResult);
        }

        private void Start()
        {
            if (_authentication == null ||
                _toggleButton == null ||
                _menuPanel == null ||
                _logoutButton == null)
            {
                Debug.LogError(
                    "[InGameMenu] Missing dependency or serialized reference.",
                    this);
                enabled = false;
                return;
            }

            _toggleButton.onClick.AddListener(Toggle);
            _logoutButton.onClick.AddListener(Logout);
            SetExpanded(false);
        }

        private void Toggle()
        {
            if (_isLoggingOut)
                return;
            SetExpanded(!_isExpanded);
        }

        private void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;
            _menuPanel.gameObject.SetActive(expanded);
            foreach (GameObject hud in _hudToHide)
            {
                if (hud != null)
                    hud.SetActive(!expanded);
            }

            if (_toggleLabel != null)
                _toggleLabel.text = expanded ? "v" : "^";

            if (_toggleButton.transform is RectTransform toggleRect)
            {
                toggleRect.anchoredPosition = expanded
                    ? new Vector2(0f, 126f)
                    : new Vector2(0f, 14f);
            }
        }

        private void Logout()
        {
            if (_isLoggingOut)
                return;

            _isLoggingOut = true;
            _logoutButton.interactable = false;
            _toggleButton.interactable = false;
            _playerController?.SetMovementEnabled(false);
            _authentication.LogoutToAuthenticationEntry();
        }

        private void OnAccountSessionLeaveResult(
            AccountSessionLeaveResultEvent result)
        {
            if (!_isLoggingOut)
                return;

            if (!result.Success)
            {
                _isLoggingOut = false;
                _logoutButton.interactable = true;
                _toggleButton.interactable = true;
                _playerController?.SetMovementEnabled(true);
                Debug.LogWarning($"[InGameMenu] Logout failed: {result.Message}");
                return;
            }

            SceneManager.LoadSceneAsync(
                _entrySceneName,
                LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            if (_toggleButton != null)
                _toggleButton.onClick.RemoveListener(Toggle);
            if (_logoutButton != null)
                _logoutButton.onClick.RemoveListener(Logout);
            _leaveSubscription?.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_toggleButton == null ||
                _toggleLabel == null ||
                _menuPanel == null ||
                _logoutButton == null)
            {
                Debug.LogWarning(
                    "[InGameMenu] Missing serialized reference.",
                    this);
            }
        }
#endif
    }
}
