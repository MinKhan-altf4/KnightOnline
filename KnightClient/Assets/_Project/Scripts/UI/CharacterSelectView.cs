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
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _registerGuestButton;
        [SerializeField] private GuestRegistrationPanel _registrationPanel;

        private readonly List<Button> _createdButtons = new();
        private IEventBus _eventBus;
        private CharacterSelectionService _selectionService;
        private IDisposable _listSubscription;
        private IDisposable _accountSubscription;

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
            _accountSubscription = _eventBus.Subscribe<AccountReadyEvent>(
                account =>
                {
                    if (_registerGuestButton != null)
                        _registerGuestButton.gameObject.SetActive(
                            account.IsGuest);
                });
            _backButton?.onClick.AddListener(OnBackClicked);
            _registerGuestButton?.onClick.AddListener(OnRegisterGuestClicked);
            _registrationPanel?.Hide();
        }

        private void OnRegisterGuestClicked() => _registrationPanel?.Show();

        private void OnBackClicked()
        {
            _eventBus.Publish(new CharacterSelectionBackRequestedEvent());
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
            _backButton?.onClick.RemoveListener(OnBackClicked);
            _registerGuestButton?.onClick.RemoveListener(OnRegisterGuestClicked);
            _listSubscription?.Dispose();
            _accountSubscription?.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_characterListRoot == null ||
                _characterButtonTemplate == null ||
                _emptyStateText == null ||
                _backButton == null)
            {
                Debug.LogWarning(
                    "[CharacterSelectView] Thiếu serialized reference.",
                    this);
            }
        }
#endif
    }
}
