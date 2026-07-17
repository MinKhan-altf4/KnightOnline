using System;
using System.Collections.Generic;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace KnightOnline.Client.UI
{
    /// <summary>Renders one selectable button for each character returned by the server.</summary>
    public sealed class CharacterSelectView : MonoBehaviour
    {
        [SerializeField] private Transform _characterListRoot;
        [SerializeField] private Button _characterButtonTemplate;
        [SerializeField] private TextMeshProUGUI _emptyStateText;

        private readonly List<Button> _createdButtons = new();
        private IEventBus _eventBus;
        private CharacterSelectionService _selectionService;
        private IDisposable _listSubscription;

        [Inject]
        public void Construct(IEventBus eventBus, CharacterSelectionService selectionService)
        {
            _eventBus = eventBus;
            _selectionService = selectionService;
        }

        private void Start()
        {
            if (_characterButtonTemplate != null)
                _characterButtonTemplate.gameObject.SetActive(false);
            _listSubscription = _eventBus.Subscribe<CharacterListReceivedEvent>(RenderCharacters);
        }

        private void RenderCharacters(CharacterListReceivedEvent e)
        {
            ClearButtons();
            var hasCharacters = e.Characters != null && e.Characters.Count > 0;
            if (_emptyStateText != null) _emptyStateText.gameObject.SetActive(!hasCharacters);
            if (!hasCharacters || _characterButtonTemplate == null || _characterListRoot == null) return;

            foreach (var character in e.Characters)
            {
                var characterCopy = character;
                var button = Instantiate(_characterButtonTemplate, _characterListRoot);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = characterCopy.CharacterName;
                button.onClick.AddListener(() => _selectionService.SelectCharacter(characterCopy));
                _createdButtons.Add(button);
            }
        }

        private void ClearButtons()
        {
            foreach (var button in _createdButtons)
                if (button != null) Destroy(button.gameObject);
            _createdButtons.Clear();
        }

        private void OnDestroy()
        {
            _listSubscription?.Dispose();
        }
    }
}
