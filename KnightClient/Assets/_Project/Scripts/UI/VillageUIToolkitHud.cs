using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnightOnline.Client.UI
{
    /// <summary>
    /// Small runtime UI Toolkit example: UXML owns structure, USS owns styling,
    /// and this view only binds authoritative map and tutorial events.
    /// </summary>
    public sealed class VillageUIToolkitHud : MonoBehaviour
    {
        private UIDocument _document;
        private PanelSettings _panelSettings;
        private IDisposable _mapSubscription;
        private IDisposable _questSubscription;
        private Label _mapName;
        private Label _questTitle;
        private Label _questDescription;
        private Label _progressText;
        private Label _statusLabel;
        private VisualElement _progressFill;
        private VisualElement _details;
        private Button _collapseButton;
        private VisualElement _questCard;
        private VisualElement _functionMenuPanel;
        private VisualElement _functionMenuToggle;
        private VisualElement _logoutButton;
        private bool _collapsed;

        public void Initialize(IEventBus events, CharacterData character,
            InGameMenuView menuView)
        {
            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>("UI/VillageHud");
            if (tree == null)
            {
                Debug.LogError("[UI Toolkit] Cannot load Resources/UI/VillageHud.uxml.");
                return;
            }

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.themeStyleSheet =
                Resources.Load<ThemeStyleSheet>("UI/VillageDefaultTheme");
            if (_panelSettings.themeStyleSheet == null)
            {
                Debug.LogError(
                    "[UI Toolkit] Cannot load Resources/UI/VillageDefaultTheme.tss.");
                Destroy(_panelSettings);
                _panelSettings = null;
                return;
            }

            _document = gameObject.AddComponent<UIDocument>();
            _document.enabled = false;
            _document.panelSettings = _panelSettings;
            _document.visualTreeAsset = tree;
            _document.sortingOrder = 20;
            _document.enabled = true;

            VisualElement root = _document.rootVisualElement;
            _mapName = root.Q<Label>("map-name");
            _questTitle = root.Q<Label>("quest-title");
            _questDescription = root.Q<Label>("quest-description");
            _progressText = root.Q<Label>("progress-text");
            _statusLabel = root.Q<Label>("status-label");
            _progressFill = root.Q<VisualElement>("progress-fill");
            _details = root.Q<VisualElement>("quest-details");
            _questCard = root.Q<VisualElement>("quest-card");
            _collapseButton = root.Q<Button>("collapse-button");
            _functionMenuPanel = root.Q("function-menu-panel");
            _functionMenuToggle = root.Q("function-menu-toggle");
            _logoutButton = root.Q("logout-button");
            LogMissingMenuElements(tree);
            _collapseButton.clicked += ToggleDetails;
            menuView?.Initialize(root, SetQuestCardVisible, tree.name);

            SetMapName(character?.CurrentMapDefinitionId);
            SetQuest("talk_to_mother", 0, 20);
            _mapSubscription = events.Subscribe<MapTransitionedEvent>(
                value => SetMapName(value.MapId));
            _questSubscription = events.Subscribe<TutorialProgressChangedEvent>(
                value => SetQuest(value.StepId, value.Progress, value.Required));
        }

        private void LogMissingMenuElements(VisualTreeAsset tree)
        {
            if (_functionMenuPanel != null && _functionMenuToggle != null &&
                _logoutButton != null)
                return;

            Debug.LogError(
                "[UI Toolkit] Loaded VillageHud asset is missing function menu nodes. " +
                $"asset={tree.name}, panel={_functionMenuPanel != null}, " +
                $"toggle={_functionMenuToggle != null}, " +
                $"logout={_logoutButton != null}.",
                this);
        }

        private void SetMapName(string mapId)
        {
            if (_mapName == null) return;
            _mapName.text = mapId switch
            {
                "tutorial_map_01" => "LÀNG TÂN THỦ",
                "wolf_field_01" => "BÃI SÓI",
                "safe_zone_01" => "SAFE ZONE 1",
                _ => string.IsNullOrWhiteSpace(mapId) ? "KHU VỰC" : mapId,
            };
        }

        private void SetQuest(string stepId, int progress, int required)
        {
            if (_questTitle == null) return;
            required = Mathf.Max(1, required);
            progress = Mathf.Clamp(progress, 0, required);
            float percent = (float)progress / required * 100f;

            switch (stepId)
            {
                case "talk_to_mother":
                    _questTitle.text = "LỜI NHẮN CỦA MẸ";
                    _questDescription.text = "Tìm Mẹ trong làng và nhận nhiệm vụ đầu tiên.";
                    _statusLabel.text = "MỚI";
                    break;
                case "hunt_20_wolves":
                    _questTitle.text = "BẦY SÓI NGOÀI BÌA LÀNG";
                    _questDescription.text = "Qua cổng đến Bãi Sói và hạ gục 20 Wolf.";
                    _statusLabel.text = "ĐANG LÀM";
                    break;
                case "return_to_mother":
                    _questTitle.text = "TRỞ VỀ GẶP MẸ";
                    _questDescription.text = "Đã đủ số Wolf. Quay về làng để nhận thưởng.";
                    _statusLabel.text = "HOÀN THÀNH MỤC TIÊU";
                    break;
                case "depart_for_safe_zone_01":
                    _questTitle.text = "LÊN ĐƯỜNG";
                    _questDescription.text = "Nhiệm vụ tân thủ hoàn tất. Cổng Safe Zone 1 đang chờ.";
                    _statusLabel.text = "ĐÃ XONG";
                    progress = required;
                    percent = 100f;
                    break;
                default:
                    _questTitle.text = "HÀNH TRÌNH TÂN THỦ";
                    _questDescription.text = stepId;
                    _statusLabel.text = "NHIỆM VỤ";
                    break;
            }

            _progressText.text = stepId == "talk_to_mother"
                ? "CHƯA NHẬN"
                : $"{progress:00} / {required:00}";
            _progressFill.style.width = Length.Percent(percent);
        }

        private void ToggleDetails()
        {
            _collapsed = !_collapsed;
            _details.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _collapseButton.text = _collapsed ? "+" : "−";
        }

        private void SetQuestCardVisible(bool visible)
        {
            if (_questCard != null)
                _questCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDestroy()
        {
            _mapSubscription?.Dispose();
            _questSubscription?.Dispose();
            if (_collapseButton != null)
                _collapseButton.clicked -= ToggleDetails;
            if (_panelSettings != null)
                Destroy(_panelSettings);
        }
    }
}
