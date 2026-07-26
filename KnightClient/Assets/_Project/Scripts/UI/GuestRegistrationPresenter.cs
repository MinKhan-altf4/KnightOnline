using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Gameplay.Services;
using UnityEngine;
using VContainer;

namespace KnightOnline.Client.UI
{
    [RequireComponent(typeof(GuestRegistrationPanel))]
    public sealed class GuestRegistrationPresenter : MonoBehaviour
    {
        private GuestRegistrationPanel _view;
        private AuthenticationFlowService _authentication;
        private IEventBus _events;
        private IDisposable _resultSubscription;

        [Inject]
        public void Construct(
            AuthenticationFlowService authentication,
            IEventBus events)
        {
            Initialize(authentication, events);
        }

        public void Initialize(
            AuthenticationFlowService authentication,
            IEventBus events)
        {
            _authentication = authentication ??
                throw new ArgumentNullException(nameof(authentication));
            _events = events ??
                throw new ArgumentNullException(nameof(events));
            SubscribeToResults();
        }

        private void Awake() => _view = GetComponent<GuestRegistrationPanel>();

        private void OnEnable()
        {
            if (_view != null)
                _view.RegistrationRequested += Register;
            SubscribeToResults();
        }

        private void SubscribeToResults()
        {
            if (!isActiveAndEnabled ||
                _events == null ||
                _resultSubscription != null)
                return;

            _resultSubscription =
                _events.Subscribe<AuthenticationResultEvent>(OnResult);
        }

        private void OnDisable()
        {
            if (_view != null)
                _view.RegistrationRequested -= Register;
            _resultSubscription?.Dispose();
            _resultSubscription = null;
        }

        private void Register(string username, string password)
        {
            if (_authentication == null)
            {
                _view.SetBusy(false, "Authentication chưa được khởi tạo.");
                return;
            }

            _authentication.RegisterGuestForDevelopment(username, password);
        }

        private void OnResult(AuthenticationResultEvent result)
        {
            if (result.Result == AuthenticationOutcome.Success &&
                !result.IsGuest)
            {
                _view.Hide();
                return;
            }

            _view.SetBusy(false, result.Message);
        }
    }
}
