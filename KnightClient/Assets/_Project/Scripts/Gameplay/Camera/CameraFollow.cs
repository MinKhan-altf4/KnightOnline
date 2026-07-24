using UnityEngine;

namespace KnightOnline.Client.Gameplay.CameraControl
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _smoothSpeed = 5f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        [Header("Map Boundaries")]
        [SerializeField] private bool _useBounds = true;
        [SerializeField] private Vector2 _minBounds; 
        [SerializeField] private Vector2 _maxBounds; 

        private Camera _cam;

        private void Start()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null)
            {
                _cam = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // 1. Tính toán vị trí mục tiêu mong muốn theo Player
            Vector3 desiredPosition = _target.position + _offset;

            // 2. Ép biên (Clamp) trực tiếp trên mục tiêu trước khi Lerp
            if (_useBounds && _cam != null && _cam.orthographic)
            {
                float halfHeight = _cam.orthographicSize;
                float halfWidth = halfHeight * _cam.aspect;

                float clampedX = Mathf.Clamp(desiredPosition.x, _minBounds.x + halfWidth, _maxBounds.x - halfWidth);
                float clampedY = Mathf.Clamp(desiredPosition.y, _minBounds.y + halfHeight, _maxBounds.y - halfHeight);
                
                desiredPosition = new Vector3(clampedX, clampedY, desiredPosition.z);
            }

            // 3. Trượt mượt mà đến vị trí đã được giới hạn biên an toàn
            transform.position = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed * Time.deltaTime);
        }
    }
}