using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Network;
using KnightOnline.Client.Core.Bootstrap;

namespace KnightOnline.Client.Root
{
    public sealed class AppLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IEventBus, EventBus>(Lifetime.Singleton);

            builder.RegisterComponentOnNewGameObject<NetworkClient>(
                Lifetime.Singleton, "NetworkClient")
                .DontDestroyOnLoad();

            builder.RegisterComponentOnNewGameObject<GameSession>(
                Lifetime.Singleton, "GameSession")
                .DontDestroyOnLoad();
        }

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Additive);
        }
    }
}