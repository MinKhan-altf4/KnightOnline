using System.Collections.Generic;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Own-player reconciliation (B6). The client records where it predicted
    /// itself when sending input #n. When the server acknowledges #n with its
    /// authoritative position, the error is measured against that recorded
    /// prediction, not against the current position, so normal latency does
    /// not pull the player back by one round trip.
    /// </summary>
    public sealed class MovementPrediction
    {
        public const int Capacity = 128;

        private readonly LinkedList<(long Sequence, Vector2 Position)> _history = new();

        public int Count => _history.Count;

        public void Record(long sequence, Vector2 predictedPosition)
        {
            if (_history.Last != null && sequence <= _history.Last.Value.Sequence)
                return; // sequences only grow; ignore stale/duplicate records
            _history.AddLast((sequence, predictedPosition));
            while (_history.Count > Capacity)
                _history.RemoveFirst();
        }

        /// <summary>
        /// Returns the correction to add to the current position. Entries older
        /// than the acknowledged one are dropped; the acknowledged entry is kept
        /// (a later snapshot may acknowledge it again, e.g. after a server-side
        /// displacement). Remaining predictions are shifted by the correction
        /// so it is not applied twice.
        /// </summary>
        public bool TryReconcile(long acknowledgedSequence, Vector2 serverPosition,
            out Vector2 correction)
        {
            correction = Vector2.zero;
            while (_history.First != null &&
                   _history.First.Value.Sequence < acknowledgedSequence)
                _history.RemoveFirst();

            if (_history.First == null ||
                _history.First.Value.Sequence != acknowledgedSequence)
                return false;

            correction = serverPosition - _history.First.Value.Position;
            if (correction == Vector2.zero)
                return true;

            for (LinkedListNode<(long Sequence, Vector2 Position)> node = _history.First;
                 node != null; node = node.Next)
            {
                node.Value = (node.Value.Sequence, node.Value.Position + correction);
            }
            return true;
        }

        public void Clear() => _history.Clear();
    }
}
