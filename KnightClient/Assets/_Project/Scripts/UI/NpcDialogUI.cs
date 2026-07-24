using System;
using System.Collections.Generic;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Gameplay.NPC;
using KnightOnline.Client.Gameplay.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace KnightOnline.Client.UI
{
    public class NpcDialogUI : MonoBehaviour
    {
        private const float ButtonWidth = 160f;
        private const float ButtonHeight = 30f;
        private const float HorizontalSpacing = 10f;
        private const float VerticalSpacing = 6f;

        [Header("UI References")]
        [SerializeField] private GameObject _dialogPanel;
        [SerializeField] private TextMeshProUGUI _npcNameText;
        [SerializeField] private TextMeshProUGUI _greetingText;
        [SerializeField] private RectTransform _buttonContainer;

        [Header("Prefabs")]
        [SerializeField] private GameObject _optionButtonPrefab;

        private IEventBus _eventBus;
        private PlayerController _playerController;
        private PlayerInteraction _playerInteraction;
        private IDisposable _interactionSubscription;
        private RectTransform _dialogRect;
        private InteractableNPC _activeNpc;
        private EntityId _activeNpcEntityId;
        private string _activeNpcName;
        private int _openedFrame = -1;

        private bool IsDialogOpen => _dialogPanel != null && _dialogPanel.activeSelf;

        [Inject]
        public void Construct(
            IEventBus eventBus,
            PlayerController playerController,
            PlayerInteraction playerInteraction)
        {
            _eventBus = eventBus;
            _playerController = playerController;
            _playerInteraction = playerInteraction;
        }

        private void Awake()
        {
            _dialogRect = _dialogPanel.GetComponent<RectTransform>();
            _dialogPanel.SetActive(false);
            ConfigureButtonContainer();
        }

        private void Start()
        {
            _interactionSubscription =
                _eventBus.Subscribe<NpcInteractionRequestedEvent>(ShowDialog);
        }

        private void OnDisable()
        {
            CloseDialog();
        }

        private void OnDestroy()
        {
            _interactionSubscription?.Dispose();
            _interactionSubscription = null;
            CloseDialog();
        }

        private void Update()
        {
            if (!IsDialogOpen)
                return;

            // Unity object đã bị Destroy sẽ so sánh bằng null.
            if (_activeNpc == null)
            {
                CloseDialog();
                return;
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                CloseDialog();
                return;
            }

            // Không xử lý click đã dùng để mở dialog trong cùng frame.
            if (Time.frameCount == _openedFrame ||
                Mouse.current?.leftButton.wasPressedThisFrame != true)
            {
                return;
            }

            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    _dialogRect,
                    pointerPosition))
            {
                CloseDialog();
            }
        }

        private void ShowDialog(NpcInteractionRequestedEvent interaction)
        {
            // Dialog đang mở thì bỏ qua mọi yêu cầu interaction mới.
            if (IsDialogOpen || interaction.Source == null)
                return;

            _activeNpc = interaction.Source;
            _activeNpcEntityId = interaction.NpcEntityId;
            _activeNpcName = interaction.NpcName;
            _openedFrame = Time.frameCount;

            _dialogPanel.SetActive(true);
            SetPlayerControlsEnabled(false);
            _npcNameText.text = interaction.NpcName;
            _greetingText.text = interaction.GreetingText;

            // Xóa các nút cũ
            for (int index = _buttonContainer.childCount - 1; index >= 0; index--)
            {
                Transform child = _buttonContainer.GetChild(index);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            var regularOptions = new List<NpcOptionData>();
            NpcOptionData? configuredCloseOption = null;

            foreach (NpcOptionData option in interaction.Options)
            {
                if (option.Action == NpcActionType.Close)
                {
                    // Chỉ giữ một nút Close nếu Inspector vô tình cấu hình lặp.
                    configuredCloseOption ??= option;
                }
                else
                {
                    regularOptions.Add(option);
                }
            }

            CreateOptionRows(regularOptions, 2);

            // Mọi NPC luôn có đúng một nút Đóng ở hàng cuối. Nếu Inspector
            // không cấu hình Close, UI tự tạo để NPC chỉ-có-câu-chào vẫn dùng được.
            NpcOptionData closeOption = configuredCloseOption ??
                new NpcOptionData("Đóng", NpcActionType.Close);
            RectTransform closeRow = CreateRow();
            CreateOptionButton(closeOption, closeRow);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_buttonContainer);
        }

        private void ConfigureButtonContainer()
        {
            if (_buttonContainer.TryGetComponent(out VerticalLayoutGroup layout))
            {
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = VerticalSpacing;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
        }

        private void CreateOptionRows(IReadOnlyList<NpcOptionData> options, int optionsPerRow)
        {
            for (int index = 0; index < options.Count; index += optionsPerRow)
            {
                RectTransform row = CreateRow();
                int rowEnd = Mathf.Min(index + optionsPerRow, options.Count);

                for (int optionIndex = index; optionIndex < rowEnd; optionIndex++)
                {
                    CreateOptionButton(options[optionIndex], row);
                }
            }
        }

        private RectTransform CreateRow()
        {
            var rowObject = new GameObject(
                "OptionRow",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));

            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.SetParent(_buttonContainer, false);
            row.sizeDelta = new Vector2(_buttonContainer.rect.width, ButtonHeight);

            HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = HorizontalSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return row;
        }

        private void CreateOptionButton(NpcOptionData option, Transform row)
        {
            GameObject buttonObject = Instantiate(_optionButtonPrefab, row);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            buttonObject.GetComponentInChildren<TextMeshProUGUI>().text = option.Text;

            Button button = buttonObject.GetComponent<Button>();
            NpcActionType action = option.Action;
            button.onClick.AddListener(() => OnOptionClicked(action));
        }

        private void OnOptionClicked(NpcActionType action)
        {
            if (action == NpcActionType.Close)
            {
                CloseDialog();
                return;
            }

            _eventBus.Publish(new NpcActionRequestedEvent(
                _activeNpcEntityId,
                _activeNpcName,
                action));
        }

        public void CloseDialog()
        {
            if (_dialogPanel != null)
                _dialogPanel.SetActive(false);

            _activeNpc = null;
            _activeNpcEntityId = default;
            _activeNpcName = null;
            _openedFrame = -1;
            SetPlayerControlsEnabled(true);
        }

        private void SetPlayerControlsEnabled(bool enabled)
        {
            if (_playerController != null)
                _playerController.SetMovementEnabled(enabled);

            if (_playerInteraction != null)
                _playerInteraction.SetInteractionEnabled(enabled);
        }
    }
}
