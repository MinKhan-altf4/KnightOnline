using System;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Shows a <see cref="DirectionalCharacterSprites"/> set on a SpriteRenderer
    /// and turns it toward the current movement direction (keeps the last
    /// facing when standing still). Purely visual.
    /// </summary>
    public sealed class DirectionalSpriteView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private DirectionalCharacterSprites _sprites;
        private Func<Vector2> _directionSource;
        private int _facing = DirectionalCharacterSprites.South;

        public void Initialize(SpriteRenderer target, DirectionalCharacterSprites sprites,
            Func<Vector2> directionSource)
        {
            _renderer = target;
            _sprites = sprites;
            _directionSource = directionSource;
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

            int facing = DirectionalCharacterSprites.DirectionIndex(_directionSource(), _facing);
            if (facing == _facing && _renderer.sprite != null)
                return;
            _facing = facing;
            _renderer.sprite = _sprites.Get(_facing);
        }
    }
}
