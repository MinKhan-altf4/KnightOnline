using System;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Plays a <see cref="DirectionalCharacterSprites"/> set on a SpriteRenderer:
    /// "walking" (looping) while the direction source reports movement,
    /// "standing" otherwise, always facing the last movement direction.
    ///
    /// When the character has no walking frames yet, a procedural step bob
    /// (whole-pixel hop per step) stands in for them; real walking frames
    /// replace it automatically. Code-driven on purpose: 8 directions x N
    /// moves would need dozens of Animator clips. Purely visual.
    /// </summary>
    public sealed class DirectionalSpriteView : MonoBehaviour
    {
        /// <summary>Default frame time for moves (PixelDoll default 100 ms).</summary>
        public const float DefaultFrameSeconds = 0.1f;

        /// <summary>One step (one hop) of the placeholder bob.</summary>
        public const float BobStepSeconds = 0.3f;

        /// <summary>Hop height of the placeholder bob, in source pixels.</summary>
        public const int BobHeightPixels = 3;

        private SpriteRenderer _renderer;
        private Transform _visual;
        private Vector3 _visualRestPosition;
        private DirectionalCharacterSprites _sprites;
        private Func<Vector2> _directionSource;
        private int _facing = DirectionalCharacterSprites.South;
        private string _move = DirectionalCharacterSprites.Standing;
        private bool _moving;
        private float _moveStartedAt;
        private float _frameSeconds = DefaultFrameSeconds;

        /// <param name="target">
        /// Renderer to drive. For the bob to be safe it must sit on its own
        /// child object (moving it must not move physics or the name label).
        /// </param>
        public void Initialize(SpriteRenderer target, DirectionalCharacterSprites sprites,
            Func<Vector2> directionSource, float frameSeconds = DefaultFrameSeconds)
        {
            _renderer = target;
            _sprites = sprites;
            _directionSource = directionSource;
            _frameSeconds = Mathf.Max(0.01f, frameSeconds);
            _visual = target != null && target.transform != transform ? target.transform : null;
            if (_visual != null)
                _visualRestPosition = _visual.localPosition;
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
            if (move != _move || moving != _moving)
            {
                _move = move;
                _moving = moving;
                _moveStartedAt = Time.time; // start on the first frame / first hop
            }

            float elapsed = Time.time - _moveStartedAt;
            Sprite[] frames = _sprites.Frames(_move, _facing);
            Sprite sprite = frames[DirectionalCharacterSprites.FrameAt(
                elapsed, frames.Length, _frameSeconds)];
            if (_renderer.sprite != sprite)
                _renderer.sprite = sprite;

            // Bob stands in for missing walk frames: no walking move at all,
            // or none drawn yet for this direction (e.g. side views).
            ApplyBob(moving && !_sprites.HasOwnFrames(DirectionalCharacterSprites.Walking, _facing),
                elapsed);
        }

        private void ApplyBob(bool active, float elapsed)
        {
            if (_visual == null)
                return;

            int pixels = active ? BobOffsetPixels(elapsed, BobStepSeconds, BobHeightPixels) : 0;
            _visual.localPosition = _visualRestPosition +
                new Vector3(0f, pixels / DirectionalCharacterSprites.PixelsPerUnit, 0f);
        }

        /// <summary>
        /// Placeholder step bob: one hop per step (|sin|), rounded to whole
        /// pixels so pixel art stays crisp. Starts on the ground.
        /// </summary>
        public static int BobOffsetPixels(double elapsedSeconds, float stepSeconds, int heightPixels)
        {
            if (elapsedSeconds <= 0 || stepSeconds <= 0f || heightPixels <= 0)
                return 0;
            double hop = Math.Abs(Math.Sin(Math.PI * elapsedSeconds / stepSeconds));
            return (int)Math.Round(hop * heightPixels, MidpointRounding.AwayFromZero);
        }

        private void OnDisable()
        {
            if (_visual != null)
                _visual.localPosition = _visualRestPosition;
        }
    }
}
