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
        /// <summary>
        /// Player + monster collision radius. Mirrors serverSettings.json
        /// world.playerCollisionRadius (0.35) + world.monsterCollisionRadius (0.5).
        /// </summary>
        [SerializeField, Min(0f)] private float _monsterBlockRadius = 0.85f;

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
        private IDisposable _boundsSubscription;
        private WalkableBounds? _bounds;
        private IMovementObstacleSource _obstacles;
        private readonly System.Collections.Generic.List<Vector2> _obstacleCenters = new();

        /// <summary>Hướng input hiện tại (dùng cho hiển thị hướng nhân vật).</summary>
        public Vector2 MoveDirection => _currentDirection;

        /// <summary>Ưu tiên MoveSpeed từ CharacterData; fallback về giá trị Inspector.</summary>
        private float MoveSpeed => _characterData?.MoveSpeed ?? _defaultMoveSpeed;

        [Inject]
        public void Construct(
            IMovementInputProvider inputProvider,
            CharacterData characterData,
            NetworkClient networkClient,
            IEventBus eventBus,
            IMovementObstacleSource obstacles)
        {
            _obstacles = obstacles;
            _inputProvider = inputProvider;
            _characterData = characterData;
            _networkClient = networkClient;
            _eventBus = eventBus;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            // Velocity is set explicitly every physics step (zero when no
            // input), so damping is not needed to stop. Box2D applies damping
            // before integrating: with 10 at 50 Hz the body moved at only
            // ~83 % of MoveSpeed, drifted behind the server and got pulled
            // forward by a correction on almost every snapshot.
            _rigidbody.linearDamping = 0f;
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
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
            // The portal list carries the authoritative map bounds.
            _boundsSubscription = _eventBus?.Subscribe<PortalListReceivedEvent>(
                value => _bounds = new WalkableBounds(
                    new Vector2(value.MinimumX, value.MinimumY),
                    new Vector2(value.MaximumX, value.MaximumY)));
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
            Vector2 velocity = _movementEnabled
                ? _currentDirection * MoveSpeed
                : Vector2.zero;

            // Small corrections ride on the velocity so interpolation keeps
            // working; only hard corrections teleport the body.
            velocity += PendingCorrectionVelocity();
            velocity += AuthoritativeSteeringVelocity();
            velocity = BlockedByMonsters(velocity);

            // Same edge rule as the server: clamp the center and never step
            // past it, sliding along the edge instead of pushing into it.
            if (_bounds is WalkableBounds bounds)
            {
                // Writing Rigidbody2D.position is a teleport that resets
                // interpolation; doing it every step made the hero (and the
                // camera locked to it) move in 50 Hz jumps. Only write when
                // the body really is outside.
                Vector2 current = _rigidbody.position;
                Vector2 clamped = bounds.Clamp(current);
                if (clamped != current)
                    _rigidbody.position = clamped;
                velocity = bounds.LimitVelocity(
                    _rigidbody.position, velocity, Time.fixedDeltaTime);
            }
            _rigidbody.linearVelocity = velocity;
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

        /// <summary>
        /// Velocity that covers this step's share of the pending prediction
        /// error (same exponential rate as before, applied by physics).
        /// </summary>
        private Vector2 PendingCorrectionVelocity()
        {
            if (_pendingCorrection == Vector2.zero || _rigidbody == null)
                return Vector2.zero;

            float deltaTime = Time.fixedDeltaTime;
            float fraction = Mathf.Clamp01(_softCorrectionSpeed * deltaTime);
            Vector2 step = _pendingCorrection * fraction;
            if (_pendingCorrection.sqrMagnitude < 0.0001f)
                step = _pendingCorrection;
            _pendingCorrection -= step;
            return step / deltaTime;
        }

        /// <summary>
        /// Shortens this step's move with the server's monster rule so the
        /// predicted position stops exactly where the server will stop it.
        /// </summary>
        private Vector2 BlockedByMonsters(Vector2 velocity)
        {
            if (_obstacles == null || velocity == Vector2.zero || _rigidbody == null)
                return velocity;

            _obstacles.CollectObstacles(_obstacleCenters);
            if (_obstacleCenters.Count == 0)
                return velocity;

            float deltaTime = Time.fixedDeltaTime;
            Vector2 start = _rigidbody.position;
            Vector2 end = CircleObstacleResolver.Resolve(start, start + velocity * deltaTime,
                _obstacleCenters, _monsterBlockRadius);
            return (end - start) / deltaTime;
        }

        private Vector2 AuthoritativeSteeringVelocity()
        {
            if (!_hasAuthoritativePosition || _rigidbody == null)
                return Vector2.zero;

            Vector2 delta = _authoritativePosition - _rigidbody.position;
            float distance = delta.magnitude;
            if (distance <= _positionTolerance)
                return Vector2.zero;

            if (distance >= _hardCorrectionDistance)
            {
                _rigidbody.position = _authoritativePosition;
                return Vector2.zero;
            }

            float deltaTime = Time.fixedDeltaTime;
            float fraction = Mathf.Clamp01(_softCorrectionSpeed * deltaTime);
            return delta * fraction / deltaTime;
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
            _bounds = null; // new map: wait for its portal list
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
            _boundsSubscription?.Dispose();
        }
    }
}
