using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Gameplay.Player;
using KnightOnline.Client.Gameplay.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VContainer;

namespace KnightOnline.Client.UI
{
    /// <summary>Connects the function popup to the existing logout flow.</summary>
    public sealed class InGameMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject[] _hudToHide = Array.Empty<GameObject>();
        [SerializeField] private string _entrySceneName = "Bootstrap";

        private AuthenticationFlowService _authentication;
        private PlayerController _playerController;
        private IDisposable _leaveSubscription;
        private Button _toggleButton;
        private Button _logoutButton;
        private VisualElement _functionMenuPanel;
        private VisualElement[] _optionalMenuItems = Array.Empty<VisualElement>();
        private Action<bool> _onExpandedChanged;
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
            _leaveSubscription = events.Subscribe<AccountSessionLeaveResultEvent>(
                OnAccountSessionLeaveResult);
        }

        public void Initialize(
            VisualElement root,
            Action<bool> onExpandedChanged,
            string sourceAssetName)
        {
            if (root == null || _authentication == null)
            {
                Debug.LogError("[InGameMenu] UI root or logout dependency is missing.", this);
                return;
            }

            VisualElement toggleElement = root.Q("function-menu-toggle");
            VisualElement logoutElement = root.Q("logout-button");
            _toggleButton = toggleElement as Button;
            _logoutButton = logoutElement as Button;
            _functionMenuPanel = root.Q("function-menu-panel");
            VisualElement equipmentItem = root.Q("equipment-menu-item");
            VisualElement mountItem = root.Q("mount-menu-item");
            VisualElement friendsItem = root.Q("friends-menu-item");
            if (_toggleButton == null || _logoutButton == null ||
                _functionMenuPanel == null)
            {
                Debug.LogError(
                    "[InGameMenu] Function menu UXML query failed. " +
                    $"toggle={_toggleButton != null}, logout={_logoutButton != null}, " +
                    $"panel={_functionMenuPanel != null}, " +
                    $"toggleType={toggleElement?.GetType().Name ?? "missing"}, " +
                    $"logoutType={logoutElement?.GetType().Name ?? "missing"}, " +
                    $"asset={sourceAssetName ?? "unknown"}.",
                    this);
                return;
            }

            _optionalMenuItems = new[] { equipmentItem, mountItem, friendsItem };
            _onExpandedChanged = onExpandedChanged;
            _toggleButton.clicked += Toggle;
            _logoutButton.clicked += Logout;
            SetExpanded(false);
        }

        private void Toggle()
        {
            if (!_isLoggingOut)
                SetExpanded(!_isExpanded);
        }

        private void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;
            _functionMenuPanel.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (VisualElement item in _optionalMenuItems)
            {
                if (item != null)
                    item.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _toggleButton.text = expanded ? "⌄" : "⌃";
            _toggleButton.tooltip = expanded ? "Thu gọn chức năng" : "Mở chức năng";
            _onExpandedChanged?.Invoke(!expanded);
        }

        private void Logout()
        {
            if (_isLoggingOut)
                return;

            _isLoggingOut = true;
            _logoutButton.SetEnabled(false);
            _toggleButton.SetEnabled(false);
            _playerController?.SetMovementEnabled(false);
            _authentication.LogoutToAuthenticationEntry();
        }

        private void OnAccountSessionLeaveResult(AccountSessionLeaveResultEvent result)
        {
            if (!_isLoggingOut)
                return;

            if (!result.Success)
            {
                _isLoggingOut = false;
                _logoutButton?.SetEnabled(true);
                _toggleButton?.SetEnabled(true);
                _playerController?.SetMovementEnabled(true);
                Debug.LogWarning($"[InGameMenu] Logout failed: {result.Message}");
                return;
            }

            SceneManager.LoadSceneAsync(_entrySceneName, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            if (_toggleButton != null)
                _toggleButton.clicked -= Toggle;
            if (_logoutButton != null)
                _logoutButton.clicked -= Logout;
            _leaveSubscription?.Dispose();
        }
    }
}
