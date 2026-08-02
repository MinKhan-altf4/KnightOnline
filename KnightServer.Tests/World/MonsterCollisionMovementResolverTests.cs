using System.Numerics;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Tests.World;

public sealed class MonsterCollisionMovementResolverTests
{
    [Fact]
    public void Resolve_StopsBeforeCrossingAliveMonster()
    {
        MonsterService monsters = CreateMonsterWorld(out _);
        var resolver = new MonsterCollisionMovementResolver(
            monsters,
            CreateOptions());

        Vector2 resolved = resolver.Resolve(
            Vector2.Zero,
            new Vector2(3f, 0f));

        Assert.InRange(resolved.X, 1.14f, 1.151f);
        Assert.Equal(0f, resolved.Y);
    }

    [Fact]
    public void Resolve_DoesNotBlockOnDeadMonster()
    {
        MonsterService monsters = CreateMonsterWorld(out int monsterId);
        monsters.ApplyDamage(monsterId, 100, DateTime.UtcNow);
        var resolver = new MonsterCollisionMovementResolver(
            monsters,
            CreateOptions());

        Vector2 resolved = resolver.Resolve(
            Vector2.Zero,
            new Vector2(3f, 0f));

        Assert.Equal(new Vector2(3f, 0f), resolved);
    }

    [Fact]
    public void Resolve_AllowsPlayerToLeaveRespawnOverlap()
    {
        MonsterService monsters = CreateMonsterWorld(out _);
        var resolver = new MonsterCollisionMovementResolver(
            monsters,
            CreateOptions());

        Vector2 resolved = resolver.Resolve(
            new Vector2(2f, 0f),
            new Vector2(1f, 0f));

        Assert.Equal(new Vector2(1f, 0f), resolved);
    }

    private static MonsterService CreateMonsterWorld(out int monsterId)
    {
        var monsters = new MonsterService();
        monsterId = monsters.Spawn(
            new MonsterDefinition(
                1,
                "Training Wolf",
                1,
                50,
                TimeSpan.FromSeconds(10)),
            new WorldPosition(2f, 0f));
        return monsters;
    }

    private static WorldOptions CreateOptions() =>
        new()
        {
            PlayerCollisionRadius = 0.35f,
            MonsterCollisionRadius = 0.5f,
        };
}
