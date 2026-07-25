using KnightOnline.Client.Core.Events;

namespace KnightOnline.Client.Gameplay.Targeting
{
    public readonly struct TargetSelectedEvent : IGameEvent
    {
        public TargetSelectedEvent(ITargetable target)
        {
            Target = target;
        }

        public ITargetable Target { get; }
    }

    public readonly struct TargetClearedEvent : IGameEvent { }
}
