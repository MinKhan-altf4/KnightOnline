using System.Threading;
using Cysharp.Threading.Tasks;
using KnightOnline.Client.Network;
using UnityEngine;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameBootstrap : IAsyncStartable
    {
        private readonly NetworkClient _networkClient;

        // VContainer tự inject instance NetworkClient đã đăng ký ở GameLifetimeScope
        public GameBootstrap(NetworkClient networkClient)
        {
            _networkClient = networkClient;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            Debug.Log("<color=green>[Bootstrap]</color> Game đang khởi động bằng VContainer & UniTask...");

            await _networkClient.ConnectAsync();
        }
    }
}