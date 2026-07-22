using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Input;
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

        private Rigidbody2D _rigidbody;
        private IMovementInputProvider _inputProvider;
        private CharacterData _characterData;
        private Vector2 _currentDirection;

        /// <summary>Ưu tiên MoveSpeed từ CharacterData; fallback về giá trị Inspector.</summary>
        private float MoveSpeed => _characterData?.MoveSpeed ?? _defaultMoveSpeed;

        [Inject]
        public void Construct(IMovementInputProvider inputProvider, CharacterData characterData)
        {
            _inputProvider = inputProvider;
            _characterData = characterData;
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
            if (_inputProvider == null) return;
            _currentDirection = _inputProvider.GetMovementDirection();
        }

        private void FixedUpdate()
        {
            // Dynamic body: set velocity trực tiếp thay vì MovePosition.
            // Linear Drag = 10 đảm bảo player dừng ngay khi thả phím.
            _rigidbody.linearVelocity = _currentDirection * MoveSpeed;
        }
    }
}