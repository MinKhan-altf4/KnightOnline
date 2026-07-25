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
        [SerializeField] private Button _backButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _messageText;

        public event Action PlayNewRequested;
        public event Action<string, string> LoginRequested;
        public event Action ServerSelectionRequested;

        private void Awake()
        {
            ConfigureInputs();
            ShowEntry(string.Empty);
        }

        private void OnEnable()
        {
            AddListeners();
        }

        private void OnDisable()
        {
            RemoveListeners();
        }

        public void ShowEntry(string message)
        {
            SetVisible(true);
            SetContent(_entryContent, true);
            SetContent(_loginContent, false);
            SetMessage(message);
            SetInteractable(true);
        }

        public void ShowLogin(string message = "")
        {
            SetVisible(true);
            SetContent(_entryContent, false);
            SetContent(_loginContent, true);
            SetMessage(message);
            SetInteractable(true);
            _usernameInput?.ActivateInputField();
        }

        public void Hide()
        {
            ClearPassword();
            SetVisible(false);
        }

        public void SetBusy(bool isBusy)
        {
            SetInteractable(!isBusy);
        }

        private void OnPlayNewClicked()
        {
            SetBusy(true);
            SetMessage("Đang tạo phiên chơi mới...");
            PlayNewRequested?.Invoke();
        }

        private void OnLoginClicked()
        {
            string username = _usernameInput?.text?.Trim() ?? string.Empty;
            string password = _passwordInput?.text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrEmpty(password))
            {
                SetMessage("Vui lòng nhập tài khoản và mật khẩu.");
                return;
            }

            SetBusy(true);
            SetMessage("Đang đăng nhập...");
            LoginRequested?.Invoke(username, password);
        }

        private void OnServerClicked()
        {
            ServerSelectionRequested?.Invoke();
            SetMessage("Chọn server sẽ được mở ở giai đoạn tiếp theo.");
        }

        private void ConfigureInputs()
        {
            if (_passwordInput != null)
                _passwordInput.contentType =
                    TMP_InputField.ContentType.Password;
        }

        private void AddListeners()
        {
            _playNewButton?.onClick.AddListener(OnPlayNewClicked);
            _showLoginButton?.onClick.AddListener(ShowLoginFromButton);
            _serverButton?.onClick.AddListener(OnServerClicked);
            _loginButton?.onClick.AddListener(OnLoginClicked);
            _backButton?.onClick.AddListener(ShowEntryFromButton);
        }

        private void RemoveListeners()
        {
            _playNewButton?.onClick.RemoveListener(OnPlayNewClicked);
            _showLoginButton?.onClick.RemoveListener(ShowLoginFromButton);
            _serverButton?.onClick.RemoveListener(OnServerClicked);
            _loginButton?.onClick.RemoveListener(OnLoginClicked);
            _backButton?.onClick.RemoveListener(ShowEntryFromButton);
        }

        private void ShowLoginFromButton() => ShowLogin();
        private void ShowEntryFromButton() => ShowEntry(string.Empty);

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

        private void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        private static void SetContent(GameObject content, bool visible)
        {
            if (content != null)
                content.SetActive(visible);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_entryContent == null || _loginContent == null ||
                _playNewButton == null || _showLoginButton == null ||
                _serverButton == null || _usernameInput == null ||
                _passwordInput == null || _loginButton == null ||
                _backButton == null || _messageText == null)
            {
                Debug.LogWarning(
                    "[AuthenticationEntryPanel] Thiếu serialized reference.",
                    this);
            }
        }
#endif
    }
}
