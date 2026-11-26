using System.Collections.Generic;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Client prediction copy of the server rule in
    /// KnightServer/World/MonsterCollisionMovementResolver.cs: a move stops
    /// just before it would enter any obstacle circle (no sliding), and a
    /// player already overlapping may always move away. The server result is
    /// authoritative; this only keeps the prediction equal to it so touching a
    /// monster does not cause a correction (the "one-beat freeze").
    /// Keep both files in sync.
    /// </summary>
    public static class CircleObstacleResolver
    {
        private const float ContactEpsilon = 0.001f;

        public static Vector2 Resolve(Vector2 start, Vector2 desiredEnd,
            IReadOnlyList<Vector2> obstacleCenters, float combinedRadius)
        {
            Vector2 movement = desiredEnd - start;
            float movementLengthSquared = movement.sqrMagnitude;
            if (movementLengthSquared <= float.Epsilon || obstacleCenters == null ||
                obstacleCenters.Count == 0)
            {
                return desiredEnd;
            }

            float radiusSquared = combinedRadius * combinedRadius;
            float earliestContact = 1f;
            for (int i = 0; i < obstacleCenters.Count; i++)
            {
                Vector2 fromCenter = start - obstacleCenters[i];
                float distanceSquared = fromCenter.sqrMagnitude;

                // Overlapping (e.g. a monster respawned on the player): allow
                // any move that does not go further in.
                if (distanceSquared < radiusSquared &&
                    Vector2.Dot(movement, fromCenter) >= 0f)
                {
                    continue;
                }

                float a = movementLengthSquared;
                float b = 2f * Vector2.Dot(fromCenter, movement);
                float c = distanceSquared - radiusSquared;
                float discriminant = b * b - 4f * a * c;
                if (discriminant < 0f)
                    continue;

                float contact = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
                if (contact >= 0f && contact <= 1f)
                    earliestContact = Mathf.Min(earliestContact, contact);
            }

            if (earliestContact >= 1f)
                return desiredEnd;

            float movementLength = Mathf.Sqrt(movementLengthSquared);
            float safeContact = Mathf.Max(0f,
                earliestContact - ContactEpsilon / movementLength);
            return start + movement * safeContact;
        }
    }

    /// <summary>Live centres of things that block player movement (alive monsters).</summary>
    public interface IMovementObstacleSource
    {
        /// <summary>Clears <paramref name="centers"/> and fills it with the current obstacles.</summary>
        void CollectObstacles(List<Vector2> centers);
    }
}
