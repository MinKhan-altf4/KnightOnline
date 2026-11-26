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

            // Blocking is decided by the server's circle rule, which the
            // player predicts (CircleObstacleResolver). A solid physics box
            // here stopped and slid the player differently from the server,
            // so every touch ended in a correction. Keep the collider for
            // clicks only (Physics2D queries still hit triggers).
            foreach (Collider2D bodyCollider in GetComponentsInChildren<Collider2D>(true))
                bodyCollider.isTrigger = true;
        }

        public void Render(MonsterData monster)
        {
            Data = monster;
            MonsterId = monster.MonsterId;

            if (_nameText != null)
                _nameText.text = monster.MonsterName;

            var maximumHealth = Mathf.Max(0, monster.MaximumHealth);
            var currentHealth = Mathf.Clamp(monster.CurrentHealth, 0, maximumHealth);

            if (_healthText != null)
                _healthText.text = $"{currentHealth}/{maximumHealth}";

            if (_healthFill != null)
                _healthFill.fillAmount = maximumHealth > 0 ? (float)currentHealth / maximumHealth : 0f;

            gameObject.name = $"Monster_{monster.MonsterId}_{monster.MonsterName}";
            ApplyArt(monster.DefinitionId);
        }

        private const string ArtPath = "Monsters/monster_";
        /// <summary>Same order as the hero and standing props: Y-sorted against them.</summary>
        private const int StandingSortingOrder = 1;
        private static readonly System.Collections.Generic.Dictionary<int, Sprite> ArtCache = new();
        private int _artDefinitionId;

        /// <summary>
        /// Swaps the placeholder body for Resources/Monsters/monster_{definitionId}
        /// when that sprite exists; definitions without art keep the prefab look.
        /// </summary>
        private void ApplyArt(int definitionId)
        {
            if (definitionId == _artDefinitionId)
                return;
            _artDefinitionId = definitionId;

            if (!ArtCache.TryGetValue(definitionId, out Sprite art))
            {
                art = Resources.Load<Sprite>(ArtPath + definitionId);
                ArtCache[definitionId] = art;
            }
            if (art == null)
                return;

            SpriteRenderer body = GetComponentInChildren<SpriteRenderer>(true);
            if (body == null)
                return;
            body.sprite = art;
            body.color = Color.white;
            body.drawMode = SpriteDrawMode.Simple;
            body.transform.localScale = Vector3.one;
            body.sortingOrder = StandingSortingOrder;
            body.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        /// <summary>Where the monster is drawn now (used for movement blocking).</summary>
        public Vector2 CurrentPosition => transform.position;

        // Wander leg being replayed (server-authoritative, see MonsterMovedPacket).
        private Vector2 _moveFrom;
        private Vector2 _moveTo;
        private float _moveStartedAt;
        private float _moveDuration;
        private bool _moving;

        /// <summary>Places the monster from a fresh server snapshot (list or respawn).</summary>
        public void SyncMotion(MonsterData monster)
        {
            if (monster.MoveRemainingSeconds > 0f && monster.MoveTarget != monster.Position)
            {
                SetPosition(monster.Position);
                BeginMove(monster.Position, monster.MoveTarget, monster.MoveRemainingSeconds);
                return;
            }
            _moving = false;
            SetPosition(monster.Position);
        }

        /// <summary>
        /// Replays a wander leg. Starts from where the monster is drawn when
        /// that is close to <paramref name="from"/>, so latency never makes it
        /// pop; a far mismatch (missed packets) snaps to the server start.
        /// </summary>
        public void BeginMove(Vector2 from, Vector2 to, float durationSeconds)
        {
            Vector2 shown = transform.position;
            _moveFrom = (shown - from).sqrMagnitude <= 1f ? shown : from;
            _moveTo = to;
            _moveStartedAt = Time.time;
            _moveDuration = Mathf.Max(0.01f, durationSeconds);
            _moving = true;
            if (Data != null)
                Data.Position = to;
        }

        private void Update()
        {
            if (!_moving)
                return;
            float t = (Time.time - _moveStartedAt) / _moveDuration;
            if (t >= 1f)
            {
                _moving = false;
                SetPosition(_moveTo);
                return;
            }
            SetPosition(Vector2.Lerp(_moveFrom, _moveTo, t));
        }

        private void SetPosition(Vector2 position) =>
            transform.position = new Vector3(position.x, position.y, transform.position.z);

        private void OnMouseDown()
        {
            Clicked?.Invoke(this);
        }
    }
}
