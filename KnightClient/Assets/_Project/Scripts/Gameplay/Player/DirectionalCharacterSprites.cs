using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// All animation frames of one PixelDoll character, loaded from
    /// Resources/Characters/&lt;Name&gt;/. Accepted file names (PixelDoll
    /// "generic" export can be dropped in unchanged):
    /// <code>
    ///   walking_south_strip6.png      (PixelDoll generic)
    ///   hero_walking_south.png        (character prefix, any frame count)
    /// </code>
    /// Each file is a horizontal strip of square cells (frames = width/height).
    /// "standing" must exist for all 8 directions; other moves are optional
    /// and fall back to standing for a missing direction. Mirrored directions
    /// come pre-flipped from PixelDoll.
    /// </summary>
    public sealed class DirectionalCharacterSprites
    {
        public const string Standing = "standing";
        public const string Walking = "walking";

        /// <summary>PixelDoll row order: index 0 = south, then clockwise on screen.</summary>
        public static readonly string[] DirectionIds =
        {
            "south", "south-east", "east", "north-east",
            "north", "north-west", "west", "south-west",
        };

        public const int South = 0;

        /// <summary>PixelDoll cell size and feet pivot (export.bakedCell/bakedPivot).</summary>
        public const float CellPixels = 128f;
        public static readonly Vector2 FeetPivot = new(64f / 128f, 1f - 116f / 128f);

        /// <summary>Must match PixelCharacterImporter.PixelsPerUnit.</summary>
        public const float PixelsPerUnit = 64f;

        private static readonly Dictionary<string, DirectionalCharacterSprites> Cache = new();

        // move -> direction -> frames
        private readonly Dictionary<string, Sprite[][]> _moves;

        private DirectionalCharacterSprites(Dictionary<string, Sprite[][]> moves) =>
            _moves = moves;

        public bool HasMove(string move) => _moves.ContainsKey(move);

        /// <summary>Returns null (with a warning) if "standing" is incomplete.</summary>
        public static DirectionalCharacterSprites Load(string characterName)
        {
            if (Cache.TryGetValue(characterName, out DirectionalCharacterSprites cached))
                return cached;

            string folder = $"Characters/{characterName}";
            Texture2D[] textures = Resources.LoadAll<Texture2D>(folder);
            var moves = new Dictionary<string, Sprite[][]>(StringComparer.OrdinalIgnoreCase);
            foreach (Texture2D texture in textures)
            {
                if (!TryParseSheetName(texture.name, characterName, out string move,
                        out int direction))
                    continue;
                if (!moves.TryGetValue(move, out Sprite[][] directions))
                {
                    directions = new Sprite[DirectionIds.Length][];
                    moves.Add(move, directions);
                }
                directions[direction] = SliceStrip(texture);
            }

            if (!moves.TryGetValue(Standing, out Sprite[][] standing) ||
                Array.Exists(standing, frames => frames == null || frames.Length == 0))
            {
                Debug.LogWarning(
                    $"[Character] Resources/{folder} needs '{Standing}' for all 8 directions " +
                    $"(found {textures.Length} texture(s): " +
                    $"{string.Join(", ", Array.ConvertAll(textures, t => t.name))}).");
                return null;
            }

            // Optional moves: a missing direction reuses standing.
            foreach (Sprite[][] directions in moves.Values)
            {
                for (int index = 0; index < directions.Length; index++)
                    directions[index] ??= standing[index];
            }

            var set = new DirectionalCharacterSprites(moves);
            Cache[characterName] = set;
            Debug.Log($"[Character] Loaded {characterName}: " +
                      string.Join(", ", Describe(moves)));
            return set;
        }

        /// <summary>Frames of <paramref name="move"/> facing a direction (standing if absent).</summary>
        public Sprite[] Frames(string move, int directionIndex)
        {
            int direction = ((directionIndex % 8) + 8) % 8;
            if (!_moves.TryGetValue(move, out Sprite[][] directions))
                directions = _moves[Standing];
            return directions[direction];
        }

        public Sprite Get(int directionIndex) => Frames(Standing, directionIndex)[0];

        /// <summary>
        /// True when <paramref name="move"/> has its own frames for this
        /// direction (false when it falls back to standing).
        /// </summary>
        public bool HasOwnFrames(string move, int directionIndex)
        {
            int direction = ((directionIndex % 8) + 8) % 8;
            return _moves.TryGetValue(move, out Sprite[][] directions) &&
                   !ReferenceEquals(directions[direction], _moves[Standing][direction]);
        }

        /// <summary>
        /// Parses "walking_south_strip6", "hero_walking_south" or
        /// "hero_standing_north-east" into move and direction index.
        /// </summary>
        public static bool TryParseSheetName(string fileName, string characterName,
            out string move, out int direction)
        {
            move = null;
            direction = -1;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            string name = fileName.ToLowerInvariant();
            int strip = name.LastIndexOf("_strip", StringComparison.Ordinal);
            if (strip > 0)
                name = name.Substring(0, strip);
            string prefix = characterName.ToLowerInvariant() + "_";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                name = name.Substring(prefix.Length);

            // Longest direction ids first so "south-east" wins over "east".
            for (int length = 10; length >= 4; length--)
            {
                for (int index = 0; index < DirectionIds.Length; index++)
                {
                    string id = DirectionIds[index];
                    if (id.Length != length || !name.EndsWith("_" + id, StringComparison.Ordinal))
                        continue;
                    move = NormalizeMove(name.Substring(0, name.Length - id.Length - 1));
                    direction = index;
                    return move.Length > 0;
                }
            }
            return false;
        }

        /// <summary>
        /// Maps PixelDoll preset names to the moves the game plays
        /// ("walk" preset -> "walking", "idle" -> "standing").
        /// </summary>
        public static string NormalizeMove(string move) => move switch
        {
            "walk" => Walking,
            "idle" => Standing,
            _ => move,
        };

        /// <summary>Frame to show after <paramref name="elapsedSeconds"/> of a looping move.</summary>
        public static int FrameAt(double elapsedSeconds, int frameCount, float frameSeconds)
        {
            if (frameCount <= 1 || frameSeconds <= 0f || elapsedSeconds <= 0)
                return 0;
            long frame = (long)Math.Floor(elapsedSeconds / frameSeconds);
            return (int)(frame % frameCount);
        }

        /// <summary>
        /// Maps a movement vector (world space, +y = north) to one of the 8
        /// directions; returns <paramref name="current"/> when not moving.
        /// </summary>
        public static int DirectionIndex(Vector2 direction, int current)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return current;

            // Atan2(x, -y): south = 0, south-east = 45, east = 90, north = 180,
            // west = -90; 45 degree sectors match the PixelDoll row order.
            float degrees = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
            int sector = Mathf.RoundToInt(degrees / 45f);
            return ((sector % 8) + 8) % 8;
        }

        private static Sprite[] SliceStrip(Texture2D texture)
        {
            texture.filterMode = FilterMode.Point;
            int cell = texture.height;
            int count = Mathf.Max(1, texture.width / cell);
            var frames = new Sprite[count];
            for (int index = 0; index < count; index++)
            {
                frames[index] = Sprite.Create(texture,
                    new Rect(index * cell, 0f, cell, cell),
                    FeetPivot,
                    PixelsPerUnit * cell / CellPixels,
                    0,
                    SpriteMeshType.FullRect);
                frames[index].name = $"{texture.name}_{index}";
            }
            return frames;
        }

        private static IEnumerable<string> Describe(Dictionary<string, Sprite[][]> moves)
        {
            foreach (KeyValuePair<string, Sprite[][]> move in moves)
                yield return $"{move.Key} ({move.Value[South].Length} frame(s))";
        }
    }
}
