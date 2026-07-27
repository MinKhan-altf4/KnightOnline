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
        [Header("Placeholder presentation")]
        [SerializeField] private Color _occupiedSlotColor =
            new Color(0.23f, 0.42f, 0.68f, 1f);
        [SerializeField] private Color _emptySlotColor =
            new Color(0.18f, 0.22f, 0.30f, 1f);
        [SerializeField] private float _slotHeight = 190f;
        [SerializeField] private float _slotWidth = 290f;
        [SerializeField] private KnightUiTheme _theme;

        private readonly List<Button> _createdButtons = new();
        private readonly Dictionary<string, string> _classNames = new();
        private IEventBus _eventBus;
        private CharacterSelectionService _selectionService;
        private IDisposable _listSubscription;
        private IDisposable _catalogSubscription;
        private IDisposable _selectionSubscription;
        private IDisposable _selectionFailedSubscription;
        private IReadOnlyList<CharacterData> _lastCharacters =
            Array.Empty<CharacterData>();
        private bool _selectionPending;
        private bool _isBound;

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
            Bind();
        }

        private void Start() => Bind();

        private void Bind()
        {
            if (_isBound)
                return;

            if (_eventBus == null || _selectionService == null)
            {
                Debug.LogError(
                    "[CharacterSelectView] Dependencies were not injected.",
                    this);
                enabled = false;
                return;
            }

            _isBound = true;
            if (_characterButtonTemplate != null)
                _characterButtonTemplate.gameObject.SetActive(false);
            _theme ??= KnightUiTheme.LoadDefault();
            ApplyPanelPresentation();
            ConfigureSlotLayout();
            _listSubscription =
                _eventBus.Subscribe<CharacterListReceivedEvent>(
                    RenderCharacters);
            _catalogSubscription =
                _eventBus.Subscribe<CharacterCreationCatalogReceivedEvent>(
                    OnCatalogReceived);
            _selectionSubscription =
                _eventBus.Subscribe<CharacterSelectedEvent>(
                    _ => SetSelectionPending(false));
            _selectionFailedSubscription =
                _eventBus.Subscribe<CharacterSelectionFailedEvent>(
                    message =>
                    {
                        SetSelectionPending(false);
                        SetMessage(message.Message);
                    });
            _backButton?.onClick.AddListener(OnBackClicked);
        }

        private void ConfigureSlotLayout()
        {
            if (_characterListRoot == null)
                return;

            // CharacterListRoot trong scene cũ có VerticalLayoutGroup. Unity
            // không cho hai LayoutGroup cùng một GameObject, vì vậy tắt layout
            // cũ và đặt ba slot bằng anchored position tương đối. Cách này giữ
            // scene tương thích mà không cần sửa/xóa component lúc runtime.
            LayoutGroup[] layouts =
                _characterListRoot.GetComponents<LayoutGroup>();
            foreach (LayoutGroup layout in layouts)
                layout.enabled = false;
        }

        private void OnBackClicked()
        {
            if (!_selectionPending)
                _eventBus.Publish(
                    new CharacterSelectionBackRequestedEvent());
        }

        private void OnCatalogReceived(
            CharacterCreationCatalogReceivedEvent message)
        {
            _classNames.Clear();
            if (message.Catalog?.Classes != null)
            {
                foreach (CharacterClassDefinitionData definition in
                         message.Catalog.Classes)
                {
                    if (!string.IsNullOrWhiteSpace(definition.DefinitionId))
                    {
                        _classNames[definition.DefinitionId] =
                            definition.DisplayName;
                    }
                }
            }

            RenderCharacters(
                new CharacterListReceivedEvent(_lastCharacters));
        }

        private void RenderCharacters(CharacterListReceivedEvent message)
        {
            _lastCharacters =
                message.Characters ?? Array.Empty<CharacterData>();
            ClearButtons();
            if (_characterButtonTemplate == null ||
                _characterListRoot == null)
            {
                return;
            }

            IReadOnlyList<CharacterData> characters = _lastCharacters;
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
                var image = button.targetGraphic as Image ??
                    button.GetComponent<Image>();
                var layout = button.GetComponent<LayoutElement>() ??
                    button.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = _slotHeight;
                layout.minHeight = _slotHeight;
                layout.minWidth = _slotWidth;
                layout.preferredWidth = _slotWidth;
                _theme?.ApplyButton(button, _slotHeight);
                PlaceSlot(button, slotIndex);

                if (bySlot.TryGetValue(slotIndex, out CharacterData character))
                {
                    CharacterData capturedCharacter = character;
                    if (label != null)
                    {
                        string className =
                            ResolveClassName(
                                character.ClassDefinitionId);
                        label.text =
                            $"Ô {slotIndex}  •  " +
                            $"{character.CharacterName}\n" +
                            $"Cấp {character.Level}  •  {className}  •  " +
                            ResolveBodyName(character.BodyTypeDefinitionId);
                    }
                    if (image != null)
                    {
                        image.color = Color.Lerp(
                            _occupiedSlotColor,
                            ColorForDefinition(
                                character.ClassDefinitionId),
                            0.35f);
                    }
                    button.onClick.AddListener(
                        () => SelectCharacter(capturedCharacter));
                }
                else
                {
                    if (label != null)
                        label.text =
                            $"Ô {slotIndex}\n+ Tạo nhân vật";
                    if (image != null)
                        image.color = _emptySlotColor;
                    button.onClick.AddListener(
                        () => OpenCreation(capturedSlot));
                }

                button.interactable = !_selectionPending;
                _createdButtons.Add(button);
            }

            if (_emptyStateText != null)
            {
                _emptyStateText.gameObject.SetActive(characters.Count == 0);
                _emptyStateText.text =
                    "Bạn chưa có nhân vật.\nHãy chọn một trong ba ô để tạo.";
            }
        }

        private void PlaceSlot(Button button, int slotIndex)
        {
            if (button?.transform is not RectTransform rect)
                return;

            float spacing = 24f;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(
                (slotIndex - 2) * (_slotWidth + spacing),
                0f);
            rect.sizeDelta = new Vector2(_slotWidth, _slotHeight);
            rect.localScale = Vector3.one;
        }

        private void SelectCharacter(CharacterData character)
        {
            if (_selectionPending)
                return;

            SetSelectionPending(true);
            SetMessage($"Đang vào game bằng {character.CharacterName}...");
            _selectionService.SelectCharacter(character);
        }

        private void OpenCreation(int slotIndex)
        {
            if (_selectionPending)
                return;

            _eventBus.Publish(
                new CharacterCreationSlotRequestedEvent(slotIndex));
        }

        private string ResolveClassName(string definitionId) =>
            !string.IsNullOrWhiteSpace(definitionId) &&
            _classNames.TryGetValue(definitionId, out string displayName)
                ? displayName
                : string.IsNullOrWhiteSpace(definitionId)
                    ? "Chưa xác định"
                    : definitionId;

        private static string ResolveBodyName(string definitionId) =>
            definitionId switch
            {
                "male" => "Nam",
                "female" => "Nữ",
                _ => definitionId ?? string.Empty,
            };

        private static Color ColorForDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return Color.gray;

            uint hash = 2166136261;
            foreach (char value in definitionId)
            {
                hash ^= value;
                hash *= 16777619;
            }

            float hue = (hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.85f);
        }

        private void SetSelectionPending(bool pending)
        {
            _selectionPending = pending;
            foreach (Button button in _createdButtons)
            {
                if (button != null)
                    button.interactable = !pending;
            }
            if (_backButton != null)
                _backButton.interactable = !pending;
        }

        private void SetMessage(string message)
        {
            if (_emptyStateText == null)
                return;
            _emptyStateText.gameObject.SetActive(true);
            _emptyStateText.text = message;
        }

        private void ApplyPanelPresentation()
        {
            _theme?.ApplyPanel(GetComponent<Image>(), true);
            _theme?.ApplyButton(_backButton, 54f);
            _theme?.ApplyBodyText(_emptyStateText, 22f);

            if (_characterListRoot is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 0f);
                rect.sizeDelta = new Vector2(980f, 230f);
            }

            if (_emptyStateText?.transform is RectTransform messageRect)
            {
                messageRect.anchorMin = new Vector2(0.5f, 0.5f);
                messageRect.anchorMax = new Vector2(0.5f, 0.5f);
                messageRect.pivot = new Vector2(0.5f, 0.5f);
                messageRect.anchoredPosition = new Vector2(0f, -165f);
                messageRect.sizeDelta = new Vector2(760f, 60f);
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
            _catalogSubscription?.Dispose();
            _selectionSubscription?.Dispose();
            _selectionFailedSubscription?.Dispose();
            _isBound = false;
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
