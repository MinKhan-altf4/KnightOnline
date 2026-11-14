using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Client copy of the server's map rule: the player's <b>center</b> is
    /// clamped to [Minimum, Maximum] (MapCatalog.ClampPosition). Predicting
    /// with exactly the same rule keeps client and server positions equal at
    /// the map edge; a physics collider at the edge stopped the 1x1 player
    /// box half a unit earlier, so every server ack pulled the player into the
    /// collider and physics pushed it back (wall jitter).
    /// </summary>
    public readonly struct WalkableBounds
    {
        public WalkableBounds(Vector2 minimum, Vector2 maximum)
        {
            Minimum = Vector2.Min(minimum, maximum);
            Maximum = Vector2.Max(minimum, maximum);
        }

        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }

        public Vector2 Clamp(Vector2 position) =>
            new(Mathf.Clamp(position.x, Minimum.x, Maximum.x),
                Mathf.Clamp(position.y, Minimum.y, Maximum.y));

        /// <summary>
        /// Limits a velocity so one physics step of <paramref name="deltaTime"/>
        /// cannot leave the bounds; movement along the edge is kept (sliding).
        /// </summary>
        public Vector2 LimitVelocity(Vector2 position, Vector2 velocity, float deltaTime)
        {
            if (deltaTime <= 0f)
                return velocity;
            return new Vector2(
                LimitAxis(position.x, velocity.x, Minimum.x, Maximum.x, deltaTime),
                LimitAxis(position.y, velocity.y, Minimum.y, Maximum.y, deltaTime));
        }

        private static float LimitAxis(float position, float velocity, float minimum,
            float maximum, float deltaTime)
        {
            if (velocity > 0f)
                return Mathf.Min(velocity, Mathf.Max(0f, (maximum - position) / deltaTime));
            if (velocity < 0f)
                return Mathf.Max(velocity, Mathf.Min(0f, (minimum - position) / deltaTime));
            return 0f;
        }
    }
}
