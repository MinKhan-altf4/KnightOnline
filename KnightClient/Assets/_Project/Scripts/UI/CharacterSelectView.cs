using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Renders the three server-authoritative character slots. Empty slots
    /// publish a creation intent; occupied slots can be selected.
    /// </summary>
    public sealed class CharacterSelectView : MonoBehaviour
    {
        private const int SlotCount = 3;

        [SerializeField] private Transform _characterListRoot;
        [SerializeField] private Button _characterButtonTemplate;
        [SerializeField] private TextMeshProUGUI _emptyStateText;
        [SerializeField] private Button _backButton;

        private readonly List<Button> _createdButtons = new();
        private IEventBus _eventBus;
        private CharacterSelectionService _selectionService;
        private IDisposable _listSubscription;

        [Inject]
        public void Construct(
            IEventBus eventBus,
            CharacterSelectionService selectionService) =>
            Initialize(eventBus, selectionService);

        public void Initialize(
            IEventBus eventBus,
            CharacterSelectionService selectionService)
        {
            _eventBus = eventBus ??
                throw new ArgumentNullException(nameof(eventBus));
            _selectionService = selectionService ??
                throw new ArgumentNullException(nameof(selectionService));
        }

        private void Start()
        {
            if (_eventBus == null || _selectionService == null)
            {
                Debug.LogError(
                    "[CharacterSelectView] Dependencies were not injected.",
                    this);
                enabled = false;
                return;
            }

            if (_characterButtonTemplate != null)
                _characterButtonTemplate.gameObject.SetActive(false);
            _listSubscription =
                _eventBus.Subscribe<CharacterListReceivedEvent>(
                    RenderCharacters);
            _backButton?.onClick.AddListener(OnBackClicked);
        }

        private void OnBackClicked() =>
            _eventBus.Publish(new CharacterSelectionBackRequestedEvent());

        private void RenderCharacters(CharacterListReceivedEvent message)
        {
            ClearButtons();
            if (_characterButtonTemplate == null ||
                _characterListRoot == null)
            {
                return;
            }

            IReadOnlyList<CharacterData> characters =
                message.Characters ?? Array.Empty<CharacterData>();
            Dictionary<int, CharacterData> bySlot = characters
                .Where(character =>
                    character != null &&
                    character.SlotIndex is >= 1 and <= SlotCount)
                .GroupBy(character => character.SlotIndex)
                .ToDictionary(group => group.Key, group => group.First());

            for (var slotIndex = 1; slotIndex <= SlotCount; slotIndex++)
            {
                int capturedSlot = slotIndex;
                var button = Instantiate(
                    _characterButtonTemplate,
                    _characterListRoot);
                button.gameObject.SetActive(true);
                var label =
                    button.GetComponentInChildren<TextMeshProUGUI>(true);

                if (bySlot.TryGetValue(slotIndex, out CharacterData character))
                {
                    CharacterData capturedCharacter = character;
                    if (label != null)
                    {
                        string className =
                            string.IsNullOrWhiteSpace(
                                character.ClassDefinitionId)
                                ? "Unknown"
                                : character.ClassDefinitionId;
                        label.text =
                            $"{character.CharacterName}\n" +
                            $"Lv.{character.Level} · {className}";
                    }
                    button.onClick.AddListener(
                        () => _selectionService.SelectCharacter(
                            capturedCharacter));
                }
                else
                {
                    if (label != null)
                        label.text = $"+ Tạo nhân vật\nÔ {slotIndex}";
                    button.onClick.AddListener(
                        () => _eventBus.Publish(
                            new CharacterCreationSlotRequestedEvent(
                                capturedSlot)));
                }

                _createdButtons.Add(button);
            }

            if (_emptyStateText != null)
            {
                _emptyStateText.gameObject.SetActive(characters.Count == 0);
                _emptyStateText.text =
                    "Bạn chưa có nhân vật. Hãy chọn một ô để tạo.";
            }
        }

        private void ClearButtons()
        {
            foreach (Button button in _createdButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }
            _createdButtons.Clear();
        }

        private void OnDestroy()
        {
            _backButton?.onClick.RemoveListener(OnBackClicked);
            _listSubscription?.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_characterListRoot == null ||
                _characterButtonTemplate == null ||
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
