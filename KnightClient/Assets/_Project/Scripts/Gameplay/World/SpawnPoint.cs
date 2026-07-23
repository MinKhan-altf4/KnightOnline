using UnityEngine;

namespace KnightOnline.Client.Gameplay.World
{
    /// <summary>
    /// Marker đánh dấu vị trí Player xuất hiện khi vào scene InGame.
    /// Không chứa logic - chỉ là 1 Transform có thể tìm bằng tag/tên,
    /// đọc bởi InGameLifetimeScope trước khi Player được khởi tạo.
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            // Vẽ marker màu xanh lá trong Scene view để dễ nhận biết vị trí
            // spawn khi thiết kế map - không ảnh hưởng gì lúc Play thật.
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}