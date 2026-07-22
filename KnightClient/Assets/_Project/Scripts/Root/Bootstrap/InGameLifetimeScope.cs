using KnightOnline.Client.Gameplay.Player;
using KnightOnline.Client.Input;
using VContainer;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>Owns dependencies used only while the gameplay scene is loaded.</summary>
    public sealed class InGameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IMovementInputProvider, KeyboardMovementInput>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<PlayerController>();
        }
    }
}
