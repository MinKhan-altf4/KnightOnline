using System.Numerics;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;

namespace KnightOnline.Server.World;

/// <summary>
/// Finds a deterministic, collision-free player position after monsters
/// respawn. The server owns the result; Unity physics only presents it.
/// </summary>
public sealed class MonsterRespawnDisplacementResolver(
    MonsterService monsters,
    WorldOptions options)
{
    private const float SeparationEpsilon = 0.002f;
    private readonly float _combinedRadius =
        options.PlayerCollisionRadius + options.MonsterCollisionRadius;
    private readonly int _angularSamples =
        options.RespawnDisplacementAngularSamples;

    public Vector2 Resolve(
        string mapDefinitionId,
        int characterId,
        Vector2 currentPosition)
    {
        MonsterSnapshot[] blockers = monsters.GetSnapshots()
            .Where(monster =>
                monster.IsAlive &&
                string.Equals(
                    monster.MapDefinitionId,
                    mapDefinitionId,
                    StringComparison.Ordinal))
            .ToArray();

        if (IsFree(currentPosition, blockers))
            return currentPosition;

        float safeRadius = _combinedRadius + SeparationEpsilon;
        Vector2? best = null;
        float bestDistanceSquared = float.PositiveInfinity;
        int angularOffset = Math.Abs(characterId) % _angularSamples;

        foreach (MonsterSnapshot blocker in blockers)
        {
            var center = new Vector2(
                blocker.SpawnPosition.X,
                blocker.SpawnPosition.Y);
            for (int sample = 0; sample < _angularSamples; sample++)
            {
                Vector2 candidate = center + Direction(
                    sample + angularOffset,
                    _angularSamples) * safeRadius;
                ConsiderCandidate(
                    candidate,
                    currentPosition,
                    blockers,
                    ref best,
                    ref bestDistanceSquared);
            }
        }

        // Dense spawn groups may cover every individual monster boundary.
        // Search deterministic rings around the original position as a
        // bounded fallback. Content validation can later flag such groups.
        for (int ring = 1; best == null && ring <= blockers.Length + 1; ring++)
        {
            float distance = safeRadius * ring;
            for (int sample = 0; sample < _angularSamples; sample++)
            {
                Vector2 candidate = currentPosition + Direction(
                    sample + angularOffset,
                    _angularSamples) * distance;
                ConsiderCandidate(
                    candidate,
                    currentPosition,
                    blockers,
                    ref best,
                    ref bestDistanceSquared);
            }
        }

        return best ?? currentPosition;
    }

    private void ConsiderCandidate(
        Vector2 candidate,
        Vector2 origin,
        IReadOnlyList<MonsterSnapshot> blockers,
        ref Vector2? best,
        ref float bestDistanceSquared)
    {
        if (!IsFree(candidate, blockers))
            return;

        float distanceSquared = Vector2.DistanceSquared(origin, candidate);
        if (distanceSquared >= bestDistanceSquared)
            return;

        best = candidate;
        bestDistanceSquared = distanceSquared;
    }

    private bool IsFree(
        Vector2 candidate,
        IReadOnlyList<MonsterSnapshot> blockers)
    {
        float radiusSquared = _combinedRadius * _combinedRadius;
        foreach (MonsterSnapshot blocker in blockers)
        {
            var center = new Vector2(
                blocker.SpawnPosition.X,
                blocker.SpawnPosition.Y);
            if (Vector2.DistanceSquared(candidate, center) < radiusSquared)
                return false;
        }

        return true;
    }

    private static Vector2 Direction(int sample, int sampleCount)
    {
        float angle = MathF.Tau * (sample % sampleCount) / sampleCount;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}
