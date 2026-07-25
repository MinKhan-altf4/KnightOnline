using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightOnline.Client.UI
{
    public sealed class GuestRegistrationPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _usernameInput;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private TMP_InputField _confirmPasswordInput;
        [SerializeField] private Button _registerButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _messageText;

        public event Action<string, string> RegistrationRequested;
        public event Action CancelRequested;

        private void Awake()
        {
            SetPasswordMode(_passwordInput);
            SetPasswordMode(_confirmPasswordInput);
        }

        private void OnEnable()
        {
            _registerButton?.onClick.AddListener(Submit);
            _cancelButton?.onClick.AddListener(Cancel);
        }

        private void OnDisable()
        {
            _registerButton?.onClick.RemoveListener(Submit);
            _cancelButton?.onClick.RemoveListener(Cancel);
            ClearSecrets();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SetMessage(string.Empty);
            _usernameInput?.ActivateInputField();
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetBusy(bool busy, string message)
        {
            if (_registerButton != null)
                _registerButton.interactable = !busy;
            if (_cancelButton != null)
                _cancelButton.interactable = !busy;
            SetMessage(message);
        }

        private void Submit()
        {
            string username = _usernameInput?.text?.Trim() ?? string.Empty;
            string password = _passwordInput?.text ?? string.Empty;
            string confirmation = _confirmPasswordInput?.text ?? string.Empty;
            if (username.Length < 3)
            {
                SetMessage("Tên tài khoản phải có ít nhất 3 ký tự.");
                return;
            }
            if (password.Length < 8)
            {
                SetMessage("Mật khẩu phải có ít nhất 8 ký tự.");
                return;
            }
            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            {
                SetMessage("Mật khẩu xác nhận không khớp.");
                return;
            }

            SetBusy(true, "Đang tạo giao dịch đăng ký...");
            RegistrationRequested?.Invoke(username, password);
            ClearSecrets();
        }

        private void Cancel()
        {
            CancelRequested?.Invoke();
            Hide();
        }

        private void ClearSecrets()
        {
            if (_passwordInput != null) _passwordInput.text = string.Empty;
            if (_confirmPasswordInput != null)
                _confirmPasswordInput.text = string.Empty;
        }

        private void SetMessage(string value)
        {
            if (_messageText != null)
                _messageText.text = value ?? string.Empty;
        }

        private static void SetPasswordMode(TMP_InputField input)
        {
            if (input != null)
                input.contentType = TMP_InputField.ContentType.Password;
        }
    }
}
