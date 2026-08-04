using UnityEngine;

namespace KnightOnline.Client.Gameplay.World
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _offset;

        public void Initialize(Transform target)
        {
            _target = target;
            _offset = new Vector3(0f, 0f, transform.position.z);
            Snap();
        }

        private void LateUpdate() => Snap();

        private void Snap()
        {
            if (_target == null) return;
            transform.position = new Vector3(
                _target.position.x,
                _target.position.y,
                _offset.z);
        }
    }
}
