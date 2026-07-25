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

        private void Awake() =>
            _view = GetComponent<AuthenticationEntryPanel>();

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

            _view.PlayNewRequested += _authentication.PlayNew;
            _view.ContinueRequested += _authentication.Continue;
            _view.LoginRequested += _authentication.StageLogin;
            _view.ServerSelectionRequested += OnServerSelectionRequested;
            _entrySubscription =
                _events.Subscribe<AuthenticationEntryRequiredEvent>(
                    OnEntryRequired);
            _readySubscription =
                _events.Subscribe<AccountReadyEvent>(_ => _view.Hide());
        }

        private static void OnServerSelectionRequested()
        {
            // Waiting for a versioned server-list contract.
        }

        private void OnEntryRequired(AuthenticationEntryRequiredEvent state)
        {
            if (!state.IsVisible)
            {
                _view.Hide();
                return;
            }

            _view.ShowEntry(
                state.Message,
                state.CanContinue,
                state.AccountDisplayHint);
        }

        private void OnDestroy()
        {
            if (_view != null && _authentication != null)
            {
                _view.PlayNewRequested -= _authentication.PlayNew;
                _view.ContinueRequested -= _authentication.Continue;
                _view.LoginRequested -= _authentication.StageLogin;
                _view.ServerSelectionRequested -=
                    OnServerSelectionRequested;
            }

            _entrySubscription?.Dispose();
            _readySubscription?.Dispose();
        }
    }
}
