using System.Threading;
using Cysharp.Threading.Tasks;
using KnightOnline.Client.Network; // Bổ sung thư viện Network
using UnityEngine;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    public class GameBootstrap : IAsyncStartable
    {
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            Debug.Log("<color=green>[Bootstrap]</color> Game đang khởi động bằng VContainer & UniTask...");

            // Tạo GameObject để chứa NetworkClient component
            var networkClientGO = new GameObject("NetworkClient");
            var networkClient = networkClientGO.AddComponent<NetworkClient>();
            
            // Kết nối tới server
            await networkClient.ConnectAsync();
        }
    }
}