using System;

namespace KnightOnline.Client.Core.Events
{
    /// <summary>
    /// Handle trả về từ Subscribe(). Gọi Dispose() để hủy đăng ký.
    /// </summary>
    internal sealed class EventBinding<T> : IDisposable where T : IGameEvent
    {
        private readonly EventBus _bus;
        private readonly Action<T> _handler;
        private bool _disposed;

        public EventBinding(EventBus bus, Action<T> handler)
        {
            _bus = bus;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Unsubscribe(_handler);
        }
    }
}