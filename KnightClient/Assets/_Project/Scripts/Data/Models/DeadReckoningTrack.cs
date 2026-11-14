using UnityEngine;

namespace KnightOnline.Client.Data.Models
{
    /// <summary>
    /// Protocol-3 movement of one remote entity: the last authoritative state
    /// (position, direction, speed, local receive time). Between updates the
    /// position is extrapolated along direction * speed; the server resends
    /// whenever that extrapolation would drift (direction change, collision,
    /// periodic correction).
    /// </summary>
    public sealed class DeadReckoningTrack
    {
        /// <summary>
        /// Stop extrapolating after this long without an update (the server
        /// refreshes moving players every second, so this only triggers on
        /// lost updates or stalls).
        /// </summary>
        public const double MaximumExtrapolationSeconds = 2.0;

        private Vector2 _position;
        private Vector2 _direction;
        private float _speed;
        private double _receivedAt;

        public DeadReckoningTrack(Vector2 position, Vector2 direction, float speed,
            double receivedAt)
        {
            Set(position, direction, speed, receivedAt);
        }

        public Vector2 LastPosition => _position;
        public Vector2 Direction => _direction;
        public float Speed => _speed;

        public void Set(Vector2 position, Vector2 direction, float speed,
            double receivedAt)
        {
            _position = position;
            _direction = direction;
            _speed = float.IsFinite(speed) && speed > 0f ? speed : 0f;
            _receivedAt = receivedAt;
        }

        public Vector2 Predict(double now)
        {
            double elapsed = now - _receivedAt;
            if (elapsed <= 0)
                return _position;
            if (elapsed > MaximumExtrapolationSeconds)
                elapsed = MaximumExtrapolationSeconds;
            return _position + _direction * (_speed * (float)elapsed);
        }

        /// <summary>
        /// Moves a displayed position toward the prediction: exponential
        /// smoothing hides small corrections, large errors snap.
        /// </summary>
        public static Vector2 Smooth(Vector2 displayed, Vector2 target,
            float deltaTime, float sharpness = 12f, float snapDistance = 3f)
        {
            if ((target - displayed).sqrMagnitude >= snapDistance * snapDistance)
                return target;
            float t = 1f - Mathf.Exp(-sharpness * Mathf.Max(0f, deltaTime));
            return Vector2.Lerp(displayed, target, t);
        }
    }
}
