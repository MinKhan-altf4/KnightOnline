using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameBootstrap : IAsyncStartable
    {
        // Hàm này sẽ tự động được VContainer gọi khi game bắt đầu
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            Debug.Log("<color=green>[Bootstrap]</color> Game đang khởi động bằng VContainer & UniTask...");

            // Giả lập load dữ liệu tài nguyên, kết nối server mất 1 giây
            await UniTask.Delay(1000, cancellationToken: cancellation);

            Debug.Log("<color=green>[Bootstrap]</color> Khởi tạo thành công! Sẵn sàng chuyển sang màn hình Login.");
            
            // Sau này ở đây chúng ta sẽ gọi Service chuyển Scene
        }
    }
}