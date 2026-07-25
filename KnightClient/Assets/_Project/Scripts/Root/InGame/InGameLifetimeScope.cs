using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Player;
using KnightOnline.Client.Gameplay.Monster;
using KnightOnline.Client.Gameplay.Targeting;
using KnightOnline.Client.Gameplay.World;
using KnightOnline.Client.Input;
using KnightOnline.Client.UI;
using KnightOnline.Client.Root;
using UnityEngine;
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
            builder.Register<TargetSelectionService>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<PlayerInteraction>();
            builder.RegisterComponentInHierarchy<MonsterSpawner>();
            builder.RegisterComponentInHierarchy<SelectionMarkerView>();
            builder.RegisterComponentInHierarchy<InGameHUD>();
            builder.RegisterComponentInHierarchy<TargetHUD>();
            builder.RegisterComponentInHierarchy<NpcDialogUI>();
            builder.RegisterComponentInHierarchy<InGameSceneRoot>();

            builder.Register<CharacterData>(container =>
            {
                var session = container.Resolve<GameSession>();
                var settings = container.Resolve<ClientGameplaySettings>();
                var characterData = session.SelectedCharacter ?? new CharacterData(
                    "TestCharacter",
                    settings.InitialLevel,
                    settings.InitialMaximumHealth,
                    settings.InitialMaximumHealth,
                    settings.DefaultMoveSpeed);

                // Chỉ scene test trực tiếp mới dùng marker local. Character đã được
                // server chọn luôn giữ spawn position authoritative từ response.
                if (session.SelectedCharacter == null)
                {
                    var spawnPoint = FindAnyObjectByType<SpawnPoint>();
                    if (spawnPoint != null)
                        characterData.SpawnPosition = spawnPoint.transform.position;
                }

                return characterData;
            }, Lifetime.Singleton);
        }
    }
}
