using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Player;
using KnightOnline.Client.Input;
using KnightOnline.Client.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>Owns dependencies used only while the gameplay scene is loaded.</summary>
    public sealed class InGameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var characterData = GameSession.Current?.SelectedCharacter
                                ?? new CharacterData("TestCharacter");

            if (GameSession.Current?.SelectedCharacter == null)
                Debug.LogWarning("[InGameScope] No GameSession found — running in test mode with default CharacterData.");

            builder.RegisterInstance(characterData);
            builder.Register<IMovementInputProvider, KeyboardMovementInput>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InGameHUD>();
        }
    }
}