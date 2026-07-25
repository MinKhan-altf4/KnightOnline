using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>
    /// Điều phối luồng nhân vật trong Bootstrap:
    /// - Hiện/ẩn panel tạo/chọn nhân vật theo trạng thái server.
    /// - Sau khi nhân vật được chọn, lưu vào GameSession và chuyển sang
    ///   scene InGame. Bootstrap bị unload; InGame tự quản lý dependency
    ///   gameplay thông qua InGameLifetimeScope.
    /// </summary>
    public sealed class CharacterFlowController : IStartable, IDisposable
    {
        [Serializable]
        public class PanelRefs
        {
            public GameObject CharacterCreationPanel;
            public GameObject CharacterSelectPanel;
            [Tooltip("Gameplay scene registered in Build Settings.")]
            public string InGameSceneName = "InGame";
        }

        private readonly IEventBus _eventBus;
        private readonly CharacterService _characterService;
        private readonly GameSession _gameSession;
        private readonly PanelRefs _panels;
        private readonly ClientAuthenticationSettings _authenticationSettings;

        private IDisposable _connectionSubscription;
        private IDisposable _listSubscription;
        private IDisposable _creationSubscription;
        private IDisposable _selectionSubscription;
        private IDisposable _selectionFailedSubscription;
        private IDisposable _accountReadySubscription;

        public CharacterFlowController(IEventBus eventBus, CharacterService characterService,
            GameSession gameSession, PanelRefs panels,
            ClientAuthenticationSettings authenticationSettings)
        {
            _eventBus = eventBus;
            _characterService = characterService;
            _gameSession = gameSession;
            _panels = panels;
            _authenticationSettings = authenticationSettings;
        }

        public void Start()
        {
            _connectionSubscription = _eventBus.Subscribe<ServerConnectionResultEvent>(OnConnectionResult);
            _accountReadySubscription =
                _eventBus.Subscribe<AccountReadyEvent>(
                    OnAccountReady);
            _listSubscription = _eventBus.Subscribe<CharacterListReceivedEvent>(OnCharacterListReceived);
            _creationSubscription = _eventBus.Subscribe<CharacterCreationResultEvent>(OnCharacterCreationResult);
            _selectionSubscription = _eventBus.Subscribe<CharacterSelectedEvent>(OnCharacterSelected);
            _selectionFailedSubscription =
                _eventBus.Subscribe<CharacterSelectionFailedEvent>(
                    e => Debug.LogError($"[Character] Selection failed: {e.Message}"));

            SetActivePanel(null); // Ẩn hết ban đầu, chờ kết quả kết nối
        }

        private void OnConnectionResult(ServerConnectionResultEvent e)
        {
            if (e.Result == ConnectionOutcome.Success &&
                _authenticationSettings.DevelopmentBypassEnabled)
                _ = _characterService.RequestListCharacters();
        }

        private void OnAccountReady(AccountReadyEvent e)
        {
            _ = _characterService.RequestListCharacters();
        }

        private void OnCharacterListReceived(CharacterListReceivedEvent e)
        {
            SetActivePanel(e.Characters != null && e.Characters.Count > 0
                ? _panels.CharacterSelectPanel
                : _panels.CharacterCreationPanel);
        }

        private void OnCharacterCreationResult(CharacterCreationResultEvent e)
        {
            if (e.Success) _ = _characterService.RequestListCharacters();
        }

        private void OnCharacterSelected(CharacterSelectedEvent e)
        {
            if (e.Character == null) return;

            // Lưu nhân vật vào GameSession (DontDestroyOnLoad) trước khi
            // Bootstrap bị unload, để InGameSceneRoot có thể đọc lại.
            _gameSession.SetSelectedCharacter(e.Character);
            SceneManager.LoadSceneAsync(_panels.InGameSceneName, LoadSceneMode.Single);
        }

        private void SetActivePanel(GameObject activePanel)
        {
            SetPanel(_panels.CharacterCreationPanel, activePanel);
            SetPanel(_panels.CharacterSelectPanel, activePanel);
        }

        private static void SetPanel(GameObject panel, GameObject activePanel)
        {
            if (panel != null) panel.SetActive(panel == activePanel);
        }

        public void Dispose()
        {
            _connectionSubscription?.Dispose();
            _listSubscription?.Dispose();
            _creationSubscription?.Dispose();
            _selectionSubscription?.Dispose();
            _selectionFailedSubscription?.Dispose();
            _accountReadySubscription?.Dispose();
        }
    }
}
