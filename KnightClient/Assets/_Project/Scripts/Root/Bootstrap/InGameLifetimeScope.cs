using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Player;
using KnightOnline.Client.Input;
using KnightOnline.Client.UI;
using KnightOnline.Client.Root;
using VContainer;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    public sealed class InGameLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            var appScope = FindAnyObjectByType<AppLifetimeScope>();
            if (appScope != null)
            {
                parentReference.Object = appScope;
            }

            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IMovementInputProvider, KeyboardMovementInput>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InGameHUD>();

            builder.Register<CharacterData>(container =>
            {
                var session = container.Resolve<GameSession>();
                return session.SelectedCharacter ?? new CharacterData("TestCharacter");
            }, Lifetime.Singleton);
        }
    }
}