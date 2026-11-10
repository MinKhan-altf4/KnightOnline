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
                _theme?.ApplyCleanButton(
                    button,
                    _slotHeight,
                    bySlot.ContainsKey(slotIndex)
                        ? new Color(0.15f, 0.27f, 0.40f, 1f)
                        : new Color(0.20f, 0.28f, 0.32f, 1f));
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
                        StyleSlotLabel(label);
                    }
                    button.onClick.AddListener(
                        () => SelectCharacter(capturedCharacter));
                }
                else
                {
                    if (label != null)
                    {
                        label.text =
                            $"Ô {slotIndex}\n+ Tạo nhân vật";
                        StyleSlotLabel(label);
                    }
                    button.onClick.AddListener(
                        () => OpenCreation(capturedSlot));
                }

                button.interactable = !_selectionPending;
                _createdButtons.Add(button);
            }

            if (_emptyStateText != null)
            {
                _emptyStateText.gameObject.SetActive(true);
                _emptyStateText.text = characters.Count == 0
                    ? "Bạn chưa có nhân vật. Chọn một ô để tạo nhân vật mới."
                    : "Chọn nhân vật để vào game, hoặc chọn ô trống để tạo thêm.";
            }
        }

        private void StyleSlotLabel(TextMeshProUGUI label)
        {
            label.color = new Color(1f, 0.94f, 0.72f, 1f);
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 24f;
            label.fontSizeMin = 17f;
            label.fontSizeMax = 24f;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
            if (label.rectTransform != null)
            {
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(14f, 12f);
                label.rectTransform.offsetMax = new Vector2(-14f, -12f);
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
            Transform panelTransform = _characterListRoot != null &&
                _characterListRoot.parent != null
                    ? _characterListRoot.parent
                    : transform;
            Image panelImage = panelTransform.GetComponent<Image>();
            _theme?.ApplyPanel(panelImage, true);
            if (panelImage != null)
                panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.34f);
            CreateBackdrop(
                panelTransform,
                "CharacterSelectBackdrop",
                new Vector2(1080f, 550f),
                new Color(0.055f, 0.075f, 0.12f, 0.88f));
            CreateHeading(
                panelTransform,
                "CharacterSelectHeading",
                "CHỌN NHÂN VẬT",
                38f,
                265f);
            _theme?.ApplyCleanButton(_backButton, 54f);
            _theme?.ApplyBodyText(_emptyStateText, 22f);

            if (_characterListRoot is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 0f);
                rect.sizeDelta = new Vector2(980f, 230f);
            }

            if (_backButton?.transform is RectTransform backRect)
            {
                backRect.anchorMin = Vector2.zero;
                backRect.anchorMax = Vector2.zero;
                backRect.pivot = Vector2.zero;
                backRect.anchoredPosition = new Vector2(58f, 42f);
                backRect.sizeDelta = new Vector2(220f, 58f);
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

        private static GameObject CreateBackdrop(
            Transform parent,
            string objectName,
            Vector2 size,
            Color color)
        {
            var backdrop = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image));
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.SetAsFirstSibling();
            RectTransform rect = (RectTransform)backdrop.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image image = backdrop.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return backdrop;
        }

        private TextMeshProUGUI CreateHeading(
            Transform parent,
            string objectName,
            string text,
            float fontSize,
            float y)
        {
            var headingObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            headingObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)headingObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(900f, 58f);
            TextMeshProUGUI heading =
                headingObject.GetComponent<TextMeshProUGUI>();
            heading.font = _emptyStateText != null
                ? _emptyStateText.font
                : TMP_Settings.defaultFontAsset;
            heading.text = text;
            heading.fontSize = fontSize;
            heading.color = new Color(1f, 0.96f, 0.84f, 1f);
            heading.fontStyle = FontStyles.Bold;
            heading.alignment = TextAlignmentOptions.Center;
            heading.raycastTarget = false;
            return heading;
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
