using UnityEngine;
using VContainer;
using KnightOnline.Client.Input;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Điều khiển di chuyển nhân vật. Dùng Rigidbody2D Kinematic +
    /// MovePosition trong FixedUpdate - không dùng lực vật lý thật (không
    /// cần trọng lực/bật nảy cho MMORPG top-down), nhưng vẫn có Collider2D
    /// để va chạm đúng với tường/NPC/quái sau này.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 4f;

        private Rigidbody2D _rigidbody;
        private IMovementInputProvider _inputProvider;
        private Vector2 _currentDirection;

        [Inject]
        public void Construct(IMovementInputProvider inputProvider)
        {
            _inputProvider = inputProvider;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            // Đọc input ở Update (đúng tần suất input thật), lưu lại để
            // FixedUpdate dùng - tách biệt tần suất đọc input và tần suất
            // physics update, tránh input bị "ăn mất" giữa các fixed tick.
            _currentDirection = _inputProvider.GetMovementDirection();
        }

        private void FixedUpdate()
        {
            Vector2 targetPosition = _rigidbody.position + _currentDirection * _moveSpeed * Time.fixedDeltaTime;
            _rigidbody.MovePosition(targetPosition);
        }
    }
}