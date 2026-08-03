using System.Numerics;
using KnightOnline.Server.Players;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Tests.Players;

public sealed class PlayerSessionMovementTests
{
    private static readonly DateTime InitialUtc =
        new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TrySetMovement_AcceptsOnlyIncreasingClientSequence()
    {
        PlayerSession session = CreateSession();
        var resolver = new PassThroughResolver();

        bool first = session.TrySetMovement(
            Vector2.UnitX,
            clientSequence: 1,
            InitialUtc,
            resolver);
        bool duplicate = session.TrySetMovement(
            -Vector2.UnitX,
            clientSequence: 1,
            InitialUtc.AddMilliseconds(100),
            resolver);
        bool stale = session.TrySetMovement(
            -Vector2.UnitX,
            clientSequence: 0,
            InitialUtc.AddMilliseconds(200),
            resolver);
        bool next = session.TrySetMovement(
            Vector2.UnitY,
            clientSequence: 2,
            InitialUtc.AddMilliseconds(300),
            resolver);

        Assert.True(first);
        Assert.False(duplicate);
        Assert.False(stale);
        Assert.True(next);
        Assert.Equal(2, session.LastProcessedMovementSequence);
    }

    [Fact]
    public void TrySetMovement_AdvancesUsingPreviouslyAcceptedDirection()
    {
        PlayerSession session = CreateSession();
        var resolver = new PassThroughResolver();
        session.TrySetMovement(
            Vector2.UnitX,
            clientSequence: 1,
            InitialUtc,
            resolver);

        session.TrySetMovement(
            Vector2.Zero,
            clientSequence: 2,
            InitialUtc.AddMilliseconds(500),
            resolver);

        Assert.Equal(new Vector2(2f, 0f), session.Position);
    }

    [Fact]
    public void AuthoritativeDisplacement_ProducesNewServerSnapshotSequence()
    {
        PlayerSession session = CreateSession();
        PlayerPositionState before = session.CapturePositionSnapshot();

        bool displaced = session.TryResolveAuthoritativePosition(
            _ => new Vector2(3f, 4f),
            out Vector2 resolved);
        PlayerPositionState after = session.CapturePositionSnapshot();

        Assert.True(displaced);
        Assert.Equal(new Vector2(3f, 4f), resolved);
        Assert.Equal(new Vector2(3f, 4f), after.Position);
        Assert.True(after.ServerSequence > before.ServerSequence);
    }

    private static PlayerSession CreateSession() =>
        new(
            new PlayerSessionProfile(
                1,
                "Movement Test",
                1,
                0,
                "warrior",
                "male",
                "tutorial-map",
                "spawn-1",
                []),
            currentHealth: 100,
            maximumHealth: 100,
            moveSpeed: 4f,
            spawnPosition: Vector2.Zero,
            baseAttack: 1,
            maximumMovementDelta: TimeSpan.FromSeconds(1),
            InitialUtc);

    private sealed class PassThroughResolver : IWorldMovementResolver
    {
        public Vector2 Resolve(
            string mapDefinitionId,
            Vector2 start,
            Vector2 desiredEnd) =>
            desiredEnd;
    }
}
