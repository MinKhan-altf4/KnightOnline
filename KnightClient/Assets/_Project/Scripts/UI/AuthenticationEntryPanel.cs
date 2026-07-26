using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightOnline.Client.UI
{
    public sealed class AuthenticationEntryPanel : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _entryContent;
        [SerializeField] private GameObject _loginContent;

        [Header("Entry")]
        [SerializeField] private Button _playNewButton;
        [SerializeField] private Button _showLoginButton;
        [SerializeField] private Button _serverButton;

        [Header("Login")]
        [SerializeField] private TMP_InputField _usernameInput;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _showRegistrationButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private GuestRegistrationPanel _registrationPanel;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _messageText;

        public event Action PlayNewRequested;
        public event Action ContinueRequested;
        public event Action<string, string> LoginRequested;
        public event Action ServerSelectionRequested;

        private bool _primaryActionContinues;
        private string _accountDisplayHint = string.Empty;

        private void Awake()
        {
            if (_passwordInput != null)
                _passwordInput.contentType =
                    TMP_InputField.ContentType.Password;
            ShowEntry(string.Empty, false, string.Empty);
        }

        private void OnEnable()
        {
            _playNewButton?.onClick.AddListener(OnPrimaryClicked);
            _showLoginButton?.onClick.AddListener(OnShowLoginClicked);
            _serverButton?.onClick.AddListener(OnServerClicked);
            _loginButton?.onClick.AddListener(OnLoginConfirmed);
            _showRegistrationButton?.onClick.AddListener(
                OnShowRegistrationClicked);
            _backButton?.onClick.AddListener(OnBackClicked);
        }

        private void OnDisable()
        {
            _playNewButton?.onClick.RemoveListener(OnPrimaryClicked);
            _showLoginButton?.onClick.RemoveListener(OnShowLoginClicked);
            _serverButton?.onClick.RemoveListener(OnServerClicked);
            _loginButton?.onClick.RemoveListener(OnLoginConfirmed);
            _showRegistrationButton?.onClick.RemoveListener(
                OnShowRegistrationClicked);
            _backButton?.onClick.RemoveListener(OnBackClicked);
        }

        public void ShowEntry(
            string message,
            bool canContinue,
            string accountDisplayHint)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            _primaryActionContinues = canContinue;
            _accountDisplayHint = accountDisplayHint ?? string.Empty;
            SetContent(_entryContent, true);
            SetContent(_loginContent, false);
            _registrationPanel?.Hide();
            SetButtonLabel(
                _playNewButton,
                canContinue
                    ? $"Chơi tiếp: {_accountDisplayHint}"
                    : "Chơi mới");
            SetButtonLabel(
                _showLoginButton,
                canContinue ? "Đổi tài khoản" : "Có tài khoản");
            SetMessage(message);
            SetInteractable(true);
        }

        public void ShowLogin(string message = "")
        {
            SetContent(_entryContent, false);
            SetContent(_loginContent, true);
            _registrationPanel?.Hide();
            SetMessage(message);
            SetInteractable(true);
            _usernameInput?.ActivateInputField();
        }

        public void Hide()
        {
            ClearPassword();
            gameObject.SetActive(false);
        }

        private void OnPrimaryClicked()
        {
            SetInteractable(false);
            if (_primaryActionContinues)
            {
                SetMessage("Đang kiểm tra tài khoản...");
                ContinueRequested?.Invoke();
                return;
            }

            SetMessage("Đang tạo phiên chơi mới...");
            PlayNewRequested?.Invoke();
        }

        private void OnShowLoginClicked() => ShowLogin();

        private void OnShowRegistrationClicked() =>
            _registrationPanel?.Show();

        private void OnLoginConfirmed()
        {
            string username = _usernameInput?.text?.Trim() ?? string.Empty;
            string password = _passwordInput?.text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrEmpty(password))
            {
                SetMessage("Vui lòng nhập tài khoản và mật khẩu.");
                return;
            }

            LoginRequested?.Invoke(username, password);
            ClearPassword();
        }

        private void OnBackClicked()
        {
            ClearPassword();
            ShowEntry(
                string.Empty,
                _primaryActionContinues,
                _accountDisplayHint);
        }

        private void OnServerClicked()
        {
            ServerSelectionRequested?.Invoke();
            SetMessage(
                "Chọn server sẽ được mở ở giai đoạn tiếp theo.");
        }

        private void SetInteractable(bool interactable)
        {
            if (_playNewButton != null)
                _playNewButton.interactable = interactable;
            if (_showLoginButton != null)
                _showLoginButton.interactable = interactable;
            if (_serverButton != null)
                _serverButton.interactable = interactable;
            if (_loginButton != null)
                _loginButton.interactable = interactable;
            if (_showRegistrationButton != null)
                _showRegistrationButton.interactable = interactable;
            if (_backButton != null)
                _backButton.interactable = interactable;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null)
                _messageText.text = message ?? string.Empty;
        }

        private void ClearPassword()
        {
            if (_passwordInput != null)
                _passwordInput.text = string.Empty;
        }

        private static void SetContent(GameObject content, bool visible)
        {
            if (content != null)
                content.SetActive(visible);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TMP_Text text = button?.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_entryContent == null || _loginContent == null ||
                _playNewButton == null || _showLoginButton == null ||
                _serverButton == null || _usernameInput == null ||
                _passwordInput == null || _loginButton == null ||
                _showRegistrationButton == null ||
                _backButton == null || _registrationPanel == null ||
                _messageText == null)
            {
                Debug.LogWarning(
                    "[AuthenticationEntryPanel] Thiếu serialized reference.",
                    this);
            }
        }
#endif
    }
}
