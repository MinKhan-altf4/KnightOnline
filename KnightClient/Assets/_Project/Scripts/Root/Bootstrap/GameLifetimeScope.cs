using VContainer;
using VContainer.Unity;
using UnityEngine;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Network;
using KnightOnline.Client.UI;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IEventBus, EventBus>(Lifetime.Singleton);

            builder.RegisterComponentOnNewGameObject<NetworkClient>(
                Lifetime.Singleton, "NetworkClient")
                .DontDestroyOnLoad();

            builder.RegisterEntryPoint<GameBootstrap>();

            // Đăng ký ConnectionStatusView - GameObject đã đặt sẵn trong scene,
            // VContainer tìm nó trong hierarchy và gọi [Inject] Construct() vào nó.
            builder.RegisterComponentInHierarchy<ConnectionStatusView>();
        }

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("<color=yellow>[Test]</color> Unity đã gọi được hàm Awake của GameLifetimeScope!");
        }
    }
}