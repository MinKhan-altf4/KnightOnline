using System.Numerics;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Tests.World;

public sealed class MonsterRespawnDisplacementResolverTests
{
    [Fact]
    public void Resolve_PushesPlayerOutsideRespawnedMonster()
    {
        MonsterService monsters = CreateMonsters(
            ("map-a", Vector2.Zero));
        WorldOptions options = CreateOptions();
        var resolver = new MonsterRespawnDisplacementResolver(
            monsters,
            options);

        Vector2 resolved = resolver.Resolve(
            "map-a",
            characterId: 1,
            Vector2.Zero);

        Assert.True(Vector2.Distance(resolved, Vector2.Zero) >= 0.85f);
    }

    [Fact]
    public void Resolve_FindsFreePositionBetweenMultipleMonsters()
    {
        MonsterService monsters = CreateMonsters(
            ("map-a", Vector2.Zero),
            ("map-a", new Vector2(0.5f, 0f)),
            ("map-a", new Vector2(-0.5f, 0f)));
        WorldOptions options = CreateOptions();
        var resolver = new MonsterRespawnDisplacementResolver(
            monsters,
            options);

        Vector2 resolved = resolver.Resolve(
            "map-a",
            characterId: 7,
            Vector2.Zero);

        foreach (MonsterSnapshot monster in monsters.GetSnapshots())
        {
            var center = new Vector2(
                monster.SpawnPosition.X,
                monster.SpawnPosition.Y);
            Assert.True(Vector2.Distance(resolved, center) >= 0.85f);
        }
    }

    [Fact]
    public void Resolve_IgnoresMonstersOnOtherMaps()
    {
        MonsterService monsters = CreateMonsters(
            ("map-b", Vector2.Zero));
        var resolver = new MonsterRespawnDisplacementResolver(
            monsters,
            CreateOptions());

        Vector2 resolved = resolver.Resolve(
            "map-a",
            characterId: 1,
            Vector2.Zero);

        Assert.Equal(Vector2.Zero, resolved);
    }

    private static MonsterService CreateMonsters(
        params (string MapId, Vector2 Position)[] spawns)
    {
        var monsters = new MonsterService();
        int definitionId = 1;
        foreach ((string mapId, Vector2 position) in spawns)
        {
            monsters.Spawn(
                new MonsterDefinition(
                    definitionId++,
                    "Respawn Test",
                    1,
                    10,
                    TimeSpan.Zero),
                mapId,
                new WorldPosition(position.X, position.Y));
        }

        return monsters;
    }

    private static WorldOptions CreateOptions() =>
        new()
        {
            PlayerCollisionRadius = 0.35f,
            MonsterCollisionRadius = 0.5f,
            RespawnDisplacementAngularSamples = 32,
        };
}
