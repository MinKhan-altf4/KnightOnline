using UnityEngine;

namespace KnightOnline.Client.Gameplay.Targeting
{
    public interface ITargetable
    {
        int TargetId { get; }
        TargetType TargetType { get; }
        string DisplayName { get; }
        int Level { get; }
        int CurrentHealth { get; }
        int MaximumHealth { get; }
        bool ShowsHealth { get; }
        Transform MarkerAnchor { get; }
    }
}
