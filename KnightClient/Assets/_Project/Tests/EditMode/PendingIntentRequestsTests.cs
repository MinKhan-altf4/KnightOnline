using System;
using KnightOnline.Client.Network;
using NUnit.Framework;

namespace KnightOnline.Client.UI.Tests
{
    public sealed class PendingIntentRequestsTests
    {
        [Test]
        public void SameIntent_ReusesRequestId_UntilAnswered()
        {
            var pending = new PendingIntentRequests();

            Guid first = pending.GetOrCreate("npc:mother");
            Guid retry = pending.GetOrCreate("npc:mother");

            Assert.AreEqual(first, retry);
            Assert.AreEqual(1, pending.Count);
        }

        [Test]
        public void AnsweredIntent_GetsNewRequestId()
        {
            var pending = new PendingIntentRequests();
            Guid first = pending.GetOrCreate("portal:village_to_wolf_field");

            Assert.IsTrue(pending.Complete(first));
            Guid next = pending.GetOrCreate("portal:village_to_wolf_field");

            Assert.AreNotEqual(first, next);
        }

        [Test]
        public void DifferentIntents_HaveDifferentIds()
        {
            var pending = new PendingIntentRequests();

            Assert.AreNotEqual(pending.GetOrCreate("npc:a"), pending.GetOrCreate("npc:b"));
        }

        [Test]
        public void UnknownResponse_IsIgnored()
        {
            var pending = new PendingIntentRequests();
            Guid id = pending.GetOrCreate("npc:a");

            Assert.IsFalse(pending.Complete(Guid.NewGuid()));
            Assert.AreEqual(id, pending.GetOrCreate("npc:a"));
        }

        [Test]
        public void Clear_DropsPendingIntents()
        {
            var pending = new PendingIntentRequests();
            Guid id = pending.GetOrCreate("npc:a");

            pending.Clear();

            Assert.AreEqual(0, pending.Count);
            Assert.AreNotEqual(id, pending.GetOrCreate("npc:a"));
        }
    }
}
