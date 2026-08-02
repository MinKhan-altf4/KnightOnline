using System.Numerics;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;

namespace KnightOnline.Server.World;

public sealed class MonsterCollisionMovementResolver(
    MonsterService monsters,
    WorldOptions options) : IWorldMovementResolver
{
    private const float ContactEpsilon = 0.001f;
    private readonly float _combinedRadius =
        options.PlayerCollisionRadius + options.MonsterCollisionRadius;

    public Vector2 Resolve(Vector2 start, Vector2 desiredEnd)
    {
        Vector2 movement = desiredEnd - start;
        float movementLengthSquared = movement.LengthSquared();
        if (movementLengthSquared <= float.Epsilon)
            return start;

        float earliestContact = 1f;
        foreach (MonsterSnapshot monster in monsters.GetSnapshots())
        {
            if (!monster.IsAlive)
                continue;

            var center = new Vector2(
                monster.SpawnPosition.X,
                monster.SpawnPosition.Y);
            Vector2 fromCenter = start - center;
            float radiusSquared = _combinedRadius * _combinedRadius;

            // A monster may respawn on top of a player. Do not trap the
            // player inside the overlap; allow movement that increases the
            // distance from the monster.
            if (fromCenter.LengthSquared() < radiusSquared &&
                Vector2.Dot(movement, fromCenter) >= 0)
            {
                continue;
            }

            float a = movementLengthSquared;
            float b = 2f * Vector2.Dot(fromCenter, movement);
            float c = fromCenter.LengthSquared() - radiusSquared;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0)
                continue;

            float contact =
                (-b - MathF.Sqrt(discriminant)) / (2f * a);
            if (contact is >= 0f and <= 1f)
                earliestContact = MathF.Min(earliestContact, contact);
        }

        if (earliestContact >= 1f)
            return desiredEnd;

        float movementLength = MathF.Sqrt(movementLengthSquared);
        float safeContact = MathF.Max(
            0f,
            earliestContact - ContactEpsilon / movementLength);
        return start + movement * safeContact;
    }
}
