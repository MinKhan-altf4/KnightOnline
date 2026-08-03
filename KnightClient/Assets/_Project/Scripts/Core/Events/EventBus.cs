using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Core.Events
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private readonly Dictionary<Type, IGameEvent> _stickyValues = new();

        public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var eventType = typeof(T);

            if (!_handlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new List<Delegate>();
                _handlers[eventType] = handlerList;
            }

            handlerList.Add(handler);

            // Nếu đây là sticky event và đã có giá trị được publish trước đó,
            // gọi ngay handler với giá trị gần nhất - giải quyết race condition
            // giữa thời điểm publish và thời điểm subscribe.
            if (_stickyValues.TryGetValue(eventType, out var cachedValue))
            {
                handler.Invoke((T)cachedValue);
            }

            return new EventBinding<T>(this, handler);
        }

        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            var eventType = typeof(T);

            if (gameEvent is IStickyGameEvent)
            {
                _stickyValues[eventType] = gameEvent;
            }

            if (!_handlers.TryGetValue(eventType, out var handlerList))
                return;

            var snapshot = handlerList.ToArray();

            foreach (var del in snapshot)
            {
                // A previous handler can synchronously unload a scene. Its
                // MonoBehaviours unsubscribe from OnDestroy, but their
                // delegates are still present in this publish snapshot.
                // Never invoke a binding that is no longer live.
                if (!handlerList.Contains(del))
                    continue;

                // Unity keeps the managed delegate target alive briefly after
                // Destroy, while its overloaded null comparison already marks
                // the native object as gone. Remove that stale binding before
                // invoking it.
                if (del.Target is UnityEngine.Object unityTarget &&
                    unityTarget == null)
                {
                    handlerList.Remove(del);
                    continue;
                }

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
