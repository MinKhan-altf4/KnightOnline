using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameBootstrap : IAsyncStartable
    {
        public async UniTask StartAsync(System.Threading.CancellationToken cancellation)
        {
            await UniTask.Delay(1000, cancellationToken: cancellation);
            Debug.Log("<color=yellow>[Bootstrap]</color> Khởi tạo game hoàn tất!");
        }
    }
}