using System;

namespace KnightOnline.Client.Core.Events
{
    public interface IEventBus
    {
        /// <summary>
        /// Đăng ký lắng nghe 1 loại event. Trả về IDisposable —
        /// BẮT BUỘC gọi Dispose() khi không cần lắng nghe nữa
        /// (thường trong OnDestroy) để tránh memory leak.
        /// </summary>
        IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent;

        /// <summary>
        /// Phát event tới mọi subscriber đang lắng nghe kiểu T.
        /// Phải gọi từ main thread nếu subscriber đụng tới Unity API.
        /// </summary>
        void Publish<T>(T gameEvent) where T : IGameEvent;
    }
}