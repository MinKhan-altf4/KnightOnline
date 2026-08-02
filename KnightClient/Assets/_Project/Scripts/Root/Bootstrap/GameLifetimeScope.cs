using VContainer;
using VContainer.Unity;
using UnityEngine;
using KnightOnline.Client.UI;
using KnightOnline.Client.Gameplay.Services;
using KnightOnline.Client.Root;
using KnightOnline.Client.Core.Events;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private CharacterFlowController.PanelRefs _panelRefs;
        [Header("Scene UI")]
        [SerializeField] private CharacterCreationView _characterCreationView;
        [SerializeField] private CharacterSelectView _characterSelectView;
        [SerializeField] private GuestRegistrationPresenter
            _guestRegistrationPresenter;
        [SerializeField] private AuthenticationEntryPresenter
            _authenticationEntryPresenter;
        [SerializeField] private AuthenticationLoadingPanel
            _authenticationLoadingPanel;
        [SerializeField] private ConnectionStatusView _connectionStatusView;

        protected override void Awake()
        {
            // Tìm AppLifetimeScope đang tồn tại (DontDestroyOnLoad) và
            // chỉ định làm Parent TRƯỚC KHI base.Awake() chạy Build().
            var appScope = FindAnyObjectByType<AppLifetimeScope>();
            if (appScope != null)
            {
                parentReference.Object = appScope;
            }

            base.Awake();
            InjectSceneUi();
        }

        private void InjectSceneUi()
        {
            if (_characterCreationView == null ||
                _characterSelectView == null ||
                _guestRegistrationPresenter == null ||
                _authenticationEntryPresenter == null ||
                _authenticationLoadingPanel == null ||
                _connectionStatusView == null)
            {
                Debug.LogError(
                    "[GameLifetimeScope] Missing Scene UI reference.",
                    this);
                return;
            }

            IEventBus eventBus = Container.Resolve<IEventBus>();
            AuthenticationFlowService authentication =
                Container.Resolve<AuthenticationFlowService>();
            CharacterSelectionService selection =
                Container.Resolve<CharacterSelectionService>();

            _characterCreationView.Initialize(eventBus);
            _characterSelectView.Initialize(eventBus, selection);
            _guestRegistrationPresenter.Initialize(
                authentication,
                eventBus);
            Container.Inject(_authenticationEntryPresenter);
            Container.Inject(_authenticationLoadingPanel);
            Container.Inject(_connectionStatusView);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<GameBootstrap>();

            builder.RegisterInstance(_panelRefs);
            builder.RegisterEntryPoint<CharacterFlowController>();

            builder.Register<CharacterService>(Lifetime.Singleton);
            builder.Register<CharacterSelectionService>(Lifetime.Singleton);
        }
    }
}
