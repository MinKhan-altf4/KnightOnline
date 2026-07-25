using System;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Targeting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnightOnline.Client.Gameplay.Monster
{
    /// <summary>Displays one monster snapshot in the world.</summary>
    public sealed class MonsterView : MonoBehaviour, ITargetable
    {
        [Header("World-space UI")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private Image _healthFill;
        [SerializeField] private Transform _markerAnchor;

        public int MonsterId { get; private set; }
        public event Action<MonsterView> Clicked;

        public MonsterData Data { get; private set; }
        public int TargetId => MonsterId;
        public TargetType TargetType => KnightOnline.Client.Gameplay.Targeting.TargetType.Monster;
        public string DisplayName => Data?.MonsterName ?? string.Empty;
        public int Level => Data?.Level ?? 0;
        public int CurrentHealth => Data?.CurrentHealth ?? 0;
        public int MaximumHealth => Data?.MaximumHealth ?? 0;
        public bool ShowsHealth => true;
        public Transform MarkerAnchor => _markerAnchor != null ? _markerAnchor : transform;

        private void Awake()
        {
            // The selected target is presented by TargetHUD. Hide the legacy
            // world-space name/health canvas if an older prefab still contains it.
            var worldSpaceCanvas = _nameText != null
                ? _nameText.GetComponentInParent<Canvas>()
                : _healthText != null
                    ? _healthText.GetComponentInParent<Canvas>()
                    : null;

            if (worldSpaceCanvas != null)
                worldSpaceCanvas.gameObject.SetActive(false);
        }

        public void Render(MonsterData monster)
        {
            Data = monster;
            MonsterId = monster.MonsterId;
            transform.position = new Vector3(monster.Position.x, monster.Position.y, transform.position.z);

            if (_nameText != null)
                _nameText.text = monster.MonsterName;

            var maximumHealth = Mathf.Max(0, monster.MaximumHealth);
            var currentHealth = Mathf.Clamp(monster.CurrentHealth, 0, maximumHealth);

            if (_healthText != null)
                _healthText.text = $"{currentHealth}/{maximumHealth}";

            if (_healthFill != null)
                _healthFill.fillAmount = maximumHealth > 0 ? (float)currentHealth / maximumHealth : 0f;

            gameObject.name = $"Monster_{monster.MonsterId}_{monster.MonsterName}";
        }

        private void OnMouseDown()
        {
            Clicked?.Invoke(this);
        }
    }
}
