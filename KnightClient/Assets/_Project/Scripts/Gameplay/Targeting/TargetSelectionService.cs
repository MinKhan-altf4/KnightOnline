using KnightOnline.Client.Core.Events;

namespace KnightOnline.Client.Gameplay.Targeting
{
    public sealed class TargetSelectionService
    {
        private readonly IEventBus _eventBus;

        public TargetSelectionService(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public ITargetable CurrentTarget { get; private set; }

        public void Select(ITargetable target)
        {
            if (target == null)
            {
                Clear();
                return;
            }

            CurrentTarget = target;
            _eventBus.Publish(new TargetSelectedEvent(target));
        }

        public void Clear()
        {
            if (CurrentTarget == null)
                return;

            CurrentTarget = null;
            _eventBus.Publish(new TargetClearedEvent());
        }

        public void ClearIfSelected(ITargetable target)
        {
            if (ReferenceEquals(CurrentTarget, target))
                Clear();
        }
    }
}
