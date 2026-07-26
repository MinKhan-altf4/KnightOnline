using System;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Network;
using UnityEngine;
using VContainer.Unity;

namespace KnightOnline.Client.Gameplay.Services
{
    public sealed class AuthenticationFlowService : IStartable, IDisposable
    {
        private readonly NetworkClient _network;
        private readonly IEventBus _events;
        private readonly ILocalAccountSessionStore _store;
        private readonly ClientAuthenticationSettings _settings;
        private IDisposable _connectionSubscription;
        private IDisposable _authenticationSubscription;
        private IDisposable _leaveSessionSubscription;
        private IDisposable _disconnectedSubscription;
        private IDisposable _popupDismissedSubscription;
        private IDisposable _registrationStartedSubscription;
        private StoredAccountSession _session;
        private string _pendingUsername;
        private string _pendingPassword;
        private bool _isCreatingGuest;
        private bool _isManualLogin;
        private bool _isAuthenticated;
        private bool _isReconnectingForRequest;
        private string _registrationVerifier;
        private string _registrationUsername;
        private string _registrationPassword;

        public AuthenticationFlowService(
            NetworkClient network,
            IEventBus events,
            ILocalAccountSessionStore store,
            ClientAuthenticationSettings settings)
        {
            _network = network;
            _events = events;
            _store = store;
            _settings = settings;
        }

        public void Start()
        {
            if (!_settings.DevelopmentBypassEnabled)
                _session = _store.Load();

            _connectionSubscription =
                _events.Subscribe<ServerConnectionResultEvent>(
                    OnConnectionResult);
            _authenticationSubscription =
                _events.Subscribe<AuthenticationResultEvent>(
                    OnAuthenticationResult);
            _leaveSessionSubscription =
                _events.Subscribe<AccountSessionLeaveResultEvent>(
                    OnAccountSessionLeaveResult);
            _disconnectedSubscription =
                _events.Subscribe<ServerDisconnectedEvent>(
                    OnServerDisconnected);
            _popupDismissedSubscription =
                _events.Subscribe<AuthenticationPopupDismissedEvent>(
                    OnPopupDismissed);
            _registrationStartedSubscription =
                _events.Subscribe<RegistrationStartedEvent>(
                    OnRegistrationStarted);

        }

        /// <summary>
        /// Local simulator for the future web registration callback. A
        /// production UI should call Begin with PKCE, open RegistrationUrl and
        /// complete only from a verified app/universal-link callback.
        /// </summary>
        public void RegisterGuestForDevelopment(
            string username,
            string password)
        {
            if (!_isAuthenticated ||
                _session == null ||
                !_session.IsGuest ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrEmpty(password))
            {
                _events.Publish(new AuthenticationPopupRequestedEvent(
                    "Cần một phiên chơi mới hợp lệ để đăng ký."));
                return;
            }

            _registrationVerifier = CreatePkceVerifier();
            _registrationUsername = username.Trim();
            _registrationPassword = password;
            PublishLoading("Đang tạo giao dịch đăng ký...");
            _network.SendBeginRegistrationRequestAsync(
                    Guid.NewGuid(),
                    _session.RefreshToken,
                    _store.GetOrCreateDeviceId(),
                    CreatePkceChallenge(_registrationVerifier))
                .Forget();
        }

        private void OnRegistrationStarted(RegistrationStartedEvent result)
        {
            if (!result.Success)
            {
                ClearRegistrationSecrets();
                HideLoading();
                _events.Publish(new AuthenticationPopupRequestedEvent(
                    result.Message));
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    result.DevelopmentAuthorizationCode))
            {
                HideLoading();
                Application.OpenURL(result.RegistrationUrl);
                return;
            }

            _network.SendCompleteDevelopmentRegistrationRequestAsync(
                    result.TransactionId,
                    result.DevelopmentAuthorizationCode,
                    _registrationVerifier,
                    _registrationUsername,
                    _registrationPassword)
                .Forget();
        }

        public void PlayNew()
        {
            if (_isAuthenticated)
                return;
            if (!_network.IsConnected)
            {
                ReconnectAndRetryAsync(PlayNew).Forget();
                return;
            }

            _isCreatingGuest = true;
            _isManualLogin = false;
            PublishLoading("Đang tạo phiên chơi mới...");
            _network.SendCreateGuestRequestAsync(
                    _store.GetOrCreateDeviceId())
                .Forget();
        }

        public void StageLogin(string username, string password)
        {
            _pendingUsername = username?.Trim();
            _pendingPassword = password;
            PublishEntry("Nhấn Chơi tiếp để xác thực tài khoản.");
        }

        public void Continue()
        {
            if (_isAuthenticated)
                return;
            if (!_network.IsConnected)
            {
                ReconnectAndRetryAsync(Continue).Forget();
                return;
            }

            _isCreatingGuest = false;
            PublishLoading("Đang kiểm tra tài khoản...");

            if (HasPendingCredentials())
            {
                _isManualLogin = true;
                string guestToken =
                    _session != null && _session.IsGuest
                        ? _session.RefreshToken
                        : null;
                _network.SendLoginRequestAsync(
                        _pendingUsername,
                        _pendingPassword,
                        _store.GetOrCreateDeviceId(),
                        guestToken)
                    .Forget();
                return;
            }

            _isManualLogin = false;
            if (_session == null ||
                string.IsNullOrWhiteSpace(_session.RefreshToken))
            {
                HideLoading();
                PublishEntry("Không có phiên đăng nhập để tiếp tục.");
                return;
            }

            _network.SendResumeAccountRequestAsync(
                    _session.RefreshToken,
                    _session.DeviceId)
                .Forget();
        }

        public void ReturnToAuthenticationEntry()
        {
            if (!_isAuthenticated)
            {
                PublishEntry("Choose Play New or sign in.");
                return;
            }

            _network.SendLeaveAccountSessionRequestAsync().Forget();
        }

        private void OnConnectionResult(ServerConnectionResultEvent result)
        {
            if (result.Result != ConnectionOutcome.Success)
            {
                HideLoading();
                PublishEntry(
                    string.IsNullOrWhiteSpace(result.Message)
                        ? "Không thể kết nối tới máy chủ."
                        : result.Message);
                return;
            }

            if (_settings.DevelopmentBypassEnabled)
                return;

            // A stored refresh token only changes Entry presentation. It is
            // never sent until the player explicitly chooses Continue.
            PublishEntry("Choose Play New or sign in.");
        }

        private void OnAuthenticationResult(AuthenticationResultEvent result)
        {
            switch (result.Result)
            {
                case AuthenticationOutcome.Success:
                    SaveReturnedSession(result);
                    ClearPendingCredentials();
                    CompleteSuccess(result);
                    return;

                case AuthenticationOutcome.AccountActive:
                    // Resume rotation can return a replacement token even when
                    // the active lease remains owned by another connection.
                    if (!_isManualLogin)
                        SaveReturnedSession(result);
                    ClearPendingCredentials();
                    _isManualLogin = false;
                    HideLoading();
                    _events.Publish(new AuthenticationPopupRequestedEvent(
                        "Tài khoản đang được đăng nhập ở nơi khác."));
                    PublishEntry(result.Message);
                    return;

                case AuthenticationOutcome.InvalidOrExpiredToken:
                    if (!_isManualLogin)
                    {
                        _store.Clear();
                        _session = null;
                    }
                    break;
            }

            if (_isCreatingGuest || _isManualLogin)
                ClearPendingCredentials();

            _isCreatingGuest = false;
            _isManualLogin = false;
            HideLoading();
            PublishEntry(result.Message);
        }

        private void CompleteSuccess(AuthenticationResultEvent result)
        {
            ClearRegistrationSecrets();
            _isAuthenticated = true;
            _isCreatingGuest = false;
            _isManualLogin = false;
            HideLoading();
            _events.Publish(new AuthenticationEntryRequiredEvent(
                string.Empty,
                isVisible: false));
            _events.Publish(new AccountReadyEvent(
                result.AccountKey,
                result.IsGuest));
        }

        private void OnAccountSessionLeaveResult(
            AccountSessionLeaveResultEvent result)
        {
            if (!result.Success)
            {
                Debug.LogWarning(
                    $"[Authentication] Cannot leave account session: " +
                    $"{result.Message}");
                return;
            }

            _isAuthenticated = false;
            HideLoading();
            PublishEntry(
                string.IsNullOrWhiteSpace(result.Message)
                    ? "Choose Play New or sign in."
                    : result.Message);
        }

        private void OnServerDisconnected(ServerDisconnectedEvent result)
        {
            _isAuthenticated = false;
            HideLoading();
            PublishEntry("Bạn bị mất kết nối máy chủ");
            if (result.IsForced)
            {
                _events.Publish(new AuthenticationPopupRequestedEvent(
                    "Bạn bị mất kết nối máy chủ",
                    reconnectOnClose: true));
            }
        }

        private void OnPopupDismissed(
            AuthenticationPopupDismissedEvent result)
        {
            if (result.ShouldReconnect)
                ReconnectAsync().Forget();
        }

        private async UniTaskVoid ReconnectAsync()
        {
            await UniTask.Delay(TimeSpan.FromMilliseconds(300));
            await _network.ConnectAsync();
        }

        private async UniTaskVoid ReconnectAndRetryAsync(Action retry)
        {
            if (_isReconnectingForRequest)
                return;

            _isReconnectingForRequest = true;
            PublishLoading("Đang kết nối lại máy chủ...");
            await _network.ConnectAsync();
            _isReconnectingForRequest = false;
            if (_network.IsConnected)
            {
                retry?.Invoke();
                return;
            }

            HideLoading();
            PublishEntry("Không thể kết nối tới máy chủ.");
        }

        private void SaveReturnedSession(AuthenticationResultEvent result)
        {
            if (string.IsNullOrWhiteSpace(result.RefreshToken))
                return;

            _session = new StoredAccountSession
            {
                AccountKey = result.AccountKey,
                IsGuest = result.IsGuest,
                RefreshToken = result.RefreshToken,
                DeviceId = _store.GetOrCreateDeviceId(),
                ExpiresAtUtcTicks =
                    result.RefreshTokenExpiresAtUtc.ToUniversalTime().Ticks,
            };
            _store.Save(_session);
        }

        private void PublishEntry(string message)
        {
            _events.Publish(new AuthenticationEntryRequiredEvent(
                message,
                CanContinue(),
                GetDisplayHint()));
        }

        private bool CanContinue() =>
            HasPendingCredentials() ||
            (_session != null &&
             !string.IsNullOrWhiteSpace(_session.RefreshToken));

        private bool HasPendingCredentials() =>
            !string.IsNullOrWhiteSpace(_pendingUsername) &&
            !string.IsNullOrEmpty(_pendingPassword);

        private string GetDisplayHint()
        {
            string value = HasPendingCredentials()
                ? _pendingUsername
                : _session?.AccountKey;
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            if (value.Length <= 4)
                return $"{value[0]}***";

            return $"{value.Substring(0, 3)}***{value[^1]}";
        }

        private void PublishLoading(string message)
        {
            _events.Publish(new AuthenticationLoadingEvent(
                true,
                0f,
                message));
        }

        private void HideLoading()
        {
            _events.Publish(new AuthenticationLoadingEvent(
                false,
                0f,
                string.Empty));
        }

        private void ClearPendingCredentials()
        {
            _pendingUsername = null;
            _pendingPassword = null;
        }

        private void ClearRegistrationSecrets()
        {
            _registrationVerifier = null;
            _registrationUsername = null;
            _registrationPassword = null;
        }

        private static string CreatePkceVerifier()
        {
            byte[] bytes = new byte[32];
            using RandomNumberGenerator random =
                RandomNumberGenerator.Create();
            random.GetBytes(bytes);
            return ToBase64Url(bytes);
        }

        private static string CreatePkceChallenge(string verifier)
        {
            using SHA256 sha256 = SHA256.Create();
            return ToBase64Url(
                sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
        }

        private static string ToBase64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        public void Dispose()
        {
            ClearPendingCredentials();
            ClearRegistrationSecrets();
            _connectionSubscription?.Dispose();
            _authenticationSubscription?.Dispose();
            _leaveSessionSubscription?.Dispose();
            _disconnectedSubscription?.Dispose();
            _popupDismissedSubscription?.Dispose();
            _registrationStartedSubscription?.Dispose();
        }
    }
}
