using System.Collections.Generic;
using UnityEngine;

namespace KnightOnline.Client.Data.Models
{
    /// <summary>
    /// Timestamped positions of one remote entity, sampled at a fixed delay
    /// behind "now" so movement looks smooth between 10 Hz server snapshots.
    /// Times are local receive times in seconds (no clock sync needed).
    /// </summary>
    public sealed class InterpolationBuffer
    {
        public const int Capacity = 32;

        private readonly List<(double Time, Vector2 Position)> _samples =
            new(Capacity);
        private readonly double _idleGapSeconds;

        /// <param name="idleGapSeconds">
        /// If no sample arrived for longer than this, the entity was standing
        /// still: a copy of the last position is inserted just before the new
        /// sample so it does not glide across the whole idle period.
        /// </param>
        public InterpolationBuffer(double idleGapSeconds = 0.25)
        {
            _idleGapSeconds = idleGapSeconds;
        }

        public int Count => _samples.Count;

        public void Add(double time, Vector2 position)
        {
            if (_samples.Count > 0)
            {
                (double lastTime, Vector2 lastPosition) = _samples[_samples.Count - 1];
                if (time <= lastTime)
                    time = lastTime + 1e-4; // keep strictly increasing
                else if (time - lastTime > _idleGapSeconds)
                    _samples.Add((time - _idleGapSeconds * 0.5, lastPosition));
            }

            _samples.Add((time, position));
            if (_samples.Count > Capacity)
                _samples.RemoveRange(0, _samples.Count - Capacity);
        }

        /// <summary>
        /// Position at <paramref name="renderTime"/>: interpolated between the
        /// surrounding samples, clamped to the first/last sample outside them
        /// (no extrapolation).
        /// </summary>
        public bool TrySample(double renderTime, out Vector2 position)
        {
            position = default;
            if (_samples.Count == 0)
                return false;

            if (renderTime <= _samples[0].Time)
            {
                position = _samples[0].Position;
                return true;
            }

            for (int index = 1; index < _samples.Count; index++)
            {
                (double time, Vector2 next) = _samples[index];
                if (renderTime > time)
                    continue;

                (double previousTime, Vector2 previous) = _samples[index - 1];
                float t = (float)((renderTime - previousTime) / (time - previousTime));
                position = Vector2.Lerp(previous, next, t);
                return true;
            }

            position = _samples[_samples.Count - 1].Position;
            return true;
        }
    }
}
