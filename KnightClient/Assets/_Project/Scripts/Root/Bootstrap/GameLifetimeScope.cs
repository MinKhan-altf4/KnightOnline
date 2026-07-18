using VContainer;
using VContainer.Unity;
using UnityEngine;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Network;
using KnightOnline.Client.UI;
using KnightOnline.Client.Gameplay.Services;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private CharacterFlowController.PanelRefs _panelRefs;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IEventBus, EventBus>(Lifetime.Singleton);

            builder.RegisterComponentOnNewGameObject<NetworkClient>(
                Lifetime.Singleton, "NetworkClient")
                .DontDestroyOnLoad();

            builder.RegisterEntryPoint<GameBootstrap>();

            builder.RegisterInstance(_panelRefs);
            builder.RegisterEntryPoint<CharacterFlowController>();

            // Đăng ký ConnectionStatusView - GameObject đã đặt sẵn trong scene,
            // VContainer tìm nó trong hierarchy và gọi [Inject] Construct() vào nó.
            builder.RegisterComponentInHierarchy<ConnectionStatusView>();
            builder.Register<CharacterService>(Lifetime.Singleton);
            builder.Register<CharacterSelectionService>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<CharacterCreationView>();
            builder.RegisterComponentInHierarchy<CharacterSelectView>();
        }

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("<color=yellow>[Test]</color> Unity đã gọi được hàm Awake của GameLifetimeScope!");
        }
    }
}