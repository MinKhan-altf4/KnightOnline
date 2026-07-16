using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Core.Events
{
    /// <summary>
    /// Cài đặt mặc định của IEventBus. Đăng ký qua VContainer làm Singleton,
    /// KHÔNG dùng static instance.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var eventType = typeof(T);

            if (!_handlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new List<Delegate>();
                _handlers[eventType] = handlerList;
            }

            handlerList.Add(handler);

            return new EventBinding<T>(this, handler);
        }

        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            var eventType = typeof(T);

            if (!_handlers.TryGetValue(eventType, out var handlerList))
                return;

            var snapshot = handlerList.ToArray();

            foreach (var del in snapshot)
            {
                if (del is Action<T> typedHandler)
                {
                    typedHandler.Invoke(gameEvent);
                }
            }
        }

        internal void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var eventType = typeof(T);

            if (_handlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList.Remove(handler);
            }
        }
    }
}