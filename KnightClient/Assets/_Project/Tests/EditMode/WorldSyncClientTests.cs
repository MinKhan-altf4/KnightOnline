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

        // ---- DirectionalCharacterSprites ----------------------------------

        [TestCase(0f, -1f, 0)]   // south
        [TestCase(1f, -1f, 1)]   // south-east
        [TestCase(1f, 0f, 2)]    // east
        [TestCase(1f, 1f, 3)]    // north-east
        [TestCase(0f, 1f, 4)]    // north
        [TestCase(-1f, 1f, 5)]   // north-west
        [TestCase(-1f, 0f, 6)]   // west
        [TestCase(-1f, -1f, 7)]  // south-west
        public void Facing_MatchesPixelDollRowOrder(float x, float y, int expected)
        {
            Assert.AreEqual(expected,
                DirectionalCharacterSprites.DirectionIndex(new Vector2(x, y), 0));
            StringAssert.AreEqualIgnoringCase(
                new[] { "south", "south-east", "east", "north-east", "north",
                        "north-west", "west", "south-west" }[expected],
                DirectionalCharacterSprites.DirectionIds[expected]);
        }

        [Test]
        public void Facing_KeepsLastDirectionWhenStanding()
        {
            Assert.AreEqual(6, DirectionalCharacterSprites.DirectionIndex(Vector2.zero, 6));
        }

        [TestCase("walking_south_strip6", "walking", 0)]
        [TestCase("standing_north-east_strip1", "standing", 3)]
        [TestCase("hero_walking_south-west", "walking", 7)]
        [TestCase("hero_standing_east", "standing", 2)]
        [TestCase("Hero_Walking_North-West_strip8", "walking", 5)]
        [TestCase("walk_east_strip8", "walking", 2)]
        [TestCase("idle_south_strip1", "standing", 0)]
        public void SheetName_IsParsed(string fileName, string move, int direction)
        {
            Assert.IsTrue(DirectionalCharacterSprites.TryParseSheetName(
                fileName, "Hero", out string parsedMove, out int parsedDirection));
            Assert.AreEqual(move, parsedMove);
            Assert.AreEqual(direction, parsedDirection);
        }

        [TestCase("readme")]
        [TestCase("hero_south")]
        [TestCase("walking_up")]
        public void SheetName_RejectsUnknown(string fileName)
        {
            Assert.IsFalse(DirectionalCharacterSprites.TryParseSheetName(
                fileName, "Hero", out _, out _));
        }

        [TestCase(0.0, 0)]
        [TestCase(0.099, 0)]
        [TestCase(0.1, 1)]
        [TestCase(0.55, 5)]
        [TestCase(0.6, 0)]   // loops after 6 frames
        [TestCase(1.25, 0)]
        public void FrameAt_LoopsAtFrameTime(double elapsed, int expected)
        {
            Assert.AreEqual(expected, DirectionalCharacterSprites.FrameAt(elapsed, 6, 0.1f));
        }

        [Test]
        public void FrameAt_SingleFrame_IsAlwaysZero()
        {
            Assert.AreEqual(0, DirectionalCharacterSprites.FrameAt(12.3, 1, 0.1f));
        }

        [TestCase(0.0, 0)]      // starts on the ground
        [TestCase(0.15, 3)]     // top of the hop at half a step
        [TestCase(0.3, 0)]      // lands at the end of a step
        [TestCase(0.45, 3)]     // second step
        [TestCase(0.05, 2)]     // rising (rounded to whole pixels)
        public void Bob_HopsOncePerStep_InWholePixels(double elapsed, int expected)
        {
            Assert.AreEqual(expected, DirectionalSpriteView.BobOffsetPixels(elapsed, 0.3f, 3));
        }

        [Test]
        public void Bob_IsZeroWhenDisabled()
        {
            Assert.AreEqual(0, DirectionalSpriteView.BobOffsetPixels(1.0, 0.3f, 0));
            Assert.AreEqual(0, DirectionalSpriteView.BobOffsetPixels(-1.0, 0.3f, 3));
        }

        [TestCase(0, 2)]   // passing: body highest
        [TestCase(1, 1)]
        [TestCase(2, 0)]   // contact: body lowest
        [TestCase(3, 1)]
        [TestCase(4, 2)]   // passing (other leg)
        [TestCase(6, 0)]   // contact (other leg)
        [TestCase(8, 2)]   // wraps
        public void WalkBob_HighAtPassing_LowAtContact(int frame, int expected)
        {
            Assert.AreEqual(expected, DirectionalSpriteView.WalkBobPixels(frame, 8, 2));
        }

        [Test]
        public void WalkBob_SingleFrame_IsFlat()
        {
            Assert.AreEqual(0, DirectionalSpriteView.WalkBobPixels(0, 1, 2));
        }

        // ---- WalkableBounds -------------------------------------------------

        [Test]
        public void Bounds_ClampCenterLikeServer()
        {
            var bounds = new WalkableBounds(new Vector2(-10, -6), new Vector2(10, 6));

            Assert.AreEqual(new Vector2(10, -6), bounds.Clamp(new Vector2(12, -9)));
        }

        [Test]
        public void Bounds_StopAtEdge_ButSlideAlongIt()
        {
            var bounds = new WalkableBounds(new Vector2(-10, -6), new Vector2(10, 6));

            Vector2 velocity = bounds.LimitVelocity(new Vector2(10, 0), new Vector2(4, 4), 0.02f);

            Assert.AreEqual(0f, velocity.x);
            Assert.AreEqual(4f, velocity.y);
        }

        [Test]
        public void Bounds_LimitLastStepToReachEdgeExactly()
        {
            var bounds = new WalkableBounds(new Vector2(-10, -6), new Vector2(10, 6));

            Vector2 velocity = bounds.LimitVelocity(new Vector2(9.95f, 0), new Vector2(4, 0), 0.02f);

            Assert.AreEqual(2.5f, velocity.x, 1e-3f); // 0.05 units in 0.02 s
        }

        [Test]
        public void Bounds_MovingAwayFromEdge_IsUnchanged()
        {
            var bounds = new WalkableBounds(new Vector2(-10, -6), new Vector2(10, 6));

            Assert.AreEqual(new Vector2(-4, 0),
                bounds.LimitVelocity(new Vector2(10, 0), new Vector2(-4, 0), 0.02f));
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
