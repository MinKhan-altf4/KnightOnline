using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Network
{
    /// <summary>
    /// Keeps one RequestId per client intent (for example "talk to NPC X")
    /// until the server answers it (B7). Sending the same intent again while it
    /// is unanswered reuses the id, so the server's RequestId idempotency
    /// treats it as a retry instead of a second command.
    ///
    /// Main-thread only, like the rest of NetworkClient's send path.
    /// </summary>
    public sealed class PendingIntentRequests
    {
        private readonly Dictionary<string, Guid> _byIntent =
            new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, string> _byRequestId = new();

        public int Count => _byIntent.Count;

        /// <summary>Returns the pending id for this intent or creates one.</summary>
        public Guid GetOrCreate(string intentKey)
        {
            if (string.IsNullOrEmpty(intentKey))
                throw new ArgumentException("Intent key is required.", nameof(intentKey));

            if (_byIntent.TryGetValue(intentKey, out Guid existing))
                return existing;

            Guid requestId = Guid.NewGuid();
            _byIntent.Add(intentKey, requestId);
            _byRequestId.Add(requestId, intentKey);
            return requestId;
        }

        /// <summary>Marks the intent answered; unknown ids are ignored.</summary>
        public bool Complete(Guid requestId)
        {
            if (!_byRequestId.Remove(requestId, out string intentKey))
                return false;
            _byIntent.Remove(intentKey);
            return true;
        }

        /// <summary>Drops every pending intent, e.g. when the connection closes.</summary>
        public void Clear()
        {
            _byIntent.Clear();
            _byRequestId.Clear();
        }
    }
}
