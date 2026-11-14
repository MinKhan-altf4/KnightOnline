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
        private readonly MovementPrediction _prediction = new();
        private Vector2 _pendingCorrection;

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
            long sequence = _networkClient.SendPlayerMoveInput(_currentDirection);
            if (_rigidbody != null)
                _prediction.Record(sequence, _rigidbody.position);
        }

        private void FixedUpdate()
        {
            // Dynamic body: set velocity trực tiếp thay vì MovePosition.
            // Linear Drag = 10 đảm bảo player dừng ngay khi thả phím.
            _rigidbody.linearVelocity = _movementEnabled
                ? _currentDirection * MoveSpeed
                : Vector2.zero;

            ApplyPendingCorrection();
            ReconcileAuthoritativePosition();
        }

        private void OnPositionSnapshot(PlayerPositionSnapshotEvent snapshot)
        {
            if (snapshot.ServerSequence <= _lastServerSnapshotSequence)
                return;

            _lastServerSnapshotSequence = snapshot.ServerSequence;
            var serverPosition = new Vector2(
                snapshot.PositionX,
                snapshot.PositionY);

            // Preferred path: compare with where we predicted ourselves when
            // that input was sent, then shift the current position by the
            // error. Normal latency therefore causes no pull-back (B6).
            if (_prediction.TryReconcile(snapshot.AcknowledgedSequence,
                    serverPosition, out Vector2 correction))
            {
                _hasAuthoritativePosition = false;
                if (correction.magnitude <= _positionTolerance)
                    return;
                if (correction.magnitude >= _hardCorrectionDistance)
                {
                    _rigidbody.position += correction;
                    _pendingCorrection = Vector2.zero;
                    return;
                }
                _pendingCorrection += correction;
                return;
            }

            // No matching prediction (e.g. first snapshot after a map change):
            // fall back to steering toward the absolute server position.
            _authoritativePosition = serverPosition;
            _hasAuthoritativePosition = true;
        }

        private void ApplyPendingCorrection()
        {
            if (_pendingCorrection == Vector2.zero || _rigidbody == null)
                return;

            float fraction = Mathf.Clamp01(
                _softCorrectionSpeed * Time.fixedUnscaledDeltaTime);
            Vector2 step = _pendingCorrection * fraction;
            if (_pendingCorrection.sqrMagnitude < 0.0001f)
                step = _pendingCorrection;
            _rigidbody.position += step;
            _pendingCorrection -= step;
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
            // Position was set exactly above; no absolute steering needed (it
            // would pull a player who starts moving before the first ack).
            _authoritativePosition = position;
            _hasAuthoritativePosition = false;
            _hasSentMovement = false;
            _prediction.Clear();
            _pendingCorrection = Vector2.zero;
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
