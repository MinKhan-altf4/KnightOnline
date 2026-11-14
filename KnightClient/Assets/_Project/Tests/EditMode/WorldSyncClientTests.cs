using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace KnightOnline.Client.UI.Tests
{
    public sealed class WorldSyncClientTests
    {
        // ---- MovementPrediction ------------------------------------------

        [Test]
        public void Prediction_MatchingServer_NeedsNoCorrection()
        {
            var prediction = new MovementPrediction();
            prediction.Record(1, new Vector2(1, 0));
            prediction.Record(2, new Vector2(2, 0));

            Assert.IsTrue(prediction.TryReconcile(1, new Vector2(1, 0), out Vector2 correction));
            Assert.AreEqual(Vector2.zero, correction);
        }

        [Test]
        public void Prediction_ErrorIsMeasuredAtAcknowledgedInput_NotCurrentPosition()
        {
            var prediction = new MovementPrediction();
            prediction.Record(1, new Vector2(1, 0));
            // Client has since moved on to x=3; server confirms input 1 at 1.2.

            Assert.IsTrue(prediction.TryReconcile(1, new Vector2(1.2f, 0), out Vector2 correction));
            Assert.AreEqual(0.2f, correction.x, 1e-4f);
        }

        [Test]
        public void Prediction_CorrectionIsNotAppliedTwice()
        {
            var prediction = new MovementPrediction();
            prediction.Record(1, new Vector2(1, 0));
            prediction.Record(2, new Vector2(2, 0));

            prediction.TryReconcile(1, new Vector2(1.5f, 0), out _);
            Assert.IsTrue(prediction.TryReconcile(2, new Vector2(2.5f, 0), out Vector2 second));

            Assert.AreEqual(Vector2.zero, second);
        }

        [Test]
        public void Prediction_UnknownSequence_FallsBack()
        {
            var prediction = new MovementPrediction();
            prediction.Record(5, Vector2.zero);

            Assert.IsFalse(prediction.TryReconcile(3, Vector2.one, out _));
        }

        // ---- DeadReckoningTrack -----------------------------------------

        [Test]
        public void DeadReckoning_ExtrapolatesAlongDirection()
        {
            var track = new DeadReckoningTrack(Vector2.zero, Vector2.right, 4f, 10.0);

            Assert.AreEqual(2f, track.Predict(10.5).x, 1e-4f);
        }

        [Test]
        public void DeadReckoning_StandingStill_StaysPut()
        {
            var track = new DeadReckoningTrack(Vector2.one, Vector2.zero, 4f, 0.0);

            Assert.AreEqual(Vector2.one, track.Predict(100.0));
        }

        [Test]
        public void DeadReckoning_CapsExtrapolationWithoutUpdates()
        {
            var track = new DeadReckoningTrack(Vector2.zero, Vector2.right, 1f, 0.0);

            Assert.AreEqual((float)DeadReckoningTrack.MaximumExtrapolationSeconds,
                track.Predict(60.0).x, 1e-4f);
        }

        [Test]
        public void DeadReckoning_UpdateReplacesState()
        {
            var track = new DeadReckoningTrack(Vector2.zero, Vector2.right, 4f, 0.0);

            track.Set(new Vector2(1, 1), Vector2.up, 2f, 1.0);

            Assert.AreEqual(new Vector2(1, 2), track.Predict(1.5));
        }

        [Test]
        public void Smooth_ConvergesForSmallErrors_AndSnapsLargeOnes()
        {
            Vector2 small = DeadReckoningTrack.Smooth(Vector2.zero, new Vector2(1, 0), 1f / 60f);
            Vector2 large = DeadReckoningTrack.Smooth(Vector2.zero, new Vector2(10, 0), 1f / 60f);

            Assert.Greater(small.x, 0f);
            Assert.Less(small.x, 1f);
            Assert.AreEqual(new Vector2(10, 0), large);
        }

        // ---- RemotePlayerDirectory ----------------------------------------

        [Test]
        public void Directory_IgnoresOldTicks_AndTracksMembership()
        {
            var directory = new RemotePlayerDirectory(null);

            Assert.IsTrue(directory.TryBeginSnapshot(10));
            directory.Spawn(2, "Beta", 1, "warrior", Vector2.zero, Vector2.zero, 4f, 0);
            int afterSpawn = directory.MembershipVersion;
            Assert.IsFalse(directory.TryBeginSnapshot(10));
            Assert.IsFalse(directory.TryBeginSnapshot(9));

            directory.Move(2, Vector2.one, Vector2.right, 4f, 0.1);
            Assert.AreEqual(afterSpawn, directory.MembershipVersion);

            directory.Despawn(2);
            Assert.AreEqual(0, directory.Players.Count);
            Assert.Greater(directory.MembershipVersion, afterSpawn);
        }

        [Test]
        public void Directory_Clear_ResetsTick()
        {
            var directory = new RemotePlayerDirectory(null);
            directory.TryBeginSnapshot(100);

            directory.Clear();

            Assert.IsTrue(directory.TryBeginSnapshot(1));
        }
    }
}
