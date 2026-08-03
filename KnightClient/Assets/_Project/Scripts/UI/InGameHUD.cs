using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Player;
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
        [Header("HUD Elements")]
        [SerializeField] private TextMeshProUGUI _characterNameText;
        [SerializeField] private TextMeshProUGUI _connectionStatusText;
        [SerializeField] private TextMeshProUGUI _positionDebugText;

        [Header("Player Status")]
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private Image _healthFill;
        [SerializeField] private TextMeshProUGUI _manaText;
        [SerializeField] private Image _manaFill;
        [SerializeField] private TextMeshProUGUI _experienceText;
        [SerializeField] private Image _experienceFill;

        private CharacterData _characterData;
        private Transform _playerTransform;
        private IDisposable _disconnectionSubscription;
        private IDisposable _progressionSubscription;

        [Inject]
        public void Construct(
            CharacterData characterData,
            PlayerController playerController,
            IEventBus eventBus)
        {
            _characterData = characterData;
            _playerTransform = playerController.transform;
            _disconnectionSubscription =
                eventBus.Subscribe<ServerDisconnectedEvent>(OnDisconnected);
            _progressionSubscription =
                eventBus.Subscribe<CharacterProgressionChangedEvent>(
                    OnProgressionChanged);
        }

        private void Start()
        {
            if (_characterNameText != null)
            {
                _characterNameText.text =
                    _characterData?.CharacterName ?? "Unknown";
            }
            RefreshPlayerStatus();
            SetConnectionStatus(true);
        }

        private void Update()
        {
            if (_playerTransform == null) return;
            var pos = _playerTransform.position;
            if (_positionDebugText != null)
                _positionDebugText.text = $"X: {pos.x:F1}  Y: {pos.y:F1}";
        }

        public void SetConnectionStatus(bool connected)
        {
            if (_connectionStatusText == null)
                return;

            _connectionStatusText.text =
                connected ? "● Connected" : "● Disconnected";
            _connectionStatusText.color = connected ? Color.green : Color.red;
        }

        private void OnDisconnected(ServerDisconnectedEvent gameEvent)
        {
            SetConnectionStatus(false);
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

        private void RefreshPlayerStatus()
        {
            if (_characterData == null)
                return;

            if (_levelText != null)
                _levelText.text = $"Lv. {_characterData.Level}";

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
                _experienceText.text = experienceToNext > 0
                    ? $"EXP {experienceIntoLevel:N0}/{experienceToNext:N0}"
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
            ValidateFilledImage(_healthFill, nameof(_healthFill));
            ValidateFilledImage(_manaFill, nameof(_manaFill));
            ValidateFilledImage(_experienceFill, nameof(_experienceFill));
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
        }
    }
}
