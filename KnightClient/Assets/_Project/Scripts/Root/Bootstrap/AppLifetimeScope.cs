using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Network;
using KnightOnline.Client.Network.Handlers;
using KnightOnline.Client.Core.Bootstrap;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Services;

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

        [Header("Authentication")]
        [SerializeField] private bool _developmentAuthenticationBypass = true;
        [SerializeField] private float _initialSessionCheckSeconds = 5f;
        [SerializeField] private float _sessionConflictRetrySeconds = 10f;
        [SerializeField] private string _registrationUrl =
            "https://account.example.com/register";

        [Header("Client Gameplay Fallbacks")]
        [SerializeField] private string _serverId = "server-1";
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
                _defaultMoveSpeed,
                _serverId));
            builder.RegisterInstance(new ClientAuthenticationSettings(
                _developmentAuthenticationBypass,
                _initialSessionCheckSeconds,
                _sessionConflictRetrySeconds,
                _registrationUrl));
            builder.Register<ILocalAccountSessionStore,
                DevelopmentAccountSessionStore>(Lifetime.Singleton);
            // Account authentication and its lease heartbeat must outlive the
            // Bootstrap scene. EnterWorld loads InGame with Single mode, so a
            // scene-scoped service would be disposed and silently stop lease
            // renewal while the persistent NetworkClient remains connected.
            builder.RegisterEntryPoint<AuthenticationFlowService>().AsSelf();
            builder.Register<IClientPacketHandler, ConnectResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, CreateCharacterResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, ListCharactersResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler,
                CharacterCreationCatalogResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler,
                CharacterNameAvailabilityResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, ListMonstersResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, MonsterHealthChangedHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, MonsterDiedHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, MonsterRespawnedHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, SelectCharacterResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, EnterWorldResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, AttackResultHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, ForcedDisconnectHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, CreateGuestResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, ResumeAccountResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler, LoginResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler,
                LeaveAccountSessionResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler,
                AccountSessionHeartbeatResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler,
                BeginRegistrationResponseHandler>(Lifetime.Singleton);
            builder.Register<IClientPacketHandler,
                CompleteDevelopmentRegistrationResponseHandler>(
                    Lifetime.Singleton);

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
