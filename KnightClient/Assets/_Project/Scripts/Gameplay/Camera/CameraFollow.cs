using UnityEngine;

namespace KnightOnline.Client.Gameplay.CameraControl
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform _target;
        [Min(0.01f)]
        [SerializeField] private float _smoothTime = 0.18f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        [Header("Map Boundaries")]
        [SerializeField] private bool _useBounds = true;
        [SerializeField] private Vector2 _minBounds; 
        [SerializeField] private Vector2 _maxBounds; 

        private Camera _cam;
        private Vector3 _followVelocity;
        private bool _snapOnNextUpdate = true;

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

            Vector3 desiredPosition = GetDesiredPosition();

            // LateUpdate đầu tiên chạy sau Start của PlayerController, nên vị trí
            // spawn đã được áp dụng và camera không trượt từ vị trí scene cũ.
            if (_snapOnNextUpdate)
            {
                transform.position = desiredPosition;
                _followVelocity = Vector3.zero;
                _snapOnNextUpdate = false;
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _followVelocity,
                _smoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        private Vector3 GetDesiredPosition()
        {
            Vector3 desiredPosition = _target.position + _offset;

            if (!_useBounds || _cam == null || !_cam.orthographic)
                return desiredPosition;

            float halfHeight = _cam.orthographicSize;
            float halfWidth = halfHeight * _cam.aspect;

            desiredPosition.x = ClampAxisOrCenter(
                desiredPosition.x,
                _minBounds.x,
                _maxBounds.x,
                halfWidth);

            desiredPosition.y = ClampAxisOrCenter(
                desiredPosition.y,
                _minBounds.y,
                _maxBounds.y,
                halfHeight);

            return desiredPosition;
        }

        private static float ClampAxisOrCenter(
            float targetPosition,
            float minimumBound,
            float maximumBound,
            float cameraHalfSize)
        {
            float minimumCameraPosition = minimumBound + cameraHalfSize;
            float maximumCameraPosition = maximumBound - cameraHalfSize;

            // Viewport lớn hơn map: giữ camera giữa map thay vì Clamp với
            // min > max, vốn tạo ra vị trí sai hoặc rung ở biên.
            if (minimumCameraPosition > maximumCameraPosition)
                return (minimumBound + maximumBound) * 0.5f;

            return Mathf.Clamp(
                targetPosition,
                minimumCameraPosition,
                maximumCameraPosition);
        }

        private void OnValidate()
        {
            _smoothTime = Mathf.Max(0.01f, _smoothTime);
        }
    }
}
