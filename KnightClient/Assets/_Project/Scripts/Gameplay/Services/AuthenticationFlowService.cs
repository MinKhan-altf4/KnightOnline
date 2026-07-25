using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Network;
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
        private CancellationTokenSource _flowCancellation;
        private StoredAccountSession _session;
        private bool _isCreatingGuest;
        private bool _isManualLogin;
        private bool _isInitialResume;
        private bool _minimumCheckComplete;
        private AuthenticationResultEvent? _pendingSuccess;

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

            if (_settings.DevelopmentBypassEnabled)
                return;

            _session = _store.Load();
        }

        public void PlayNew()
        {
            CancelFlow();
            _isCreatingGuest = true;
            _isManualLogin = false;
            _network.SendCreateGuestRequestAsync(
                    _store.GetOrCreateDeviceId())
                .Forget();
        }

        public void Login(string username, string password)
        {
            CancelFlow();
            _isCreatingGuest = false;
            _isManualLogin = true;
            string guestToken =
                _session != null && _session.IsGuest
                    ? _session.RefreshToken
                    : null;
            _network.SendLoginRequestAsync(
                    username,
                    password,
                    _store.GetOrCreateDeviceId(),
                    guestToken)
                .Forget();
        }

        private void OnConnectionResult(ServerConnectionResultEvent result)
        {
            if (result.Result != ConnectionOutcome.Success ||
                _settings.DevelopmentBypassEnabled)
                return;

            if (_session == null ||
                string.IsNullOrWhiteSpace(_session.RefreshToken))
            {
                _events.Publish(new AuthenticationEntryRequiredEvent(
                    "Choose Play New or sign in."));
                return;
            }

            BeginInitialResume(
                _settings.InitialSessionCheckSeconds,
                "Checking saved account session...");
        }

        private void BeginInitialResume(float minimumSeconds, string message)
        {
            CancelFlow();
            _isCreatingGuest = false;
            _isManualLogin = false;
            _isInitialResume = true;
            _minimumCheckComplete = false;
            _pendingSuccess = null;
            _flowCancellation = new CancellationTokenSource();
            _ = CompleteMinimumCheckDelayAsync(
                minimumSeconds,
                message,
                _flowCancellation.Token);
            _network.SendResumeAccountRequestAsync(
                    _session.RefreshToken,
                    _session.DeviceId)
                .Forget();
        }

        private async UniTaskVoid CompleteMinimumCheckDelayAsync(
            float delaySeconds,
            string message,
            CancellationToken cancellationToken)
        {
            float remaining = Math.Max(0f, delaySeconds);
            while (remaining > 0f)
            {
                _events.Publish(new AuthenticationLoadingEvent(
                    true,
                    remaining,
                    message));
                await UniTask.Delay(
                    TimeSpan.FromSeconds(Math.Min(1f, remaining)),
                    cancellationToken: cancellationToken);
                remaining -= 1f;
            }

            _minimumCheckComplete = true;
            if (_pendingSuccess.HasValue)
                CompleteSuccess(_pendingSuccess.Value);
        }

        private void BeginConflictRetry()
        {
            CancelFlow();
            _isInitialResume = false;
            _pendingSuccess = null;
            _flowCancellation = new CancellationTokenSource();
            _ = RetryAfterDelayAsync(
                _settings.SessionConflictRetrySeconds,
                _flowCancellation.Token);
        }

        private async UniTaskVoid RetryAfterDelayAsync(
            float delaySeconds,
            CancellationToken cancellationToken)
        {
            float remaining = Math.Max(0f, delaySeconds);
            while (remaining > 0f)
            {
                _events.Publish(new AuthenticationLoadingEvent(
                    true,
                    remaining,
                    "Account is active. Retrying..."));
                await UniTask.Delay(
                    TimeSpan.FromSeconds(Math.Min(1f, remaining)),
                    cancellationToken: cancellationToken);
                remaining -= 1f;
            }

            await _network.ConnectAsync();
            await _network.SendResumeAccountRequestAsync(
                _session.RefreshToken,
                _session.DeviceId);
        }

        private void OnAuthenticationResult(AuthenticationResultEvent result)
        {
            if (result.Result == AuthenticationOutcome.Success)
            {
                SaveReturnedSession(result);
                if (_isInitialResume && !_minimumCheckComplete)
                {
                    _pendingSuccess = result;
                    return;
                }

                CompleteSuccess(result);
                return;
            }

            if (result.Result == AuthenticationOutcome.SessionConflict)
            {
                SaveReturnedSession(result);
                BeginConflictRetry();
                return;
            }

            if (result.Result ==
                AuthenticationOutcome.InvalidOrExpiredToken)
            {
                _store.Clear();
                _session = null;
            }

            if (_isCreatingGuest || _isManualLogin)
            {
                _events.Publish(new AuthenticationEntryRequiredEvent(
                    result.Message));
            }
        }

        private void CompleteSuccess(AuthenticationResultEvent result)
        {
            CancelFlow();
            _events.Publish(new AuthenticationLoadingEvent(
                false,
                0f,
                string.Empty));
            _events.Publish(new AccountReadyEvent(
                result.AccountKey,
                result.IsGuest));
            _isCreatingGuest = false;
            _isManualLogin = false;
            _isInitialResume = false;
            _pendingSuccess = null;
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

        private void CancelFlow()
        {
            _flowCancellation?.Cancel();
            _flowCancellation?.Dispose();
            _flowCancellation = null;
        }

        public void Dispose()
        {
            CancelFlow();
            _connectionSubscription?.Dispose();
            _authenticationSubscription?.Dispose();
        }
    }
}
