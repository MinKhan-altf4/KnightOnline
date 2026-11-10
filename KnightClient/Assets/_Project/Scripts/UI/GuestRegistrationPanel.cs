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
        [Header("Presentation")]
        [SerializeField] private KnightUiTheme _theme;

        public event Action<string, string> RegistrationRequested;
        public event Action CancelRequested;

        private void Awake()
        {
            _theme ??= KnightUiTheme.LoadDefault();
            ApplyPresentation();
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

        private void ApplyPresentation()
        {
            if (_theme == null)
                return;

            Image overlay = GetComponent<Image>();
            _theme.ApplyPanel(overlay, true);
            _theme.ApplyInput(_usernameInput);
            _theme.ApplyInput(_passwordInput);
            _theme.ApplyInput(_confirmPasswordInput);
            _theme.ApplyCleanButton(_registerButton, 58f);
            _theme.ApplyCleanButton(_cancelButton, 58f);
            _theme.ApplyBodyText(_messageText, 20f);

            RectTransform window = _usernameInput != null
                ? _usernameInput.transform.parent as RectTransform
                : null;
            if (window != null)
            {
                Image card = window.GetComponent<Image>() ??
                    window.gameObject.AddComponent<Image>();
                _theme.ApplyCard(card);
                AddHeading(window);
            }
            SetRect(
                window,
                Vector2.zero,
                new Vector2(560f, 500f));
            SetRect(
                _usernameInput?.transform as RectTransform,
                new Vector2(0f, 110f),
                new Vector2(440f, 55f));
            SetRect(
                _passwordInput?.transform as RectTransform,
                new Vector2(0f, 35f),
                new Vector2(440f, 55f));
            SetRect(
                _confirmPasswordInput?.transform as RectTransform,
                new Vector2(0f, -40f),
                new Vector2(440f, 55f));
            SetRect(
                _registerButton?.transform as RectTransform,
                new Vector2(-115f, -125f),
                new Vector2(210f, 58f));
            SetRect(
                _cancelButton?.transform as RectTransform,
                new Vector2(115f, -125f),
                new Vector2(210f, 58f));
            SetRect(
                _messageText?.transform as RectTransform,
                new Vector2(0f, -195f),
                new Vector2(460f, 52f));
        }

        private void AddHeading(RectTransform parent)
        {
            var headingObject = new GameObject(
                "RegistrationHeading",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            headingObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)headingObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 205f);
            rect.sizeDelta = new Vector2(460f, 56f);

            TextMeshProUGUI heading =
                headingObject.GetComponent<TextMeshProUGUI>();
            heading.font = _messageText != null
                ? _messageText.font
                : TMP_Settings.defaultFontAsset;
            heading.text = "TẠO TÀI KHOẢN\n<size=60%>Đăng ký tài khoản mới</size>";
            heading.fontSize = 26f;
            heading.fontSizeMin = 16f;
            heading.fontSizeMax = 26f;
            heading.enableAutoSizing = true;
            heading.fontStyle = FontStyles.Bold;
            heading.color = new Color(1f, 0.96f, 0.84f, 1f);
            heading.alignment = TextAlignmentOptions.Center;
            heading.raycastTarget = false;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
    }
}
