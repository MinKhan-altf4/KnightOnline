using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.World;
using Xunit;

namespace KnightServer.Tests.World;

public sealed class MapCatalogTests
{
    [Fact]
    public void ResolveSpawn_ReturnsConfiguredAuthoritativePosition()
    {
        var catalog = new ConfiguredMapCatalog([CreateTutorialMap()]);

        bool found = catalog.TryResolveSpawn(
            "tutorial_map_01",
            "tutorial_spawn_default",
            out MapSpawnPoint spawn);

        Assert.True(found);
        Assert.Equal(new WorldPosition(2f, -3f), spawn.Position);
        Assert.True(spawn.IsSafeZone);
    }

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(-10f, -5f, true)]
    [InlineData(10f, 5f, true)]
    [InlineData(10.01f, 0f, false)]
    [InlineData(0f, -5.01f, false)]
    public void ContainsPosition_UsesConfiguredMapBounds(
        float x,
        float y,
        bool expected)
    {
        var catalog = new ConfiguredMapCatalog([CreateTutorialMap()]);

        Assert.Equal(
            expected,
            catalog.ContainsPosition(
                "tutorial_map_01",
                new WorldPosition(x, y)));
    }

    [Fact]
    public void ResolveSpawn_RejectsUnknownMapOrSpawn()
    {
        var catalog = new ConfiguredMapCatalog([CreateTutorialMap()]);

        Assert.False(catalog.TryResolveSpawn(
            "unknown_map",
            "tutorial_spawn_default",
            out _));
        Assert.False(catalog.TryResolveSpawn(
            "tutorial_map_01",
            "unknown_spawn",
            out _));
    }

    private static MapDefinitionOptions CreateTutorialMap() => new()
    {
        DefinitionId = "tutorial_map_01",
        DisplayName = "Tutorial Map",
        MinimumX = -10f,
        MaximumX = 10f,
        MinimumY = -5f,
        MaximumY = 5f,
        SpawnPoints =
        [
            new MapSpawnPointOptions
            {
                SpawnPointId = "tutorial_spawn_default",
                PositionX = 2f,
                PositionY = -3f,
                IsSafeZone = true,
            },
        ],
    };
}
