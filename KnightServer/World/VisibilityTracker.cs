using System.Numerics;

namespace KnightOnline.Server.World;

/// <summary>One player as seen by the world tick.</summary>
public readonly record struct WorldPlayerState(
    int CharacterId,
    string Name,
    int Level,
    string ClassDefinitionId,
    string MapDefinitionId,
    Vector2 Position,
    Vector2 Direction);

public readonly record struct VisibilityDelta(
    IReadOnlyList<WorldPlayerState> Spawned,
    IReadOnlyList<WorldPlayerState> Moved,
    IReadOnlyList<int> Despawned)
{
    public bool IsEmpty =>
        Spawned.Count == 0 && Moved.Count == 0 && Despawned.Count == 0;
}

/// <summary>
/// Per-observer memory of what was last sent, turning "who is visible now"
/// into spawn / move / despawn deltas. Not thread-safe: owned by the tick.
/// </summary>
public sealed class VisibilityTracker
{
    /// <summary>Smaller position changes are not resent.</summary>
    public const float PositionEpsilon = 0.01f;

    private readonly Dictionary<int, (Vector2 Position, Vector2 Direction)> _sent =
        new();

    public int VisibleCount => _sent.Count;

    public bool IsVisible(int characterId) => _sent.ContainsKey(characterId);

    /// <param name="visibleNow">
    /// Every player the observer should see this tick (observer excluded).
    /// </param>
    public VisibilityDelta Update(IReadOnlyList<WorldPlayerState> visibleNow)
    {
        var spawned = new List<WorldPlayerState>();
        var moved = new List<WorldPlayerState>();
        var stillVisible = new HashSet<int>();

        foreach (WorldPlayerState player in visibleNow)
        {
            stillVisible.Add(player.CharacterId);
            if (!_sent.TryGetValue(player.CharacterId, out var last))
            {
                spawned.Add(player);
                _sent[player.CharacterId] = (player.Position, player.Direction);
                continue;
            }

            if (Vector2.DistanceSquared(last.Position, player.Position) >
                    PositionEpsilon * PositionEpsilon ||
                last.Direction != player.Direction)
            {
                moved.Add(player);
                _sent[player.CharacterId] = (player.Position, player.Direction);
            }
        }

        var despawned = new List<int>();
        foreach (int characterId in _sent.Keys)
        {
            if (!stillVisible.Contains(characterId))
                despawned.Add(characterId);
        }
        foreach (int characterId in despawned)
            _sent.Remove(characterId);

        return new VisibilityDelta(spawned, moved, despawned);
    }
}
