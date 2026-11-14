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

        // ---- InterpolationBuffer -----------------------------------------

        [Test]
        public void Interpolation_LerpsBetweenSamples()
        {
            var buffer = new InterpolationBuffer();
            buffer.Add(0.0, Vector2.zero);
            buffer.Add(0.1, new Vector2(1, 0));

            Assert.IsTrue(buffer.TrySample(0.05, out Vector2 position));
            Assert.AreEqual(0.5f, position.x, 1e-4f);
        }

        [Test]
        public void Interpolation_ClampsOutsideRange()
        {
            var buffer = new InterpolationBuffer();
            buffer.Add(1.0, Vector2.zero);
            buffer.Add(1.1, new Vector2(1, 0));

            buffer.TrySample(0.0, out Vector2 before);
            buffer.TrySample(5.0, out Vector2 after);

            Assert.AreEqual(Vector2.zero, before);
            Assert.AreEqual(new Vector2(1, 0), after);
        }

        [Test]
        public void Interpolation_AfterIdle_DoesNotGlideAcrossIdlePeriod()
        {
            var buffer = new InterpolationBuffer(idleGapSeconds: 0.25);
            buffer.Add(0.0, Vector2.zero);
            buffer.Add(5.0, new Vector2(1, 0)); // started moving after 5 s idle

            buffer.TrySample(4.0, out Vector2 duringIdle);

            Assert.AreEqual(Vector2.zero, duringIdle);
        }

        // ---- RemotePlayerDirectory ----------------------------------------

        [Test]
        public void Directory_IgnoresOldTicks_AndTracksMembership()
        {
            var directory = new RemotePlayerDirectory(null);

            Assert.IsTrue(directory.TryBeginSnapshot(10));
            directory.Spawn(2, "Beta", 1, "warrior", Vector2.zero, Vector2.zero, 0);
            int afterSpawn = directory.MembershipVersion;
            Assert.IsFalse(directory.TryBeginSnapshot(10));
            Assert.IsFalse(directory.TryBeginSnapshot(9));

            directory.Move(2, Vector2.one, Vector2.right, 0.1);
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
