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
        private IDisposable _portalSubscription;
        private Label _mapName;
        private Label _questTitle;
        private Label _questDescription;
        private Label _progressText;
        private Label _statusLabel;
        private Label _noticeLabel;
        private string _defaultNotice;
        private VisualElement _progressFill;
        private VisualElement _details;
        private Button _collapseButton;
        private VisualElement _questCard;
        private VisualElement _functionMenuPanel;
        private VisualElement _functionMenuToggle;
        private VisualElement _logoutButton;
        private bool _collapsed;

        /// <summary>UI Toolkit root, for sibling views on the same document.</summary>
        public VisualElement Root => _document != null ? _document.rootVisualElement : null;
        private string _authoritativeMapId;
        private string _authoritativeStepId = HudPresentation.StepTalkToMother;
        private int _authoritativeProgress;
        private int _authoritativeRequired = 20;

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
            _noticeLabel = root.Q<Label>("quest-notice");
            _defaultNotice = _noticeLabel?.text;
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

            _authoritativeMapId = character?.CurrentMapDefinitionId;
            RenderMapName(_authoritativeMapId);
            RenderQuest(_authoritativeStepId, _authoritativeProgress,
                _authoritativeRequired);
            _mapSubscription = events.Subscribe<MapTransitionedEvent>(
                OnMapTransitioned);
            _questSubscription = events.Subscribe<TutorialProgressChangedEvent>(
                OnTutorialProgressChanged);
            _portalSubscription = events.Subscribe<PortalUseResultEvent>(
                OnPortalUseResult);
        }

        private void OnMapTransitioned(MapTransitionedEvent value)
        {
            _authoritativeMapId = value.MapId;
            RenderMapName(value.MapId);
            ShowNotice(null);
        }

        private void OnTutorialProgressChanged(TutorialProgressChangedEvent value)
        {
            _authoritativeStepId = value.StepId;
            _authoritativeProgress = value.Progress;
            _authoritativeRequired = value.Required;
            RenderQuest(value.StepId, value.Progress, value.Required);
            ShowNotice(null);
        }

        private void OnPortalUseResult(PortalUseResultEvent value) =>
            ShowNotice(HudPresentation.PortalResultMessage(value.Result));

        /// <summary>Shows a warning in the card footer; null restores the hint.</summary>
        private void ShowNotice(string message)
        {
            if (_noticeLabel == null) return;
            bool hasMessage = !string.IsNullOrEmpty(message);
            _noticeLabel.text = hasMessage ? message : _defaultNotice;
            _noticeLabel.EnableInClassList("footer-text--warning", hasMessage);
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

        private void RenderMapName(string mapId)
        {
            if (_mapName != null)
                _mapName.text = HudPresentation.MapDisplayName(mapId);
        }

        private void RenderQuest(string stepId, int progress, int required)
        {
            if (_questTitle == null) return;
            QuestCardModel model =
                HudPresentation.DescribeQuest(stepId, progress, required);
            _questTitle.text = model.Title;
            _questDescription.text = model.Description;
            _statusLabel.text = model.Status;
            _progressText.text = model.ProgressText;
            _progressFill.style.width = Length.Percent(model.ProgressRatio * 100f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Dev-only preview API used by HudDebugPanel. It renders values without
        // changing the cached authoritative state, so Restore always returns
        // to what the server last sent.
        public string DebugAuthoritativeSummary =>
            $"map={_authoritativeMapId ?? "-"} | step={_authoritativeStepId} " +
            $"({_authoritativeProgress}/{_authoritativeRequired})";

        public void DebugPreviewQuest(string stepId, int progress, int required) =>
            RenderQuest(stepId, progress, required);

        public void DebugPreviewMap(string mapId) => RenderMapName(mapId);

        public void DebugPreviewPortalResult(byte result) =>
            ShowNotice(HudPresentation.PortalResultMessage(result));

        public void DebugRestoreAuthoritative()
        {
            RenderMapName(_authoritativeMapId);
            RenderQuest(_authoritativeStepId, _authoritativeProgress,
                _authoritativeRequired);
            ShowNotice(null);
        }
#endif

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
            _portalSubscription?.Dispose();
            if (_collapseButton != null)
                _collapseButton.clicked -= ToggleDetails;
            if (_panelSettings != null)
                Destroy(_panelSettings);
        }
    }
}
