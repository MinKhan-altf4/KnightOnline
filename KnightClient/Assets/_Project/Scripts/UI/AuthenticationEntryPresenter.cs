using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Gameplay.Services;
using UnityEngine;
using VContainer;

namespace KnightOnline.Client.UI
{
    [RequireComponent(typeof(AuthenticationEntryPanel))]
    public sealed class AuthenticationEntryPresenter : MonoBehaviour
    {
        private AuthenticationEntryPanel _view;
        private AuthenticationFlowService _authentication;
        private IEventBus _events;
        private IDisposable _entrySubscription;
        private IDisposable _readySubscription;

        [Inject]
        public void Construct(
            AuthenticationFlowService authentication,
            IEventBus events)
        {
            _authentication = authentication;
            _events = events;
        }

        private void Awake()
        {
            _view = GetComponent<AuthenticationEntryPanel>();
        }

        private void Start()
        {
            if (_authentication == null || _events == null)
            {
                Debug.LogError(
                    "[AuthenticationEntryPresenter] VContainer injection failed.",
                    this);
                enabled = false;
                return;
            }

            _view.PlayNewRequested += OnPlayNewRequested;
            _view.LoginRequested += OnLoginRequested;
            _view.ServerSelectionRequested += OnServerSelectionRequested;
            _entrySubscription =
                _events.Subscribe<AuthenticationEntryRequiredEvent>(
                    OnEntryRequired);
            _readySubscription =
                _events.Subscribe<AccountReadyEvent>(OnAccountReady);
        }

        private void OnPlayNewRequested()
        {
            _authentication.PlayNew();
        }

        private void OnLoginRequested(string username, string password)
        {
            _authentication.Login(username, password);
        }

        private static void OnServerSelectionRequested()
        {
            // Server selection is intentionally presentation-only until
            // a versioned server-list contract is available.
        }

        private void OnEntryRequired(AuthenticationEntryRequiredEvent result)
        {
            _view.ShowEntry(result.Message);
        }

        private void OnAccountReady(AccountReadyEvent _)
        {
            _view.Hide();
        }

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.PlayNewRequested -= OnPlayNewRequested;
                _view.LoginRequested -= OnLoginRequested;
                _view.ServerSelectionRequested -=
                    OnServerSelectionRequested;
            }

            _entrySubscription?.Dispose();
            _readySubscription?.Dispose();
        }
    }
}
