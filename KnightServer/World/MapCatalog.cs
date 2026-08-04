using KnightOnline.Server.Configuration;
using KnightOnline.Server.Monsters;

namespace KnightOnline.Server.World;

public readonly record struct MapSpawnPoint(
    string MapDefinitionId,
    string SpawnPointId,
    WorldPosition Position,
    bool IsSafeZone);

public interface IMapCatalog
{
    bool ContainsMap(string mapDefinitionId);

    bool ContainsPosition(string mapDefinitionId, WorldPosition position);

    WorldPosition ClampPosition(string mapDefinitionId, WorldPosition position);

    bool TryResolveSpawn(
        string mapDefinitionId,
        string spawnPointId,
        out MapSpawnPoint spawnPoint);
}

public sealed class ConfiguredMapCatalog : IMapCatalog
{
    private sealed record MapEntry(
        float MinimumX,
        float MaximumX,
        float MinimumY,
        float MaximumY,
        IReadOnlyDictionary<string, MapSpawnPoint> SpawnPoints);

    private readonly IReadOnlyDictionary<string, MapEntry> _maps;

    public ConfiguredMapCatalog(IEnumerable<MapDefinitionOptions> definitions)
    {
        _maps = definitions.ToDictionary(
            definition => definition.DefinitionId,
            definition => new MapEntry(
                definition.MinimumX,
                definition.MaximumX,
                definition.MinimumY,
                definition.MaximumY,
                definition.SpawnPoints.ToDictionary(
                    spawn => spawn.SpawnPointId,
                    spawn => new MapSpawnPoint(
                        definition.DefinitionId,
                        spawn.SpawnPointId,
                        new WorldPosition(spawn.PositionX, spawn.PositionY),
                        spawn.IsSafeZone),
                    StringComparer.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool ContainsMap(string mapDefinitionId) =>
        !string.IsNullOrWhiteSpace(mapDefinitionId) &&
        _maps.ContainsKey(mapDefinitionId);

    public bool ContainsPosition(
        string mapDefinitionId,
        WorldPosition position) =>
        _maps.TryGetValue(mapDefinitionId, out MapEntry? map) &&
        position.X >= map.MinimumX &&
        position.X <= map.MaximumX &&
        position.Y >= map.MinimumY &&
        position.Y <= map.MaximumY;

    public WorldPosition ClampPosition(string mapDefinitionId,
        WorldPosition position)
    {
        if (!_maps.TryGetValue(mapDefinitionId, out MapEntry? map))
            return position;
        return new WorldPosition(
            Math.Clamp(position.X, map.MinimumX, map.MaximumX),
            Math.Clamp(position.Y, map.MinimumY, map.MaximumY));
    }

    public bool TryResolveSpawn(
        string mapDefinitionId,
        string spawnPointId,
        out MapSpawnPoint spawnPoint)
    {
        spawnPoint = default;
        return !string.IsNullOrWhiteSpace(mapDefinitionId) &&
               !string.IsNullOrWhiteSpace(spawnPointId) &&
               _maps.TryGetValue(mapDefinitionId, out MapEntry? map) &&
               map.SpawnPoints.TryGetValue(spawnPointId, out spawnPoint);
    }
}
