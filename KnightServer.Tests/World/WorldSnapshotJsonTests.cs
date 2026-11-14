using System.Numerics;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.World;

namespace KnightServer.Tests.World;

public sealed class WorldSnapshotJsonTests
{
    [Fact]
    public void Build_DeserializesAsSharedPacket()
    {
        var json = new WorldSnapshotJson();
        json.BeginTick();
        var time = new DateTime(2026, 11, 14, 8, 30, 15, 123, DateTimeKind.Utc);
        var spawned = new WorldPlayerState(7, "Hiệp sĩ \"Một\"", 3, "warrior", "map",
            new Vector2(1.23456f, -2f), new Vector2(0.6f, 0.8f));
        var moved = new WorldPlayerState(9, "B", 1, "rogue", "map",
            new Vector2(-0.0004f, 5.5f), Vector2.Zero);

        string payload = json.Build(42, time,
            new VisibilityDelta([spawned], [moved], [3, 4]));
        WorldEntitiesSnapshotPacket packet =
            JsonSerializer.Deserialize<WorldEntitiesSnapshotPacket>(payload)!;

        Assert.Equal(42, packet.ServerTick);
        Assert.Equal(time, packet.ServerTimeUtc.ToUniversalTime());
        RemotePlayerSpawnPacket spawn = Assert.Single(packet.Spawned);
        Assert.Equal("Hiệp sĩ \"Một\"", spawn.Name);
        Assert.Equal("warrior", spawn.ClassDefinitionId);
        Assert.Equal(3, spawn.Level);
        Assert.Equal(1.235f, spawn.X, 3);
        Assert.Equal(-2f, spawn.Y);
        Assert.Equal(0.8f, spawn.DirectionY, 3);
        RemotePlayerMovePacket move = Assert.Single(packet.Moved);
        Assert.Equal(9, move.CharacterId);
        Assert.Equal(0f, move.X);
        Assert.Equal(5.5f, move.Y);
        Assert.Equal([3, 4], packet.Despawned);
    }

    [Fact]
    public void Build_EmptyLists_AreValid()
    {
        var json = new WorldSnapshotJson();
        json.BeginTick();

        WorldEntitiesSnapshotPacket packet = JsonSerializer.Deserialize<WorldEntitiesSnapshotPacket>(
            json.Build(1, DateTime.UtcNow, new VisibilityDelta([], [], [])))!;

        Assert.Empty(packet.Spawned);
        Assert.Empty(packet.Moved);
        Assert.Empty(packet.Despawned);
    }

    [Theory]
    [InlineData(1.5f, "1.5")]
    [InlineData(-0.3333f, "-0.333")]
    [InlineData(0.0001f, "0")]
    [InlineData(float.NaN, "0")]
    [InlineData(12345.6789f, "12345.679")]
    public void Number_IsInvariantAndRounded(float value, string expected)
    {
        Assert.Equal(expected, WorldSnapshotJson.Number(value));
    }
}
