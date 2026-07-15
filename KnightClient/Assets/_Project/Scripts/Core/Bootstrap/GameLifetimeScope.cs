using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Đăng ký GameBootstrap làm Entry Point của game
            builder.RegisterEntryPoint<GameBootstrap>();
        }

        protected override void Awake()
        {
            // Gọi base.Awake để kích hoạt VContainer hoạt động bình thường
            base.Awake(); 

            Debug.Log("<color=yellow>[Test]</color> Unity đã gọi được hàm Awake của GameLifetimeScope!");
        }
    }
}