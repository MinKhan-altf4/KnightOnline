using System.Numerics;
using KnightOnline.Server.World;

namespace KnightServer.Tests.World;

public sealed class DeadReckoningTests
{
    private static readonly DateTime T0 = new(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);

    private static WorldPlayerState Walker(Vector2 position, Vector2 direction,
        float speed = 4f) =>
        new(2, "Walker", 1, "warrior", "map", position, direction, speed);

    [Fact]
    public void SteadyMovement_IsNotResent_WhileExtrapolationHolds()
    {
        var tracker = new VisibilityTracker();
        tracker.Update([Walker(Vector2.Zero, Vector2.UnitX)], T0);

        // 0.5 s later the walker is exactly where the client extrapolates.
        VisibilityDelta delta = tracker.Update(
            [Walker(new Vector2(2, 0), Vector2.UnitX)], T0.AddSeconds(0.5));

        Assert.True(delta.IsEmpty);
    }

    [Fact]
    public void DirectionChange_IsSentImmediately()
    {
        var tracker = new VisibilityTracker();
        tracker.Update([Walker(Vector2.Zero, Vector2.UnitX)], T0);

        VisibilityDelta delta = tracker.Update(
            [Walker(new Vector2(0.4f, 0), Vector2.UnitY)], T0.AddSeconds(0.1));

        Assert.Single(delta.Moved);
    }

    [Fact]
    public void Drift_BeyondThreshold_IsCorrected()
    {
        var tracker = new VisibilityTracker(new DeadReckoningSettings(ErrorThreshold: 0.25f));
        tracker.Update([Walker(Vector2.Zero, Vector2.UnitX)], T0);

        // Slowed by collisions: 0.3 behind the extrapolated 2.0.
        VisibilityDelta delta = tracker.Update(
            [Walker(new Vector2(1.7f, 0), Vector2.UnitX)], T0.AddSeconds(0.5));

        Assert.Single(delta.Moved);
    }

    [Fact]
    public void MovingPlayer_GetsPeriodicRefresh()
    {
        var tracker = new VisibilityTracker(new DeadReckoningSettings(
            RefreshInterval: TimeSpan.FromSeconds(1)));
        tracker.Update([Walker(Vector2.Zero, Vector2.UnitX)], T0);

        Assert.True(tracker.Update([Walker(new Vector2(3.6f, 0), Vector2.UnitX)],
            T0.AddSeconds(0.9)).IsEmpty);
        Assert.Single(tracker.Update([Walker(new Vector2(4f, 0), Vector2.UnitX)],
            T0.AddSeconds(1.0)).Moved);
    }

    [Fact]
    public void StandingPlayer_IsNeverResent()
    {
        var tracker = new VisibilityTracker();
        tracker.Update([Walker(new Vector2(1, 1), Vector2.Zero)], T0);

        Assert.True(tracker.Update([Walker(new Vector2(1, 1), Vector2.Zero)],
            T0.AddSeconds(30)).IsEmpty);
    }

    [Fact]
    public void Stopping_IsSentWithFinalPosition()
    {
        var tracker = new VisibilityTracker();
        tracker.Update([Walker(Vector2.Zero, Vector2.UnitX)], T0);

        VisibilityDelta delta = tracker.Update(
            [Walker(new Vector2(1, 0), Vector2.Zero)], T0.AddSeconds(0.25));

        WorldPlayerState stop = Assert.Single(delta.Moved);
        Assert.Equal(Vector2.Zero, stop.Direction);
        Assert.Equal(new Vector2(1, 0), stop.Position);
    }
}
