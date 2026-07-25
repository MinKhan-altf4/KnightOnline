using Cysharp.Threading.Tasks;
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

        private Rigidbody2D _rigidbody;
        private IMovementInputProvider _inputProvider;
        private CharacterData _characterData;
        private Vector2 _currentDirection;
        private bool _movementEnabled = true;
        private NetworkClient _networkClient;
        private Vector2 _lastSentDirection;
        private bool _hasSentMovement;
        private float _nextMovementSyncTime;

        /// <summary>Ưu tiên MoveSpeed từ CharacterData; fallback về giá trị Inspector.</summary>
        private float MoveSpeed => _characterData?.MoveSpeed ?? _defaultMoveSpeed;

        [Inject]
        public void Construct(
            IMovementInputProvider inputProvider,
            CharacterData characterData,
            NetworkClient networkClient)
        {
            _inputProvider = inputProvider;
            _characterData = characterData;
            _networkClient = networkClient;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            if (_characterData != null)
                _rigidbody.position = _characterData.SpawnPosition;
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
    }
}
