using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KnightOnline.Server.World;

/// <summary>
/// Hand-built JSON for <c>WorldEntitiesSnapshotPacket</c>.
///
/// Every observer receives a different subset of the same players, so the
/// tick serializes each player's spawn/move object once and concatenates the
/// fragments per observer instead of running reflection-based serialization
/// for every (observer, player) pair. Output is the same shape System.Text.Json
/// produces for the shared packet class (PascalCase property names, ISO-8601
/// UTC time) so clients deserialize it unchanged. Coordinates are rounded to
/// 0.001 world units.
/// </summary>
public sealed class WorldSnapshotJson
{
    private readonly Dictionary<int, string> _spawnFragments = new();
    private readonly Dictionary<int, string> _moveFragments = new();

    /// <summary>Drops fragments from the previous tick.</summary>
    public void BeginTick()
    {
        _spawnFragments.Clear();
        _moveFragments.Clear();
    }

    public string Build(long serverTick, DateTime serverTimeUtc, VisibilityDelta delta)
    {
        var builder = new StringBuilder(64 + delta.Moved.Count * 96 + delta.Spawned.Count * 192);
        builder.Append("{\"ServerTick\":").Append(serverTick.ToString(CultureInfo.InvariantCulture))
            .Append(",\"ServerTimeUtc\":\"")
            .Append(serverTimeUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
            .Append("\",\"Spawned\":[");
        AppendFragments(builder, delta.Spawned, _spawnFragments, SpawnFragment);
        builder.Append("],\"Moved\":[");
        AppendFragments(builder, delta.Moved, _moveFragments, MoveFragment);
        builder.Append("],\"Despawned\":[");
        for (int index = 0; index < delta.Despawned.Count; index++)
        {
            if (index > 0)
                builder.Append(',');
            builder.Append(delta.Despawned[index].ToString(CultureInfo.InvariantCulture));
        }
        return builder.Append("]}").ToString();
    }

    private static void AppendFragments(StringBuilder builder,
        IReadOnlyList<WorldPlayerState> players, Dictionary<int, string> cache,
        Func<WorldPlayerState, string> create)
    {
        for (int index = 0; index < players.Count; index++)
        {
            WorldPlayerState player = players[index];
            if (!cache.TryGetValue(player.CharacterId, out string? fragment))
            {
                fragment = create(player);
                cache.Add(player.CharacterId, fragment);
            }
            if (index > 0)
                builder.Append(',');
            builder.Append(fragment);
        }
    }

    private static string MoveFragment(WorldPlayerState player) =>
        "{\"CharacterId\":" + player.CharacterId.ToString(CultureInfo.InvariantCulture) +
        ",\"X\":" + Number(player.Position.X) +
        ",\"Y\":" + Number(player.Position.Y) +
        ",\"DirectionX\":" + Number(player.Direction.X) +
        ",\"DirectionY\":" + Number(player.Direction.Y) +
        ",\"Speed\":" + Number(player.Speed) + "}";

    private static string SpawnFragment(WorldPlayerState player) =>
        "{\"CharacterId\":" + player.CharacterId.ToString(CultureInfo.InvariantCulture) +
        ",\"Name\":" + JsonSerializer.Serialize(player.Name) +
        ",\"Level\":" + player.Level.ToString(CultureInfo.InvariantCulture) +
        ",\"ClassDefinitionId\":" + JsonSerializer.Serialize(player.ClassDefinitionId) +
        ",\"X\":" + Number(player.Position.X) +
        ",\"Y\":" + Number(player.Position.Y) +
        ",\"DirectionX\":" + Number(player.Direction.X) +
        ",\"DirectionY\":" + Number(player.Direction.Y) +
        ",\"Speed\":" + Number(player.Speed) + "}";

    /// <summary>Finite, invariant, at most 3 decimals ("1.5", "-0.333", "0").</summary>
    public static string Number(float value)
    {
        if (!float.IsFinite(value))
            return "0";
        double rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        return rounded == 0 ? "0" : rounded.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
