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
        private readonly AuthenticationFlowService _authenticationFlow;
        private readonly ClientGameplaySettings _gameplaySettings;
        private readonly CharacterSelectionService _characterSelection;

        private IDisposable _connectionSubscription;
        private IDisposable _listSubscription;
        private IDisposable _listFailedSubscription;
        private IDisposable _creationSubscription;
        private IDisposable _creationRequestedSubscription;
        private IDisposable _selectionSubscription;
        private IDisposable _gameplaySessionReadySubscription;
        private IDisposable _enterWorldFailedSubscription;
        private IDisposable _selectionFailedSubscription;
        private IDisposable _accountReadySubscription;
        private IDisposable _backSubscription;
        private IDisposable _entryRequiredSubscription;
        private IDisposable _creationSlotSubscription;
        private IDisposable _creationCancelledSubscription;
        private IDisposable _nameCheckSubscription;

        public CharacterFlowController(IEventBus eventBus, CharacterService characterService,
            GameSession gameSession, PanelRefs panels,
            ClientAuthenticationSettings authenticationSettings,
            AuthenticationFlowService authenticationFlow,
            ClientGameplaySettings gameplaySettings,
            CharacterSelectionService characterSelection)
        {
            _eventBus = eventBus;
            _characterService = characterService;
            _gameSession = gameSession;
            _panels = panels;
            _authenticationSettings = authenticationSettings;
            _authenticationFlow = authenticationFlow;
            _gameplaySettings = gameplaySettings;
            _characterSelection = characterSelection;
        }

        public void Start()
        {
            _connectionSubscription = _eventBus.Subscribe<ServerConnectionResultEvent>(OnConnectionResult);
            _accountReadySubscription =
                _eventBus.Subscribe<AccountReadyEvent>(
                    OnAccountReady);
            _backSubscription =
                _eventBus.Subscribe<CharacterSelectionBackRequestedEvent>(
                    _ => _authenticationFlow.ReturnToAuthenticationEntry());
            _entryRequiredSubscription =
                _eventBus.Subscribe<AuthenticationEntryRequiredEvent>(
                    state =>
                    {
                        if (state.IsVisible)
                            SetActivePanel(null);
                    });
            _listSubscription = _eventBus.Subscribe<CharacterListReceivedEvent>(OnCharacterListReceived);
            _listFailedSubscription =
                _eventBus.Subscribe<CharacterListFailedEvent>(
                    e => Debug.LogError(
                        $"[Character] List failed: {e.Message}"));
            _creationSubscription = _eventBus.Subscribe<CharacterCreationResultEvent>(OnCharacterCreationResult);
            _creationRequestedSubscription =
                _eventBus.Subscribe<CharacterCreationRequestedEvent>(
                    request =>
                    {
                        _ = _characterService.RequestCreateCharacter(
                            request.Draft);
                    });
            _creationSlotSubscription =
                _eventBus.Subscribe<CharacterCreationSlotRequestedEvent>(
                    requestedSlot =>
                    {
                        SetActivePanel(_panels.CharacterCreationPanel);
                        _ = _characterService.RequestCreationCatalog(
                            _gameplaySettings.ServerId);
                    });
            _creationCancelledSubscription =
                _eventBus.Subscribe<CharacterCreationCancelledEvent>(
                    cancelled =>
                    {
                        SetActivePanel(_panels.CharacterSelectPanel);
                        _ = _characterService.RequestListCharacters(
                            _gameplaySettings.ServerId);
                    });
            _nameCheckSubscription =
                _eventBus.Subscribe<CharacterNameCheckRequestedEvent>(
                    request =>
                    {
                        _ = _characterService.CheckName(
                            request.ServerId,
                            request.CharacterName);
                    });
            _selectionSubscription = _eventBus.Subscribe<CharacterSelectedEvent>(OnCharacterSelected);
            _gameplaySessionReadySubscription =
                _eventBus.Subscribe<GameplaySessionReadyEvent>(
                    e => _characterSelection.EnterWorld(
                        e.GameplaySessionId));
            _enterWorldFailedSubscription =
                _eventBus.Subscribe<EnterWorldFailedEvent>(
                    e => Debug.LogError(
                        $"[Character] Enter world failed: {e.Message}"));
            _selectionFailedSubscription =
                _eventBus.Subscribe<CharacterSelectionFailedEvent>(
                    e => Debug.LogError($"[Character] Selection failed: {e.Message}"));

            SetActivePanel(null); // Ẩn hết ban đầu, chờ kết quả kết nối
        }

        private void OnConnectionResult(ServerConnectionResultEvent e)
        {
            if (e.Result == ConnectionOutcome.Success &&
                _authenticationSettings.DevelopmentBypassEnabled)
                RequestCharacterSelectData();
        }

        private void OnAccountReady(AccountReadyEvent e)
        {
            RequestCharacterSelectData();
        }

        private void RequestCharacterSelectData()
        {
            // Character Select cần catalog để hiển thị tên class/visual theo
            // definition ID. Hai request độc lập để sau này catalog có thể
            // được cache/version hóa mà không ghép cứng vào character list.
            _ = _characterService.RequestCreationCatalog(
                _gameplaySettings.ServerId);
            _ = _characterService.RequestListCharacters(
                _gameplaySettings.ServerId);
        }

        private void OnCharacterListReceived(CharacterListReceivedEvent e)
        {
            SetActivePanel(_panels.CharacterSelectPanel);
        }

        private void OnCharacterCreationResult(CharacterCreationResultEvent e)
        {
            if (e.Success && e.Character != null)
                _characterSelection.SelectCharacter(e.Character);
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
            _listFailedSubscription?.Dispose();
            _creationSubscription?.Dispose();
            _creationRequestedSubscription?.Dispose();
            _selectionSubscription?.Dispose();
            _gameplaySessionReadySubscription?.Dispose();
            _enterWorldFailedSubscription?.Dispose();
            _selectionFailedSubscription?.Dispose();
            _accountReadySubscription?.Dispose();
            _backSubscription?.Dispose();
            _entryRequiredSubscription?.Dispose();
            _creationSlotSubscription?.Dispose();
            _creationCancelledSubscription?.Dispose();
            _nameCheckSubscription?.Dispose();
        }
    }
}
