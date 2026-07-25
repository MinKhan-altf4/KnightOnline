using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Network;
using KnightOnline.Client.Network.Handlers;
using KnightOnline.Client.Core.Bootstrap;
using KnightOnline.Client.Data.Models;

namespace KnightOnline.Client.Root
{
    public sealed class AppLifetimeScope : LifetimeScope
    {
        [Header("Network")]
        [SerializeField] private string _serverHost = "127.0.0.1";
        [SerializeField] private int _serverPort = 7777;
        [SerializeField] private int _maximumPacketBytes = 1024 * 1024;
        [Header("Scenes")]
        [SerializeField] private string _bootstrapSceneName = "Bootstrap";

        [Header("Client Gameplay Fallbacks")]
        [SerializeField] private int _initialLevel = 1;
        [SerializeField] private int _initialMaximumHealth = 100;
        [SerializeField] private float _defaultMoveSpeed = 4f;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IEventBus, EventBus>(Lifetime.Singleton);
            builder.RegisterInstance(new NetworkSettings(
                _serverHost,
                _serverPort,
                _maximumPacketBytes));
            builder.RegisterInstance(new ClientGameplaySettings(
                _initialLevel,
                _initialMaximumHealth,
                _defaultMoveSpeed));
            builder.Register<IClientPacketHandler, ConnectResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, CreateCharacterResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, ListCharactersResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, ListMonstersResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, MonsterHealthChangedHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, MonsterDiedHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, MonsterRespawnedHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, SelectCharacterResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, AttackResultHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, ForcedDisconnectHandler>(Lifetime.Singleton);

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
            SceneManager.LoadScene(_bootstrapSceneName, LoadSceneMode.Additive);
        }
    }
}
