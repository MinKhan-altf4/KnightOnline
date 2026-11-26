using UnityEngine;

namespace KnightOnline.Client.Gameplay.World.Environment
{
    /// <summary>Loops a few sprites on a renderer (campfire flames and similar ambient props).</summary>
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _framesPerSecond;
        private float _offset;

        public void Initialize(SpriteRenderer target, Sprite[] frames, float framesPerSecond)
        {
            _renderer = target;
            _frames = frames;
            _framesPerSecond = Mathf.Max(0.1f, framesPerSecond);
            // Desynchronise neighbouring fires.
            _offset = Random.value * 10f;
        }

        private void Update()
        {
            if (_renderer == null || _frames == null || _frames.Length == 0) return;
            int index = (int)((Time.time + _offset) * _framesPerSecond) % _frames.Length;
            Sprite frame = _frames[index];
            if (frame != null)
                _renderer.sprite = frame;
        }
    }
}
