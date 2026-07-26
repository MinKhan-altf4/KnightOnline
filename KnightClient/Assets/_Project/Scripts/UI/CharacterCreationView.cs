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

        private IEventBus _eventBus;
        private CharacterCreationCatalogData _catalog;
        private int _slotIndex;
        private IDisposable _catalogSubscription;
        private IDisposable _slotSubscription;
        private IDisposable _creationSubscription;
        private IDisposable _nameSubscription;

        [Inject]
        public void Construct(IEventBus eventBus) => Initialize(eventBus);

        public void Initialize(IEventBus eventBus) =>
            _eventBus = eventBus ??
                throw new ArgumentNullException(nameof(eventBus));

        private void Start()
        {
            if (_eventBus == null)
            {
                Debug.LogError(
                    "[CharacterCreationView] EventBus was not injected.",
                    this);
                enabled = false;
                return;
            }

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
                    message => SetResult(message.Message));

            _classDropdown?.onValueChanged.AddListener(OnClassChanged);
            _checkNameButton?.onClick.AddListener(OnCheckNameClicked);
            _createButton?.onClick.AddListener(OnCreateCharacterClicked);
            _backButton?.onClick.AddListener(OnBackClicked);
            SetInteractable(false);
        }

        private void OnCatalogReceived(
            CharacterCreationCatalogReceivedEvent message)
        {
            _catalog = message.Catalog;
            if (_catalog == null)
            {
                SetResult("Không tải được dữ liệu tạo nhân vật.");
                return;
            }

            SetDropdownOptions(
                _classDropdown,
                _catalog.Classes.Select(value => value.DisplayName));
            RefreshBodyTypes();
            SetInteractable(true);
            SetResult($"Đang tạo nhân vật ở ô {_slotIndex}.");
        }

        private void OnClassChanged(int _) => RefreshBodyTypes();

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

        private void OnCheckNameClicked()
        {
            if (_catalog == null)
                return;
            string name = _nameInput?.text?.Trim() ?? string.Empty;
            _eventBus.Publish(
                new CharacterNameCheckRequestedEvent(
                    _catalog.ServerId,
                    name));
        }

        public void OnCreateCharacterClicked()
        {
            if (_catalog == null || _slotIndex is < 1 or > 3)
            {
                SetResult("Dữ liệu tạo nhân vật hoặc ô nhân vật chưa hợp lệ.");
                return;
            }

            CharacterClassDefinitionData selectedClass =
                _catalog.Classes[
                    Mathf.Clamp(
                        _classDropdown?.value ?? 0,
                        0,
                        _catalog.Classes.Count - 1)];
            BodyTypeDefinitionData[] allowedBodies =
                _catalog.BodyTypes
                    .Where(body =>
                        selectedClass.AllowedBodyTypeIds.Contains(
                            body.DefinitionId))
                    .ToArray();
            if (allowedBodies.Length == 0)
            {
                SetResult("Class này chưa có body type hợp lệ.");
                return;
            }

            BodyTypeDefinitionData selectedBody = allowedBodies[
                Mathf.Clamp(
                    _bodyTypeDropdown?.value ?? 0,
                    0,
                    allowedBodies.Length - 1)];
            AppearanceSelectionData[] starterAppearance =
                SelectStarterAppearance(
                    selectedClass.DefinitionId,
                    selectedBody.DefinitionId);

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

        private AppearanceSelectionData[] SelectStarterAppearance(
            string classId,
            string bodyTypeId)
        {
            string[] requiredSlots =
                { "base_body", "hair", "bottom", "expression" };
            return requiredSlots.Select(slot =>
            {
                AppearanceDefinitionData option =
                    _catalog.AppearanceOptions.FirstOrDefault(value =>
                        value.IsStarterOption &&
                        value.SlotDefinitionId == slot &&
                        IsAllowed(value.AllowedBodyTypeIds, bodyTypeId) &&
                        IsAllowed(
                            value.AllowedClassDefinitionIds,
                            classId));
                return option == null
                    ? null
                    : new AppearanceSelectionData
                    {
                        SlotDefinitionId = slot,
                        OptionDefinitionId = option.DefinitionId,
                    };
            }).Where(value => value != null).ToArray();
        }

        private static bool IsAllowed(
            IReadOnlyList<string> allowed,
            string selected) =>
            allowed == null ||
            allowed.Count == 0 ||
            allowed.Contains(selected);

        private void OnCharacterCreationResult(
            CharacterCreationResultEvent message)
        {
            SetResult(message.Success
                ? $"Tạo thành công: {message.Character.CharacterName}"
                : $"Thất bại: {message.Message}");
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

        private void OnDestroy()
        {
            _classDropdown?.onValueChanged.RemoveListener(OnClassChanged);
            _checkNameButton?.onClick.RemoveListener(OnCheckNameClicked);
            _createButton?.onClick.RemoveListener(OnCreateCharacterClicked);
            _backButton?.onClick.RemoveListener(OnBackClicked);
            _catalogSubscription?.Dispose();
            _slotSubscription?.Dispose();
            _creationSubscription?.Dispose();
            _nameSubscription?.Dispose();
        }
    }
}
