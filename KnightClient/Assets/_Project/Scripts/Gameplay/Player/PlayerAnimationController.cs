using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer), typeof(Rigidbody2D))]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Rigidbody2D _rb;

        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            // Dùng linearVelocity chuẩn theo API của Unity 6
            Vector2 velocity = _rb.linearVelocity;

            bool isWalking = velocity.sqrMagnitude > 0.01f;
            _animator.SetBool(IsWalkingHash, isWalking);

            if (velocity.x < -0.01f)
            {
                _spriteRenderer.flipX = true;
            }
            else if (velocity.x > 0.01f)
            {
                _spriteRenderer.flipX = false;
            }
        }
    }
}