using System;
using Cysharp.Threading.Tasks;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Input;
using KnightOnline.Client.Network;
using UnityEngine;
using VContainer;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Điều khiển di chuyển nhân vật. Dùng Rigidbody2D Dynamic với Gravity Scale = 0
    /// và Linear Drag cao để dừng ngay khi thả phím — không cần lực vật lý thật
    /// cho MMORPG top-down. Dynamic body tự xử lý va chạm với tường/NPC/quái
    /// mà không cần config thêm, đáng tin cậy hơn Kinematic + MovePosition.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        /// <summary>Fallback khi chạy thẳng scene InGame không qua Bootstrap.</summary>
        [SerializeField] private float _defaultMoveSpeed = 4f;
        [SerializeField, Min(0.02f)] private float _movementSyncInterval = 0.1f;
        [SerializeField, Min(0f)] private float _positionTolerance = 0.15f;
        [SerializeField, Min(0.01f)] private float _hardCorrectionDistance = 1f;
        [SerializeField, Min(0f)] private float _softCorrectionSpeed = 8f;

        private Rigidbody2D _rigidbody;
        private IMovementInputProvider _inputProvider;
        private CharacterData _characterData;
        private Vector2 _currentDirection;
        private bool _movementEnabled = true;
        private NetworkClient _networkClient;
        private Vector2 _lastSentDirection;
        private bool _hasSentMovement;
        private float _nextMovementSyncTime;
        private IEventBus _eventBus;
        private IDisposable _positionSnapshotSubscription;
        private IDisposable _mapTransitionSubscription;
        private Vector2 _authoritativePosition;
        private bool _hasAuthoritativePosition;
        private long _lastServerSnapshotSequence;

        /// <summary>Ưu tiên MoveSpeed từ CharacterData; fallback về giá trị Inspector.</summary>
        private float MoveSpeed => _characterData?.MoveSpeed ?? _defaultMoveSpeed;

        [Inject]
        public void Construct(
            IMovementInputProvider inputProvider,
            CharacterData characterData,
            NetworkClient networkClient,
            IEventBus eventBus)
        {
            _inputProvider = inputProvider;
            _characterData = characterData;
            _networkClient = networkClient;
            _eventBus = eventBus;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            if (_characterData != null)
                _rigidbody.position = _characterData.SpawnPosition;

            _positionSnapshotSubscription =
                _eventBus?.Subscribe<PlayerPositionSnapshotEvent>(
                    OnPositionSnapshot);
            _mapTransitionSubscription = _eventBus?.Subscribe<MapTransitionedEvent>(
                OnMapTransitioned);
        }

        private void Update()
        {
            if (!_movementEnabled || _inputProvider == null)
            {
                _currentDirection = Vector2.zero;
                return;
            }

            _currentDirection = _inputProvider.GetMovementDirection();
            SyncMovementInput();
        }

        private void SyncMovementInput()
        {
            if (_networkClient == null)
                return;

            bool changed = !_hasSentMovement ||
                (_currentDirection - _lastSentDirection).sqrMagnitude > 0.0001f;
            if (!changed && Time.unscaledTime < _nextMovementSyncTime)
                return;

            _lastSentDirection = _currentDirection;
            _hasSentMovement = true;
            _nextMovementSyncTime = Time.unscaledTime + _movementSyncInterval;
            _networkClient.SendPlayerMoveInputAsync(_currentDirection).Forget();
        }

        private void FixedUpdate()
        {
            // Dynamic body: set velocity trực tiếp thay vì MovePosition.
            // Linear Drag = 10 đảm bảo player dừng ngay khi thả phím.
            _rigidbody.linearVelocity = _movementEnabled
                ? _currentDirection * MoveSpeed
                : Vector2.zero;

            ReconcileAuthoritativePosition();
        }

        private void OnPositionSnapshot(PlayerPositionSnapshotEvent snapshot)
        {
            if (snapshot.ServerSequence <= _lastServerSnapshotSequence)
                return;

            _lastServerSnapshotSequence = snapshot.ServerSequence;
            _authoritativePosition = new Vector2(
                snapshot.PositionX,
                snapshot.PositionY);
            _hasAuthoritativePosition = true;
        }

        private void ReconcileAuthoritativePosition()
        {
            if (!_hasAuthoritativePosition || _rigidbody == null)
                return;

            Vector2 delta = _authoritativePosition - _rigidbody.position;
            float distance = delta.magnitude;
            if (distance <= _positionTolerance)
                return;

            if (distance >= _hardCorrectionDistance)
            {
                _rigidbody.position = _authoritativePosition;
                return;
            }

            float correction = Mathf.Clamp01(
                _softCorrectionSpeed * Time.fixedUnscaledDeltaTime);
            _rigidbody.position = Vector2.Lerp(
                _rigidbody.position,
                _authoritativePosition,
                correction);
        }

        private void OnMapTransitioned(MapTransitionedEvent value)
        {
            Vector2 position = new(value.X, value.Y);
            _rigidbody.position = position;
            _rigidbody.linearVelocity = Vector2.zero;
            _authoritativePosition = position;
            _hasAuthoritativePosition = true;
            _hasSentMovement = false;
        }

        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;

            if (enabled)
                return;

            _currentDirection = Vector2.zero;

            if (_rigidbody != null)
                _rigidbody.linearVelocity = Vector2.zero;

            SyncMovementInput();
        }

        private void OnDestroy()
        {
            _positionSnapshotSubscription?.Dispose();
            _mapTransitionSubscription?.Dispose();
        }
    }
}
