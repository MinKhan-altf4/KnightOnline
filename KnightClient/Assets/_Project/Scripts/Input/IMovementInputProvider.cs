using UnityEngine;

namespace KnightOnline.Client.Input
{
    /// <summary>
    /// Trừu tượng hóa nguồn input di chuyển - Gameplay không cần biết
    /// hướng đi đến từ bàn phím, chuột, hay D-Pad ảo trên mobile.
    /// Trả về Vector2 đã chuẩn hóa (magnitude tối đa = 1).
    /// </summary>
    public interface IMovementInputProvider
    {
        Vector2 GetMovementDirection();
    }
}