using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Player;
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
            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InGameHUD>();

            builder.Register<CharacterData>(container =>
            {
                var session = container.Resolve<GameSession>();
                var characterData = session.SelectedCharacter ?? new CharacterData("TestCharacter");

                // Ghi đè SpawnPosition từ marker trong map, nếu tìm thấy.
                // Không throw nếu thiếu SpawnPoint - fallback về Vector2.zero
                // (giá trị mặc định sẵn có của CharacterData), tránh crash
                // khi test 1 scene InGame chưa kịp đặt SpawnPoint.
                var spawnPoint = FindAnyObjectByType<SpawnPoint>();
                if (spawnPoint != null)
                {
                    characterData.SpawnPosition = spawnPoint.transform.position;
                }

                return characterData;
            }, Lifetime.Singleton);
        }
    }
}