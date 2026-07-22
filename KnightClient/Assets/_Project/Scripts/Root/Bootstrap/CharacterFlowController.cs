using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Gameplay.Services;
using KnightOnline.Client.Shared.Packets;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>
    /// Điều phối việc hiện/ẩn panel dựa theo trạng thái luồng nhân vật.
    /// Dùng Panel-based switching (SetActive) thay vì SceneManager.LoadScene,
    /// vì component ở scene mới sẽ KHÔNG được VContainer inject - Configure()
    /// chỉ quét component tồn tại tại thời điểm build container.
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

        private IDisposable _connectionSubscription;
        private IDisposable _listSubscription;
        private IDisposable _creationSubscription;
        private IDisposable _selectionSubscription;

        public CharacterFlowController(IEventBus eventBus, CharacterService characterService,
            GameSession gameSession, PanelRefs panels)
        {
            _eventBus = eventBus;
            _characterService = characterService;
            _gameSession = gameSession;
            _panels = panels;
        }

        public void Start()
        {
            _connectionSubscription = _eventBus.Subscribe<ServerConnectionResultEvent>(OnConnectionResult);
            _listSubscription = _eventBus.Subscribe<CharacterListReceivedEvent>(OnCharacterListReceived);
            _creationSubscription = _eventBus.Subscribe<CharacterCreationResultEvent>(OnCharacterCreationResult);
            _selectionSubscription = _eventBus.Subscribe<CharacterSelectedEvent>(OnCharacterSelected);

            SetActivePanel(null); // Ẩn hết ban đầu, chờ kết quả kết nối
        }

        private void OnConnectionResult(ServerConnectionResultEvent e)
        {
            if (e.Result == ConnectResult.Success) _ = _characterService.RequestListCharacters();
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
        }
    }
}
