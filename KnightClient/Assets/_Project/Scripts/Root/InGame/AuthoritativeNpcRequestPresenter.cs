using System;
using Cysharp.Threading.Tasks;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Gameplay.NPC;
using KnightOnline.Client.Network;
using VContainer.Unity;

namespace KnightOnline.Client.Core.Bootstrap
{
    public sealed class AuthoritativeNpcRequestPresenter : IStartable,
        IDisposable
    {
        private readonly IEventBus _events;
        private readonly NetworkClient _network;
        private IDisposable _subscription;

        public AuthoritativeNpcRequestPresenter(IEventBus events,
            NetworkClient network)
        {
            _events = events;
            _network = network;
        }

        public void Start() => _subscription =
            _events.Subscribe<NpcActionRequestedEvent>(OnRequested);

        private void OnRequested(NpcActionRequestedEvent value)
        {
            if (value.Action != NpcActionType.Quest ||
                string.IsNullOrWhiteSpace(value.NpcDefinitionId))
                return;
            _network.SendInteractNpcRequestAsync(value.NpcDefinitionId).Forget();
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
