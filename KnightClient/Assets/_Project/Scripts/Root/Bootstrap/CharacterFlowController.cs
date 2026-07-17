using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Gameplay.Services;
using KnightOnline.Client.Shared.Packets;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>Owns the Bootstrap -> creation/select -> game scene decisions.</summary>
    public sealed class CharacterFlowController : IStartable, IDisposable
    {
        public const string CharacterCreationScene = "CharacterCreation";
        public const string CharacterSelectScene = "CharacterSelect";
        public const string InGameScene = "InGame";

        private readonly IEventBus _eventBus;
        private readonly CharacterService _characterService;
        private IDisposable _connectionSubscription;
        private IDisposable _listSubscription;
        private IDisposable _creationSubscription;
        private IDisposable _selectionSubscription;

        public CharacterFlowController(IEventBus eventBus, CharacterService characterService)
        {
            _eventBus = eventBus;
            _characterService = characterService;
        }

        public void Start()
        {
            _connectionSubscription = _eventBus.Subscribe<ServerConnectionResultEvent>(OnConnectionResult);
            _listSubscription = _eventBus.Subscribe<CharacterListReceivedEvent>(OnCharacterListReceived);
            _creationSubscription = _eventBus.Subscribe<CharacterCreationResultEvent>(OnCharacterCreationResult);
            _selectionSubscription = _eventBus.Subscribe<CharacterSelectedEvent>(OnCharacterSelected);
        }

        private void OnConnectionResult(ServerConnectionResultEvent e)
        {
            if (e.Result == ConnectResult.Success) _ = _characterService.RequestListCharacters();
        }

        private void OnCharacterListReceived(CharacterListReceivedEvent e)
        {
            LoadScene(e.Characters != null && e.Characters.Count > 0
                ? CharacterSelectScene
                : CharacterCreationScene);
        }

        private void OnCharacterCreationResult(CharacterCreationResultEvent e)
        {
            if (e.Success) _ = _characterService.RequestListCharacters();
        }

        private void OnCharacterSelected(CharacterSelectedEvent e)
        {
            if (e.Character != null) LoadScene(InGameScene);
        }

        private static void LoadScene(string sceneName)
        {
            if (SceneManager.GetActiveScene().name != sceneName)
                SceneManager.LoadScene(sceneName);
        }

        public void Dispose()
        {
            _connectionSubscription?.Dispose();
            _listSubscription?.Dispose();
            _creationSubscription?.Dispose();
            _selectionSubscription?.Dispose();
        }
    }
}
