using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Gameplay.Targeting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace KnightOnline.Client.UI
{
    public sealed class TargetHUD : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _targetNameText;
        [SerializeField] private TMP_Text _targetLevelText;
        [SerializeField] private GameObject _healthGroup;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private Image _healthFill;

        private ITargetable _target;
        private IDisposable _selectedSubscription;
        private IDisposable _clearedSubscription;

        [Inject]
        public void Construct(IEventBus eventBus)
        {
            _selectedSubscription = eventBus.Subscribe<TargetSelectedEvent>(OnTargetSelected);
            _clearedSubscription = eventBus.Subscribe<TargetClearedEvent>(_ => Hide());
        }

        private void Start()
        {
            Hide();
        }

        private void Update()
        {
            if (_target == null)
                return;

            if (_target.MarkerAnchor == null)
            {
                Hide();
                return;
            }

            RefreshHealth();
        }

        private void OnTargetSelected(TargetSelectedEvent gameEvent)
        {
            _target = gameEvent.Target;
            if (_target == null)
            {
                Hide();
                return;
            }

            if (_targetNameText != null)
                _targetNameText.text = _target.DisplayName;

            if (_targetLevelText != null)
                _targetLevelText.text = $"Lv. {_target.Level}";

            if (_healthGroup != null)
                _healthGroup.SetActive(_target.ShowsHealth);

            RefreshHealth();

            if (_panel != null)
                _panel.SetActive(true);
        }

        private void RefreshHealth()
        {
            if (_target == null || !_target.ShowsHealth)
                return;

            var maximumHealth = Mathf.Max(0, _target.MaximumHealth);
            var currentHealth = Mathf.Clamp(_target.CurrentHealth, 0, maximumHealth);

            if (_healthText != null)
                _healthText.text = $"{currentHealth}/{maximumHealth}";

            if (_healthFill != null)
                _healthFill.fillAmount = maximumHealth > 0
                    ? (float)currentHealth / maximumHealth
                    : 0f;
        }

        private void Hide()
        {
            _target = null;
            if (_panel != null)
                _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            _selectedSubscription?.Dispose();
            _clearedSubscription?.Dispose();
        }
    }
}
