using System;
using System.Collections.Generic;
using System.Linq;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace KnightOnline.Client.UI
{
    /// <summary>
    /// Catalog-driven creation form. Asset preview assembly is intentionally
    /// kept behind the catalog asset addresses and can be added separately.
    /// </summary>
    public sealed class CharacterCreationView : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown _classDropdown;
        [SerializeField] private TMP_Dropdown _bodyTypeDropdown;
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private Button _checkNameButton;
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private TextMeshProUGUI _resultText;
        [Header("Catalog-driven appearance placeholder")]
        [SerializeField] private RectTransform _appearanceOptionsRoot;
        [SerializeField] private TMP_Dropdown _appearanceDropdownTemplate;
        [SerializeField] private Vector2 _runtimeAppearancePanelPosition =
            new Vector2(230f, 10f);
        [SerializeField] private Vector2 _runtimeAppearancePanelSize =
            new Vector2(280f, 230f);
        [SerializeField] private KnightUiTheme _theme;

        private IEventBus _eventBus;
        private CharacterCreationCatalogData _catalog;
        private int _slotIndex;
        private readonly Dictionary<string, TMP_Dropdown>
            _appearanceDropdowns = new();
        private readonly Dictionary<string,
            IReadOnlyList<AppearanceDefinitionData>>
            _appearanceChoices = new();
        private readonly List<GameObject> _runtimeAppearanceObjects = new();
        private IDisposable _catalogSubscription;
        private IDisposable _slotSubscription;
        private IDisposable _creationSubscription;
        private IDisposable _nameSubscription;
        private bool _nameCheckPending;
        private bool _isBound;

        [Inject]
        public void Construct(IEventBus eventBus) => Initialize(eventBus);

        public void Initialize(IEventBus eventBus)
        {
            _eventBus = eventBus ??
                throw new ArgumentNullException(nameof(eventBus));
            Bind();
        }

        private void Start() => Bind();

        private void Bind()
        {
            if (_isBound)
                return;

            if (_eventBus == null)
            {
                Debug.LogError(
                    "[CharacterCreationView] EventBus was not injected.",
                    this);
                enabled = false;
                return;
            }

            _isBound = true;
            _theme ??= KnightUiTheme.LoadDefault();
            ApplyPanelPresentation();
            _catalogSubscription =
                _eventBus.Subscribe<CharacterCreationCatalogReceivedEvent>(
                    OnCatalogReceived);
            _slotSubscription =
                _eventBus.Subscribe<CharacterCreationSlotRequestedEvent>(
                    message => _slotIndex = message.SlotIndex);
            _creationSubscription =
                _eventBus.Subscribe<CharacterCreationResultEvent>(
                    OnCharacterCreationResult);
            _nameSubscription =
                _eventBus.Subscribe<CharacterNameAvailabilityReceivedEvent>(
                    OnNameAvailabilityReceived);

            _classDropdown?.onValueChanged.AddListener(OnClassChanged);
            _bodyTypeDropdown?.onValueChanged.AddListener(OnBodyTypeChanged);
            _checkNameButton?.onClick.AddListener(OnCheckNameClicked);
            _createButton?.onClick.AddListener(OnCreateCharacterClicked);
            _backButton?.onClick.AddListener(OnBackClicked);
            SetInteractable(false);
        }

        private void OnCatalogReceived(
            CharacterCreationCatalogReceivedEvent message)
        {
            _catalog = message.Catalog;
            if (_catalog == null ||
                _catalog.Classes == null ||
                _catalog.Classes.Count == 0 ||
                _catalog.BodyTypes == null ||
                _catalog.BodyTypes.Count == 0 ||
                _catalog.AppearanceOptions == null)
            {
                SetResult("Không tải được dữ liệu tạo nhân vật.");
                return;
            }

            SetDropdownOptions(
                _classDropdown,
                _catalog.Classes.Select(value => value.DisplayName));
            RefreshBodyTypes();
            RefreshAppearanceOptions();
            SetInteractable(true);
            SetResult($"Đang tạo nhân vật ở ô {_slotIndex}.");
        }

        private void OnClassChanged(int _)
        {
            RefreshBodyTypes();
            RefreshAppearanceOptions();
        }

        private void OnBodyTypeChanged(int _) =>
            RefreshAppearanceOptions();

        private void RefreshBodyTypes()
        {
            if (_catalog == null ||
                _catalog.Classes == null ||
                _catalog.Classes.Count == 0)
            {
                return;
            }

            CharacterClassDefinitionData selectedClass =
                _catalog.Classes[
                    Mathf.Clamp(
                        _classDropdown?.value ?? 0,
                        0,
                        _catalog.Classes.Count - 1)];
            IReadOnlyList<BodyTypeDefinitionData> allowed =
                _catalog.BodyTypes
                    .Where(body =>
                        selectedClass.AllowedBodyTypeIds.Contains(
                            body.DefinitionId))
                    .ToArray();
            SetDropdownOptions(
                _bodyTypeDropdown,
                allowed.Select(value => value.DisplayName));
        }

        private void RefreshAppearanceOptions()
        {
            ClearAppearanceOptions();
            if (!TryGetSelectedClassAndBody(
                    out CharacterClassDefinitionData selectedClass,
                    out BodyTypeDefinitionData selectedBody))
            {
                return;
            }

            EnsureAppearanceRoot();
            TMP_Dropdown template =
                _appearanceDropdownTemplate != null
                    ? _appearanceDropdownTemplate
                    : _bodyTypeDropdown;
            if (_appearanceOptionsRoot == null || template == null)
                return;

            IEnumerable<IGrouping<string, AppearanceDefinitionData>> groups =
                _catalog.AppearanceOptions
                    .Where(value =>
                        value.IsStarterOption &&
                        IsAllowed(
                            value.AllowedBodyTypeIds,
                            selectedBody.DefinitionId) &&
                        IsAllowed(
                            value.AllowedClassDefinitionIds,
                            selectedClass.DefinitionId))
                    .GroupBy(value => value.SlotDefinitionId)
                    .OrderBy(group => AppearanceSlotOrder(group.Key));

            foreach (IGrouping<string, AppearanceDefinitionData> group in
                     groups)
            {
                AppearanceDefinitionData[] choices = group.ToArray();
                TMP_Dropdown dropdown = Instantiate(
                    template,
                    _appearanceOptionsRoot);
                dropdown.name =
                    $"Appearance_{group.Key}_Dropdown";
                dropdown.gameObject.SetActive(true);
                SetDropdownOptions(
                    dropdown,
                    choices.Select(value =>
                        $"{DisplaySlotName(group.Key)}: " +
                        value.DisplayName));
                dropdown.onValueChanged.AddListener(
                    _ => UpdateAppearanceSummary());

                var layout = dropdown.GetComponent<LayoutElement>() ??
                    dropdown.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 42f;
                layout.minHeight = 42f;
                layout.preferredWidth = _runtimeAppearancePanelSize.x;
                _theme?.ApplyDropdown(dropdown, 42f);

                _appearanceDropdowns[group.Key] = dropdown;
                _appearanceChoices[group.Key] = choices;
                _runtimeAppearanceObjects.Add(dropdown.gameObject);
            }

            UpdateAppearanceSummary();
        }

        private bool TryGetSelectedClassAndBody(
            out CharacterClassDefinitionData selectedClass,
            out BodyTypeDefinitionData selectedBody)
        {
            selectedClass = null;
            selectedBody = null;
            if (_catalog?.Classes == null ||
                _catalog.Classes.Count == 0)
            {
                return false;
            }

            selectedClass = _catalog.Classes[
                Mathf.Clamp(
                    _classDropdown?.value ?? 0,
                    0,
                    _catalog.Classes.Count - 1)];
            BodyTypeDefinitionData[] allowedBodies =
                GetAllowedBodies(selectedClass);
            if (allowedBodies.Length == 0)
                return false;

            selectedBody = allowedBodies[
                Mathf.Clamp(
                    _bodyTypeDropdown?.value ?? 0,
                    0,
                    allowedBodies.Length - 1)];
            return true;
        }

        private BodyTypeDefinitionData[] GetAllowedBodies(
            CharacterClassDefinitionData selectedClass) =>
            _catalog.BodyTypes
                .Where(body =>
                    selectedClass.AllowedBodyTypeIds.Contains(
                        body.DefinitionId))
                .ToArray();

        private void EnsureAppearanceRoot()
        {
            if (_appearanceOptionsRoot != null)
                return;

            var root = new GameObject(
                "RuntimeAppearanceOptions",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            root.layer = gameObject.layer;
            _appearanceOptionsRoot =
                root.GetComponent<RectTransform>();
            Transform uiParent = _bodyTypeDropdown != null
                ? _bodyTypeDropdown.transform.parent
                : transform;
            _appearanceOptionsRoot.SetParent(uiParent, false);
            _appearanceOptionsRoot.anchorMin =
                new Vector2(0.5f, 0.5f);
            _appearanceOptionsRoot.anchorMax =
                new Vector2(0.5f, 0.5f);
            _appearanceOptionsRoot.pivot =
                new Vector2(0.5f, 0.5f);
            _appearanceOptionsRoot.anchoredPosition =
                _runtimeAppearancePanelPosition;
            _appearanceOptionsRoot.sizeDelta =
                _runtimeAppearancePanelSize;

            VerticalLayoutGroup layout =
                root.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private void OnCheckNameClicked()
        {
            if (_catalog == null)
            {
                SetResult("Đang tải dữ liệu máy chủ. Vui lòng thử lại sau.");
                return;
            }

            if (_nameCheckPending)
                return;

            string name = _nameInput?.text?.Trim() ?? string.Empty;
            if (name.Length < 3 || name.Length > 20)
            {
                SetResult("Tên nhân vật phải dài từ 3 đến 20 ký tự.");
                _nameInput?.ActivateInputField();
                return;
            }

            _nameCheckPending = true;
            if (_checkNameButton != null)
                _checkNameButton.interactable = false;
            SetResult($"Đang kiểm tra tên “{name}”…");
            _eventBus.Publish(
                new CharacterNameCheckRequestedEvent(
                    _catalog.ServerId,
                    name));
        }

        private void OnNameAvailabilityReceived(
            CharacterNameAvailabilityReceivedEvent message)
        {
            if (!_nameCheckPending)
                return;

            _nameCheckPending = false;
            if (_checkNameButton != null && _catalog != null)
                _checkNameButton.interactable = true;

            string serverMessage = message.Message ?? string.Empty;
            string normalizedMessage = serverMessage.ToLowerInvariant();
            string displayMessage = message.IsAvailable
                ? "Tên này có thể sử dụng."
                : normalizedMessage.Contains("already exists")
                    ? "Tên này đã được sử dụng. Hãy chọn tên khác."
                    : normalizedMessage.Contains("3 to 20")
                        ? "Tên nhân vật phải dài từ 3 đến 20 ký tự."
                        : normalizedMessage.Contains("only contain")
                            ? "Tên chỉ được gồm chữ cái, chữ số và dấu cách đơn."
                            : normalizedMessage.Contains("selected server")
                                ? "Máy chủ đã chọn hiện không khả dụng."
                                : string.IsNullOrWhiteSpace(serverMessage)
                                    ? "Không nhận được kết quả kiểm tra tên."
                                    : serverMessage;
            SetResult(displayMessage);
        }

        public void OnCreateCharacterClicked()
        {
            if (_catalog == null || _slotIndex is < 1 or > 3)
            {
                SetResult("Dữ liệu tạo nhân vật hoặc ô nhân vật chưa hợp lệ.");
                return;
            }

            if (!TryGetSelectedClassAndBody(
                    out CharacterClassDefinitionData selectedClass,
                    out BodyTypeDefinitionData selectedBody))
            {
                SetResult("Class này chưa có body type hợp lệ.");
                return;
            }

            AppearanceSelectionData[] starterAppearance =
                GetSelectedAppearance();

            var draft = new CharacterCreationDraftData
            {
                RequestId = Guid.NewGuid(),
                ServerId = _catalog.ServerId,
                SlotIndex = _slotIndex,
                CharacterName = _nameInput?.text?.Trim() ?? string.Empty,
                ClassDefinitionId = selectedClass.DefinitionId,
                BodyTypeDefinitionId = selectedBody.DefinitionId,
                AppearanceSelections = starterAppearance,
                CatalogVersion = _catalog.CatalogVersion,
            };
            SetInteractable(false);
            SetResult("Đang tạo nhân vật...");
            _eventBus.Publish(new CharacterCreationRequestedEvent(draft));
        }

        private AppearanceSelectionData[] GetSelectedAppearance()
        {
            if (_appearanceDropdowns.Count > 0)
            {
                return _appearanceDropdowns
                    .OrderBy(pair => AppearanceSlotOrder(pair.Key))
                    .Select(pair =>
                    {
                        IReadOnlyList<AppearanceDefinitionData> choices =
                            _appearanceChoices[pair.Key];
                        int index = Mathf.Clamp(
                            pair.Value.value,
                            0,
                            choices.Count - 1);
                        return new AppearanceSelectionData
                        {
                            SlotDefinitionId = pair.Key,
                            OptionDefinitionId =
                                choices[index].DefinitionId,
                        };
                    }).ToArray();
            }

            // Fallback giữ luồng hoạt động khi scene chưa có vùng appearance.
            if (!TryGetSelectedClassAndBody(
                    out CharacterClassDefinitionData selectedClass,
                    out BodyTypeDefinitionData selectedBody))
            {
                return Array.Empty<AppearanceSelectionData>();
            }

            return _catalog.AppearanceOptions
                .Where(value =>
                    value.IsStarterOption &&
                    IsAllowed(
                        value.AllowedBodyTypeIds,
                        selectedBody.DefinitionId) &&
                    IsAllowed(
                        value.AllowedClassDefinitionIds,
                        selectedClass.DefinitionId))
                .GroupBy(value => value.SlotDefinitionId)
                .Select(group => group.First())
                .OrderBy(value =>
                    AppearanceSlotOrder(value.SlotDefinitionId))
                .Select(value => new AppearanceSelectionData
                {
                    SlotDefinitionId = value.SlotDefinitionId,
                    OptionDefinitionId = value.DefinitionId,
                }).ToArray();
        }

        private void UpdateAppearanceSummary()
        {
            if (!TryGetSelectedClassAndBody(
                    out CharacterClassDefinitionData selectedClass,
                    out BodyTypeDefinitionData selectedBody))
            {
                return;
            }

            SetResult(
                $"Ô {_slotIndex}  •  {selectedClass.DisplayName}  •  " +
                $"{selectedBody.DisplayName}\n" +
                "Chọn ngoại hình, sau đó kiểm tra tên và tạo nhân vật.");
        }

        private void ClearAppearanceOptions()
        {
            foreach (TMP_Dropdown dropdown in
                     _appearanceDropdowns.Values)
            {
                if (dropdown != null)
                    dropdown.onValueChanged.RemoveAllListeners();
            }
            foreach (GameObject runtimeObject in
                     _runtimeAppearanceObjects)
            {
                if (runtimeObject != null)
                    Destroy(runtimeObject);
            }

            _appearanceDropdowns.Clear();
            _appearanceChoices.Clear();
            _runtimeAppearanceObjects.Clear();
        }

        private static int AppearanceSlotOrder(string slot) =>
            slot switch
            {
                "base_body" => 0,
                "hair" => 1,
                "bottom" => 2,
                "expression" => 3,
                _ => 100,
            };

        private static string DisplaySlotName(string slot) =>
            slot switch
            {
                "base_body" => "Cơ thể",
                "hair" => "Tóc",
                "bottom" => "Trang phục",
                "expression" => "Biểu cảm",
                _ => slot,
            };

        private static bool IsAllowed(
            IReadOnlyList<string> allowed,
            string selected) =>
            allowed == null ||
            allowed.Count == 0 ||
            allowed.Contains(selected);

        private void OnCharacterCreationResult(
            CharacterCreationResultEvent message)
        {
            bool completed =
                message.Success && message.Character != null;
            SetResult(completed
                ? $"Tạo thành công: {message.Character.CharacterName}"
                : $"Thất bại: {message.Message}");
            // Thành công sẽ tự gửi SelectCharacter và chuyển scene. Chỉ mở
            // lại form khi Server từ chối để tránh tạo/select lặp.
            if (!completed)
                SetInteractable(true);
        }

        private void OnBackClicked() =>
            _eventBus.Publish(new CharacterCreationCancelledEvent());

        private void SetInteractable(bool value)
        {
            if (_classDropdown != null)
                _classDropdown.interactable = value;
            if (_bodyTypeDropdown != null)
                _bodyTypeDropdown.interactable = value;
            if (_nameInput != null)
                _nameInput.interactable = value;
            if (_checkNameButton != null)
                _checkNameButton.interactable = value;
            if (_createButton != null)
                _createButton.interactable = value;
            if (_backButton != null)
                _backButton.interactable = value;
        }

        private static void SetDropdownOptions(
            TMP_Dropdown dropdown,
            IEnumerable<string> labels)
        {
            if (dropdown == null)
                return;
            dropdown.ClearOptions();
            dropdown.AddOptions(labels.ToList());
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
        }

        private void SetResult(string message)
        {
            if (_resultText != null)
                _resultText.text = message;
        }

        private void ApplyPanelPresentation()
        {
            Transform panelTransform = _classDropdown != null
                ? _classDropdown.transform.parent
                : transform;
            _theme?.ApplyPanel(
                panelTransform.GetComponent<Image>(),
                true);
            Image panelImage = panelTransform.GetComponent<Image>();
            if (panelImage != null)
                panelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.34f);
            CreatePresentationBackdrop(
                panelTransform,
                new Vector2(1040f, 640f),
                new Color(0.055f, 0.075f, 0.12f, 0.88f));
            CreateHeading(panelTransform);
            _theme?.ApplyDropdown(_classDropdown);
            _theme?.ApplyDropdown(_bodyTypeDropdown);
            _theme?.ApplyInput(_nameInput);
            _theme?.ApplyCleanButton(_checkNameButton, 54f);
            _theme?.ApplyCleanButton(_createButton, 54f);
            _theme?.ApplyCleanButton(_backButton, 54f);
            _theme?.ApplyBodyText(_resultText, 20f);

            SetRect(
                _classDropdown?.transform as RectTransform,
                new Vector2(-210f, 120f),
                new Vector2(360f, 54f));
            SetRect(
                _bodyTypeDropdown?.transform as RectTransform,
                new Vector2(-210f, 52f),
                new Vector2(360f, 54f));
            SetRect(
                _nameInput?.transform as RectTransform,
                new Vector2(-210f, -16f),
                new Vector2(360f, 54f));
            SetRect(
                _checkNameButton?.transform as RectTransform,
                new Vector2(-300f, -88f),
                new Vector2(170f, 54f));
            SetRect(
                _createButton?.transform as RectTransform,
                new Vector2(-120f, -88f),
                new Vector2(170f, 54f));
            SetRect(
                _backButton?.transform as RectTransform,
                new Vector2(-210f, -160f),
                new Vector2(360f, 54f));
            SetRect(
                _resultText?.transform as RectTransform,
                new Vector2(0f, 230f),
                new Vector2(900f, 64f));
            if (_resultText != null)
            {
                _resultText.alignment = TextAlignmentOptions.Center;
                _resultText.fontStyle = FontStyles.Bold;
            _resultText.color = new Color(1f, 0.96f, 0.84f, 1f);
            }
        }

        private static void CreatePresentationBackdrop(
            Transform parent,
            Vector2 size,
            Color color)
        {
            var card = new GameObject(
                "CharacterCreationBackdrop",
                typeof(RectTransform),
                typeof(Image));
            card.transform.SetParent(parent, false);
            card.transform.SetAsFirstSibling();
            RectTransform rect = (RectTransform)card.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image image = card.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private void CreateHeading(Transform parent)
        {
            var headingObject = new GameObject(
                "CharacterCreationHeading",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            headingObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)headingObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 315f);
            rect.sizeDelta = new Vector2(900f, 58f);
            TextMeshProUGUI heading =
                headingObject.GetComponent<TextMeshProUGUI>();
            heading.font = _resultText != null
                ? _resultText.font
                : TMP_Settings.defaultFontAsset;
            heading.text = "TẠO NHÂN VẬT";
            heading.fontSize = 38f;
            heading.fontStyle = FontStyles.Bold;
            heading.color = new Color(1f, 0.96f, 0.84f, 1f);
            heading.alignment = TextAlignmentOptions.Center;
            heading.raycastTarget = false;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            _classDropdown?.onValueChanged.RemoveListener(OnClassChanged);
            _bodyTypeDropdown?.onValueChanged.RemoveListener(
                OnBodyTypeChanged);
            _checkNameButton?.onClick.RemoveListener(OnCheckNameClicked);
            _createButton?.onClick.RemoveListener(OnCreateCharacterClicked);
            _backButton?.onClick.RemoveListener(OnBackClicked);
            _catalogSubscription?.Dispose();
            _slotSubscription?.Dispose();
            _creationSubscription?.Dispose();
            _nameSubscription?.Dispose();
            ClearAppearanceOptions();
            _isBound = false;
        }
    }
}
