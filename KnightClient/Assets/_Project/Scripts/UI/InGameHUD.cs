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

        private CharacterData _characterData;
        private IDisposable _disconnectionSubscription;
        private IDisposable _progressionSubscription;
        private IDisposable _vitalsSubscription;
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
        }

        private void Start()
        {
            RefreshPlayerStatus();
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

            if (_levelText != null)
                _levelText.text = $"Lv.{_characterData.Level}";

            SetResourceBar(
                _healthText,
                _healthFill,
                _characterData.CurrentHp,
                _characterData.MaxHp,
                "HP");
            SetResourceBar(
                _manaText,
                _manaFill,
                _characterData.CurrentMana,
                _characterData.MaxMana,
                "MP");

            long experienceToNext = Math.Max(
                0,
                _characterData.ExperienceToNextLevel);
            long experienceIntoLevel = Math.Clamp(
                _characterData.ExperienceIntoLevel,
                0,
                experienceToNext);
            if (_experienceText != null)
            {
                double percentage = experienceToNext > 0
                    ? experienceIntoLevel * 100d / experienceToNext
                    : 100d;
                _experienceText.text = experienceToNext > 0
                    ? $"EXP {percentage:F1}%"
                    : "EXP MAX";
            }
            if (_experienceFill != null)
            {
                _experienceFill.fillAmount = experienceToNext > 0
                    ? (float)experienceIntoLevel / experienceToNext
                    : 1f;
            }
        }

        private static void SetResourceBar(
            TMP_Text label,
            Image fill,
            int currentValue,
            int maximumValue,
            string prefix)
        {
            int safeMaximum = Math.Max(0, maximumValue);
            int safeCurrent = Math.Clamp(currentValue, 0, safeMaximum);
            if (label != null)
                label.text = $"{prefix} {safeCurrent:N0}/{safeMaximum:N0}";
            if (fill != null)
            {
                fill.fillAmount = safeMaximum > 0
                    ? (float)safeCurrent / safeMaximum
                    : 0f;
            }
        }

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
        }
    }
}
