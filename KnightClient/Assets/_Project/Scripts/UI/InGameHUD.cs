using System;
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
    /// HUD tối thiểu cho InGame scene.
    /// Hiển thị tên nhân vật, trạng thái kết nối và vị trí debug.
    /// Inject CharacterData và PlayerController qua VContainer.
    /// </summary>
    public sealed class InGameHUD : MonoBehaviour
    {
        [Header("Player Status")]
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private Image _healthFill;
        [SerializeField] private TextMeshProUGUI _manaText;
        [SerializeField] private Image _manaFill;
        [SerializeField] private TextMeshProUGUI _experienceText;
        [SerializeField] private Image _experienceFill;
        [Header("Quest")]
        [SerializeField] private TextMeshProUGUI _questText;
        [SerializeField] private TextMeshProUGUI _mapNameText;

        private CharacterData _characterData;
        private IDisposable _disconnectionSubscription;
        private IDisposable _progressionSubscription;
        private IDisposable _vitalsSubscription;
        private IDisposable _questSubscription;
        private IDisposable _mapSubscription;
        private IDisposable _portalSubscription;
        private long _lastVitalsSequence;

        [Inject]
        public void Construct(
            CharacterData characterData,
            IEventBus eventBus)
        {
            _characterData = characterData;
            _disconnectionSubscription =
                eventBus.Subscribe<ServerDisconnectedEvent>(OnDisconnected);
            _progressionSubscription =
                eventBus.Subscribe<CharacterProgressionChangedEvent>(
                    OnProgressionChanged);
            _vitalsSubscription =
                eventBus.Subscribe<CharacterVitalsChangedEvent>(
                    OnVitalsChanged);
            _questSubscription = eventBus.Subscribe<TutorialProgressChangedEvent>(
                OnTutorialProgressChanged);
            _mapSubscription = eventBus.Subscribe<MapTransitionedEvent>(
                OnMapTransitioned);
            _portalSubscription = eventBus.Subscribe<PortalUseResultEvent>(
                OnPortalUseResult);
        }

        private void Start()
        {
            EnsureQuestLabel();
            SetMapName(_characterData?.CurrentMapDefinitionId);
            RefreshPlayerStatus();
        }

        public void SetQuestCardVisible(bool visible)
        {
            EnsureQuestLabel();
            if (_questText != null && _questText.transform.parent != null)
                _questText.transform.parent.gameObject.SetActive(visible);
        }

        private void EnsureQuestLabel()
        {
            if (_questText != null) return;
            var panel = new GameObject("StarterQuestPanel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(20f, -185f);
            panelRect.sizeDelta = new Vector2(620f, 150f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.035f, 0.055f, 0.085f, 0.94f);
            background.raycastTarget = false;

            var mapObject = new GameObject("MapNameText",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            mapObject.transform.SetParent(panel.transform, false);
            RectTransform mapRect = (RectTransform)mapObject.transform;
            mapRect.anchorMin = new Vector2(0f, 1f);
            mapRect.anchorMax = new Vector2(1f, 1f);
            mapRect.pivot = new Vector2(0.5f, 1f);
            mapRect.offsetMin = new Vector2(14f, -46f);
            mapRect.offsetMax = new Vector2(-14f, -10f);
            _mapNameText = mapObject.GetComponent<TextMeshProUGUI>();
            _mapNameText.fontSize = 28f;
            _mapNameText.fontStyle = FontStyles.Bold;
            _mapNameText.color = new Color(1f, 0.72f, 0.18f);

            var value = new GameObject("QuestStatusText",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            value.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(14f, -98f);
            rect.offsetMax = new Vector2(-14f, -50f);
            _questText = value.GetComponent<TextMeshProUGUI>();
            _questText.fontSize = 25f;
            _questText.color = Color.white;
            _questText.fontStyle = FontStyles.Bold;
            _questText.outlineColor = Color.black;
            _questText.outlineWidth = 0.18f;
            _questText.textWrappingMode = TextWrappingModes.Normal;
            _questText.text = HudPresentation.DescribeQuest(
                HudPresentation.StepTalkToMother, 0, 1).SummaryLine;
        }

        private void OnMapTransitioned(MapTransitionedEvent value) =>
            SetMapName(value.MapId);

        private void OnPortalUseResult(PortalUseResultEvent value)
        {
            string message = HudPresentation.PortalResultMessage(value.Result);
            if (_questText == null || message == null)
                return;
            _questText.text = message;
            _questText.color = new Color(1f, 0.55f, 0.25f);
        }

        private void SetMapName(string mapId)
        {
            EnsureQuestLabel();
            string displayName = HudPresentation.MapDisplayName(mapId);
            if (_mapNameText != null)
                _mapNameText.text = displayName;
            TMP_Text minimapName = GameObject.Find("ZoneNameText")?
                .GetComponent<TMP_Text>();
            if (minimapName != null)
            {
                minimapName.text = displayName;
                minimapName.color = Color.white;
            }
        }

        private void OnTutorialProgressChanged(TutorialProgressChangedEvent value)
        {
            EnsureQuestLabel();
            _questText.color = Color.white;
            _questText.text = HudPresentation.DescribeQuest(
                value.StepId, value.Progress, value.Required).SummaryLine;
        }

        private void OnDisconnected(ServerDisconnectedEvent gameEvent)
        {
            if (gameEvent.IsForced && !string.IsNullOrWhiteSpace(gameEvent.Message))
                Debug.LogError($"[Network] {gameEvent.Message}");
        }

        private void OnProgressionChanged(
            CharacterProgressionChangedEvent progression)
        {
            if (_characterData == null)
                return;

            _characterData.Level = progression.Level;
            _characterData.TotalExperience = progression.TotalExperience;
            _characterData.ExperienceIntoLevel =
                progression.ExperienceIntoLevel;
            _characterData.ExperienceToNextLevel =
                progression.ExperienceToNextLevel;
            _characterData.CurrentHp = progression.CurrentHealth;
            _characterData.MaxHp = progression.MaximumHealth;
            _characterData.CurrentMana = progression.CurrentMana;
            _characterData.MaxMana = progression.MaximumMana;
            _characterData.Attack = progression.Attack;
            _characterData.Defense = progression.Defense;
            RefreshPlayerStatus();
        }

        private void OnVitalsChanged(CharacterVitalsChangedEvent vitals)
        {
            if (_characterData == null ||
                vitals.Sequence <= _lastVitalsSequence)
            {
                return;
            }

            _lastVitalsSequence = vitals.Sequence;
            _characterData.CurrentHp = vitals.CurrentHealth;
            _characterData.MaxHp = vitals.MaximumHealth;
            _characterData.CurrentMana = vitals.CurrentMana;
            _characterData.MaxMana = vitals.MaximumMana;
            RefreshPlayerStatus();
        }

        private void RefreshPlayerStatus()
        {
            if (_characterData == null)
                return;

            RenderPlayerStatus(
                _characterData.Level,
                _characterData.CurrentHp,
                _characterData.MaxHp,
                _characterData.CurrentMana,
                _characterData.MaxMana,
                _characterData.ExperienceIntoLevel,
                _characterData.ExperienceToNextLevel);
        }

        private void RenderPlayerStatus(int level, int currentHp, int maxHp,
            int currentMana, int maxMana, long experienceIntoLevel,
            long experienceToNextLevel)
        {
            if (_levelText != null)
                _levelText.text = HudPresentation.LevelLabel(level);

            ApplyBar(_healthText, _healthFill,
                HudPresentation.ResourceBar("HP", currentHp, maxHp));
            ApplyBar(_manaText, _manaFill,
                HudPresentation.ResourceBar("MP", currentMana, maxMana));
            ApplyBar(_experienceText, _experienceFill,
                HudPresentation.ExperienceBar(
                    experienceIntoLevel, experienceToNextLevel));
        }

        private static void ApplyBar(TMP_Text label, Image fill, HudBarModel bar)
        {
            if (label != null)
                label.text = bar.Label;
            if (fill != null)
                fill.fillAmount = bar.Fill;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Dev-only: renders arbitrary values without touching CharacterData
        /// or the vitals sequence, so a later server event always wins.
        /// </summary>
        public void DebugPreviewPlayerStatus(int level, int currentHp, int maxHp,
            int currentMana, int maxMana, long experienceIntoLevel,
            long experienceToNextLevel) =>
            RenderPlayerStatus(level, currentHp, maxHp, currentMana, maxMana,
                experienceIntoLevel, experienceToNextLevel);

        /// <summary>Dev-only: re-renders the last authoritative values.</summary>
        public void DebugRestoreAuthoritativeStatus() => RefreshPlayerStatus();

        public CharacterData DebugCharacterData => _characterData;
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_levelText == null ||
                _healthText == null ||
                _healthFill == null ||
                _manaText == null ||
                _manaFill == null ||
                _experienceText == null)
            {
                Debug.LogWarning(
                    "[InGameHUD] One or more serialized HUD references are " +
                    "missing. Run KnightOnline > UI > Build Player Status " +
                    "HUD or reconnect them in the Inspector.",
                    this);
            }

            ValidateFilledImage(_healthFill, nameof(_healthFill));
            ValidateFilledImage(_manaFill, nameof(_manaFill));
            if (_experienceFill != null)
                ValidateFilledImage(
                    _experienceFill,
                    nameof(_experienceFill));
        }

        private static void ValidateFilledImage(
            Image image,
            string fieldName)
        {
            if (image != null && image.type != Image.Type.Filled)
            {
                Debug.LogWarning(
                    $"[InGameHUD] {fieldName} must use Image Type = Filled.",
                    image);
            }
        }
#endif

        private void OnDestroy()
        {
            _disconnectionSubscription?.Dispose();
            _progressionSubscription?.Dispose();
            _vitalsSubscription?.Dispose();
            _questSubscription?.Dispose();
            _mapSubscription?.Dispose();
            _portalSubscription?.Dispose();
        }
    }
}
