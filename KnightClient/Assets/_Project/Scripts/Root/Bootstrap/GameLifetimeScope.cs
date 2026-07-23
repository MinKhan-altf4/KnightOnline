using VContainer;
using VContainer.Unity;
using UnityEngine;
using KnightOnline.Client.UI;
using KnightOnline.Client.Gameplay.Services;
using KnightOnline.Client.Root;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private CharacterFlowController.PanelRefs _panelRefs;

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
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<GameBootstrap>();

            builder.RegisterInstance(_panelRefs);
            builder.RegisterEntryPoint<CharacterFlowController>();

            builder.RegisterComponentInHierarchy<ConnectionStatusView>();
            builder.Register<CharacterService>(Lifetime.Singleton);
            builder.Register<CharacterSelectionService>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<CharacterCreationView>();
            builder.RegisterComponentInHierarchy<CharacterSelectView>();
        }
    }
}