using UnityEngine;

namespace KnightOnline.Client.Gameplay.CameraControl
{
    /// <summary>
    /// Camera bám theo Player mượt mà bằng Lerp, giữ khoảng cách Z cố định
    /// (view 2D top-down). Không dùng DI - đây là behavior thuần Unity,
    /// tham chiếu trực tiếp tới Player qua Inspector cho đơn giản ở giai
    /// đoạn này (không cần EventBus/Service cho việc này).
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _smoothSpeed = 5f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 desiredPosition = _target.position + _offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed * Time.deltaTime);
        }
    }
}