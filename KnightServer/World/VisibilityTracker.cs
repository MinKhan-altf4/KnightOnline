using System.Numerics;

namespace KnightOnline.Server.World;

/// <summary>One player as seen by the world tick.</summary>
/// <param name="Direction">
/// Effective movement direction (input direction, or zero when the player is
/// not actually moving, e.g. blocked by a wall). Velocity = Direction * Speed.
/// </param>
public readonly record struct WorldPlayerState(
    int CharacterId,
    string Name,
    int Level,
    string ClassDefinitionId,
    string MapDefinitionId,
    Vector2 Position,
    Vector2 Direction,
    float Speed = 0f);

public readonly record struct VisibilityDelta(
    IReadOnlyList<WorldPlayerState> Spawned,
    IReadOnlyList<WorldPlayerState> Moved,
    IReadOnlyList<int> Despawned)
{
    public bool IsEmpty =>
        Spawned.Count == 0 && Moved.Count == 0 && Despawned.Count == 0;
}

/// <summary>
/// Dead-reckoning rules (protocol 3, World_Simulation_P1_Design §12.4).
/// Clients extrapolate position + direction * speed * elapsed; the server
/// only resends when that extrapolation would be wrong.
/// </summary>
public sealed record DeadReckoningSettings(
    float ErrorThreshold = 0.25f,
    TimeSpan? RefreshInterval = null)
{
    public TimeSpan Refresh => RefreshInterval ?? TimeSpan.FromSeconds(1);
}

/// <summary>
/// Per-observer memory of what was last sent, turning "who is visible now"
/// into spawn / move / despawn deltas. A move is sent only when the direction
/// or speed changed, when the observer's extrapolation of the last sent state
/// drifted beyond the threshold, or (while moving) once per refresh interval
/// to bound accumulated rounding. Not thread-safe: owned by the tick.
/// </summary>
public sealed class VisibilityTracker(DeadReckoningSettings? settings = null)
{
    private readonly DeadReckoningSettings _settings = settings ?? new DeadReckoningSettings();
    private readonly Dictionary<int, SentState> _sent = new();
    private readonly HashSet<int> _seenThisUpdate = new();
    private readonly List<int> _despawnBuffer = new();

    public int VisibleCount => _sent.Count;

    public bool IsVisible(int characterId) => _sent.ContainsKey(characterId);

    /// <param name="visibleNow">
    /// Every player the observer should see this tick (observer excluded).
    /// </param>
    public VisibilityDelta Update(IReadOnlyList<WorldPlayerState> visibleNow,
        DateTime utcNow = default)
    {
        List<WorldPlayerState>? spawned = null;
        List<WorldPlayerState>? moved = null;
        _seenThisUpdate.Clear();

        foreach (WorldPlayerState player in visibleNow)
        {
            _seenThisUpdate.Add(player.CharacterId);
            if (!_sent.TryGetValue(player.CharacterId, out SentState last))
            {
                (spawned ??= []).Add(player);
                _sent[player.CharacterId] = SentState.From(player, utcNow);
                continue;
            }

            if (NeedsMove(last, player, utcNow))
            {
                (moved ??= []).Add(player);
                _sent[player.CharacterId] = SentState.From(player, utcNow);
            }
        }

        _despawnBuffer.Clear();
        foreach (int characterId in _sent.Keys)
        {
            if (!_seenThisUpdate.Contains(characterId))
                _despawnBuffer.Add(characterId);
        }
        foreach (int characterId in _despawnBuffer)
            _sent.Remove(characterId);

        return new VisibilityDelta(
            (IReadOnlyList<WorldPlayerState>?)spawned ?? Array.Empty<WorldPlayerState>(),
            (IReadOnlyList<WorldPlayerState>?)moved ?? Array.Empty<WorldPlayerState>(),
            _despawnBuffer.Count == 0 ? Array.Empty<int>() : _despawnBuffer.ToArray());
    }

    private bool NeedsMove(SentState last, WorldPlayerState now, DateTime utcNow)
    {
        if (last.Direction != now.Direction || last.Speed != now.Speed)
            return true;

        double elapsed = Math.Max(0, (utcNow - last.SentAtUtc).TotalSeconds);
        Vector2 predicted = last.Position + last.Direction * last.Speed * (float)elapsed;
        if (Vector2.DistanceSquared(predicted, now.Position) >
            _settings.ErrorThreshold * _settings.ErrorThreshold)
            return true;

        bool moving = now.Direction != Vector2.Zero && now.Speed > 0f;
        return moving && utcNow - last.SentAtUtc >= _settings.Refresh;
    }

    private readonly record struct SentState(
        Vector2 Position, Vector2 Direction, float Speed, DateTime SentAtUtc)
    {
        public static SentState From(WorldPlayerState player, DateTime utcNow) =>
            new(player.Position, player.Direction, player.Speed, utcNow);
    }
}
