using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        private IDisposable _heartbeatResponseSubscription;
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
        private bool _registrationPending;
        private CancellationTokenSource _heartbeatCancellation;
        private CancellationTokenSource _sessionRetryCancellation;
        private Guid _sessionGeneration;

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
            _heartbeatResponseSubscription =
                _events.Subscribe<AccountSessionHeartbeatEvent>(
                    OnHeartbeatResponse);

            if (!_settings.DevelopmentBypassEnabled)
            {
                try
                {
                    _session = _store.Load();
                }
                catch (Exception exception)
                {
                    ReportCredentialStoreUnavailable(exception);
                }
            }
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
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrEmpty(password))
            {
                _events.Publish(new AuthenticationPopupRequestedEvent(
                    "Cần một phiên chơi mới hợp lệ để đăng ký."));
                return;
            }

            _registrationUsername = username.Trim();
            _registrationPassword = password;
            _registrationPending = true;

            if (!_isAuthenticated)
            {
                if (!_network.IsConnected)
                {
                    ReconnectAndRetryAsync(
                        () => RegisterGuestForDevelopment(
                            _registrationUsername,
                            _registrationPassword)).Forget();
                    return;
                }

                if (!TryGetDeviceId(out string deviceId))
                    return;

                _isCreatingGuest = true;
                _isManualLogin = false;
                PublishLoading("Đang chuẩn bị phiên đăng ký...");
                _network.SendCreateGuestRequestAsync(deviceId)
                    .Forget();
                return;
            }

            if (_session == null || !_session.IsGuest)
            {
                ClearRegistrationSecrets();
                _events.Publish(new AuthenticationPopupRequestedEvent(
                    "Tài khoản hiện tại đã được đăng ký."));
                return;
            }

            BeginRegistrationTransaction();
        }

        private void BeginRegistrationTransaction()
        {
            if (_session == null ||
                !_session.IsGuest ||
                string.IsNullOrWhiteSpace(_session.RefreshToken))
            {
                ClearRegistrationSecrets();
                HideLoading();
                _events.Publish(new AuthenticationPopupRequestedEvent(
                    "Không thể chuẩn bị phiên đăng ký."));
                return;
            }

            if (!TryGetDeviceId(out string deviceId))
                return;

            _registrationVerifier = CreatePkceVerifier();
            PublishLoading("Đang tạo giao dịch đăng ký...");
            _network.SendBeginRegistrationRequestAsync(
                    Guid.NewGuid(),
                    _session.RefreshToken,
                    deviceId,
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
            CancelSessionRetry();
            if (_isAuthenticated)
                return;
            if (!_network.IsConnected)
            {
                ReconnectAndRetryAsync(PlayNew).Forget();
                return;
            }

            if (!TryGetDeviceId(out string deviceId))
                return;

            _isCreatingGuest = true;
            _isManualLogin = false;
            PublishLoading("Đang tạo phiên chơi mới...");
            _network.SendCreateGuestRequestAsync(deviceId)
                .Forget();
        }

        public void StageLogin(string username, string password)
        {
            CancelSessionRetry();
            _pendingUsername = username?.Trim();
            _pendingPassword = password;
            PublishEntry("Nhấn Chơi tiếp để xác thực tài khoản.");
        }

        public void Continue()
        {
            if (_isAuthenticated)
            {
                HideLoading();
                _events.Publish(new AuthenticationEntryRequiredEvent(
                    string.Empty,
                    isVisible: false));
                _events.Publish(new AccountReadyEvent(
                    _session?.AccountKey ?? string.Empty,
                    _session?.IsGuest ?? false));
                return;
            }
            if (!_network.IsConnected)
            {
                ReconnectAndRetryAsync(Continue).Forget();
                return;
            }

            _isCreatingGuest = false;
            PublishLoading("Đang kiểm tra tài khoản...");

            if (HasPendingCredentials())
            {
                if (!TryGetDeviceId(out string deviceId))
                    return;

                _isManualLogin = true;
                string guestToken =
                    _session != null && _session.IsGuest
                        ? _session.RefreshToken
                        : null;
                _network.SendLoginRequestAsync(
                        _pendingUsername,
                        _pendingPassword,
                        deviceId,
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

        public void ShowEntryKeepingSession()
        {
            HideLoading();
            PublishEntry("Choose Continue or another account.");
        }

        public void LogoutToAuthenticationEntry()
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
                    CancelSessionRetry();
                    if (!SaveReturnedSession(result))
                    {
                        _network.Disconnect();
                        return;
                    }
                    if (!StartHeartbeat(result))
                        return;
                    if (_registrationPending && result.IsGuest)
                    {
                        _isAuthenticated = true;
                        _isCreatingGuest = false;
                        _isManualLogin = false;
                        BeginRegistrationTransaction();
                        return;
                    }
                    ClearPendingCredentials();
                    CompleteSuccess(result);
                    return;

                case AuthenticationOutcome.SessionConflict:
                    if (!SaveReturnedSession(result))
                    {
                        _network.Disconnect();
                        return;
                    }
                    ClearPendingCredentials();
                    _isCreatingGuest = false;
                    _isManualLogin = false;
                    StartSessionRetryCountdown(Math.Clamp(
                        result.RetryAfterSeconds,
                        1,
                        60));
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

                case AuthenticationOutcome.ServerFull:
                    // Authentication/token rotation may have committed before
                    // active-account admission. Persist the returned session so
                    // a later retry does not reuse a revoked refresh token.
                    SaveReturnedSession(result);
                    ClearPendingCredentials();
                    _isCreatingGuest = false;
                    _isManualLogin = false;
                    HideLoading();
                    _events.Publish(new AuthenticationPopupRequestedEvent(
                        "Máy chủ hiện đã đầy. Vui lòng thử lại sau."));
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
            if (_registrationPending)
                ClearRegistrationSecrets();

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
            StopHeartbeat();
            HideLoading();
            PublishEntry(
                string.IsNullOrWhiteSpace(result.Message)
                    ? "Choose Play New or sign in."
                    : result.Message);
        }

        private void OnServerDisconnected(ServerDisconnectedEvent result)
        {
            _isAuthenticated = false;
            StopHeartbeat();
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

        private bool SaveReturnedSession(AuthenticationResultEvent result)
        {
            if (string.IsNullOrWhiteSpace(result.RefreshToken))
                return true;

            if (!TryGetDeviceId(out string deviceId))
                return false;

            _session = new StoredAccountSession
            {
                AccountKey = result.AccountKey,
                DisplayName = result.DisplayName,
                IsGuest = result.IsGuest,
                RefreshToken = result.RefreshToken,
                DeviceId = deviceId,
                ExpiresAtUtcTicks =
                    result.RefreshTokenExpiresAtUtc.ToUniversalTime().Ticks,
            };
            try
            {
                _store.Save(_session);
                return true;
            }
            catch (Exception exception)
            {
                _session = null;
                ReportCredentialStoreUnavailable(exception);
                return false;
            }
        }

        private bool StartHeartbeat(AuthenticationResultEvent result)
        {
            StopHeartbeat();
            if (result.SessionGeneration == Guid.Empty ||
                result.HeartbeatIntervalSeconds <= 0)
            {
                Debug.LogError(
                    "[Authentication] Server did not establish a valid " +
                    "account session lease.");
                _network.Disconnect();
                return false;
            }

            _sessionGeneration = result.SessionGeneration;
            _heartbeatCancellation = new CancellationTokenSource();
            RunHeartbeatAsync(
                    TimeSpan.FromSeconds(result.HeartbeatIntervalSeconds),
                    _heartbeatCancellation.Token)
                .Forget();
            return true;
        }

        private async UniTask RunHeartbeatAsync(
            TimeSpan interval,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Authentication lease time is server UTC and must never
                    // depend on gameplay timeScale (pause, death overlay or
                    // slow-motion presentation).
                    await UniTask.Delay(
                        interval,
                        DelayType.Realtime,
                        cancellationToken: cancellationToken);
                    if (!_isAuthenticated || !_network.IsConnected)
                        return;

                    await _network.SendAccountSessionHeartbeatAsync(
                        _sessionGeneration);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when leaving the account, disconnecting or quitting.
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Authentication] Heartbeat loop stopped: " +
                    $"{exception.Message}");
                if (_network.IsConnected)
                    _network.Disconnect();
            }
        }

        private void OnHeartbeatResponse(
            AccountSessionHeartbeatEvent heartbeat)
        {
            if (heartbeat.Renewed)
                return;

            Debug.LogWarning(
                "[Authentication] Server rejected account heartbeat.");
            _network.Disconnect();
        }

        private void StopHeartbeat()
        {
            _heartbeatCancellation?.Cancel();
            _heartbeatCancellation?.Dispose();
            _heartbeatCancellation = null;
            _sessionGeneration = Guid.Empty;
        }

        private bool TryGetDeviceId(out string deviceId)
        {
            try
            {
                deviceId = _store.GetOrCreateDeviceId();
                return !string.IsNullOrWhiteSpace(deviceId);
            }
            catch (Exception exception)
            {
                deviceId = null;
                ReportCredentialStoreUnavailable(exception);
                return false;
            }
        }

        private void ReportCredentialStoreUnavailable(Exception exception)
        {
            HideLoading();
            Debug.LogError(
                $"[Authentication] Secure credential store unavailable: " +
                $"{exception.Message}");
            _events.Publish(new AuthenticationPopupRequestedEvent(
                "Secure credential storage is unavailable in this build."));
            PublishEntry("Cannot access the stored account session.");
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
                : !string.IsNullOrWhiteSpace(_session?.DisplayName)
                    ? _session.DisplayName
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

        private void PublishLoading(string message, float remainingSeconds)
        {
            _events.Publish(new AuthenticationLoadingEvent(
                true,
                remainingSeconds,
                message));
        }

        private void StartSessionRetryCountdown(int retryAfterSeconds)
        {
            CancelSessionRetry();
            _sessionRetryCancellation = new CancellationTokenSource();
            RunSessionRetryCountdownAsync(
                    retryAfterSeconds,
                    _sessionRetryCancellation.Token)
                .Forget();
        }

        private async UniTask RunSessionRetryCountdownAsync(
            int retryAfterSeconds,
            CancellationToken cancellationToken)
        {
            try
            {
                for (int remaining = retryAfterSeconds;
                     remaining > 0;
                     remaining--)
                {
                    PublishLoading(
                        "Đang chờ phiên đăng xuất kết thúc...",
                        remaining);
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(1),
                        DelayType.Realtime,
                        cancellationToken: cancellationToken);
                }

                _sessionRetryCancellation?.Dispose();
                _sessionRetryCancellation = null;
                Continue();
            }
            catch (OperationCanceledException)
            {
                // Expected when the player selects another authentication path.
            }
        }

        private void CancelSessionRetry()
        {
            _sessionRetryCancellation?.Cancel();
            _sessionRetryCancellation?.Dispose();
            _sessionRetryCancellation = null;
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
            _registrationPending = false;
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
            StopHeartbeat();
            CancelSessionRetry();
            ClearPendingCredentials();
            ClearRegistrationSecrets();
            _connectionSubscription?.Dispose();
            _authenticationSubscription?.Dispose();
            _leaveSessionSubscription?.Dispose();
            _disconnectedSubscription?.Dispose();
            _popupDismissedSubscription?.Dispose();
            _registrationStartedSubscription?.Dispose();
            _heartbeatResponseSubscription?.Dispose();
        }
    }
}
