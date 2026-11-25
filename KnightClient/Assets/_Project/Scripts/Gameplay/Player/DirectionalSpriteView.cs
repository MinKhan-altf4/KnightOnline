using System;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Plays a <see cref="DirectionalCharacterSprites"/> set on a SpriteRenderer:
    /// "walking" (looping) while the direction source reports movement,
    /// "standing" otherwise, always facing the last movement direction.
    /// Code-driven on purpose: 8 directions x N moves would need dozens of
    /// Animator clips. Purely visual.
    /// </summary>
    public sealed class DirectionalSpriteView : MonoBehaviour
    {
        /// <summary>Default frame time for moves (PixelDoll default 100 ms).</summary>
        public const float DefaultFrameSeconds = 0.1f;

        private SpriteRenderer _renderer;
        private DirectionalCharacterSprites _sprites;
        private Func<Vector2> _directionSource;
        private int _facing = DirectionalCharacterSprites.South;
        private string _move = DirectionalCharacterSprites.Standing;
        private float _moveStartedAt;
        private float _frameSeconds = DefaultFrameSeconds;

        public void Initialize(SpriteRenderer target, DirectionalCharacterSprites sprites,
            Func<Vector2> directionSource, float frameSeconds = DefaultFrameSeconds)
        {
            _renderer = target;
            _sprites = sprites;
            _directionSource = directionSource;
            _frameSeconds = Mathf.Max(0.01f, frameSeconds);
            if (_renderer != null && _sprites != null)
            {
                _renderer.color = Color.white;
                _renderer.sprite = _sprites.Get(_facing);
            }
        }

        private void LateUpdate()
        {
            if (_renderer == null || _sprites == null || _directionSource == null)
                return;

            Vector2 direction = _directionSource();
            bool moving = direction.sqrMagnitude >= 0.0001f;
            _facing = DirectionalCharacterSprites.DirectionIndex(direction, _facing);

            string move = moving && _sprites.HasMove(DirectionalCharacterSprites.Walking)
                ? DirectionalCharacterSprites.Walking
                : DirectionalCharacterSprites.Standing;
            if (move != _move)
            {
                _move = move;
                _moveStartedAt = Time.time; // start a new move on its first frame
            }

            Sprite[] frames = _sprites.Frames(_move, _facing);
            int frame = DirectionalCharacterSprites.FrameAt(
                Time.time - _moveStartedAt, frames.Length, _frameSeconds);
            Sprite sprite = frames[frame];
            if (_renderer.sprite != sprite)
                _renderer.sprite = sprite;
        }
    }
}
