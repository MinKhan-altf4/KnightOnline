using UnityEngine;

namespace KnightOnline.Client.Gameplay.Player
{
    /// <summary>
    /// Eight facing sprites of one character, loaded from
    /// Resources/Characters/&lt;name&gt;/&lt;name&gt;_standing_&lt;direction&gt;.png
    /// (PixelDoll "generic" export; mirrored directions are pre-flipped).
    /// </summary>
    public sealed class DirectionalCharacterSprites
    {
        /// <summary>PixelDoll row order: index 0 = south, then clockwise on screen.</summary>
        public static readonly string[] DirectionIds =
        {
            "south", "south-east", "east", "north-east",
            "north", "north-west", "west", "south-west",
        };

        public const int South = 0;

        private readonly Sprite[] _sprites;

        private DirectionalCharacterSprites(Sprite[] sprites) => _sprites = sprites;

        /// <summary>PixelDoll cell size and feet pivot (export.bakedCell/bakedPivot).</summary>
        public const float CellPixels = 128f;
        public static readonly Vector2 FeetPivot = new(64f / 128f, 1f - 116f / 128f);

        /// <summary>Must match PixelCharacterImporter.PixelsPerUnit.</summary>
        public const float PixelsPerUnit = 64f;

        private static readonly System.Collections.Generic.Dictionary<string, DirectionalCharacterSprites>
            Cache = new();

        /// <summary>Returns null (with a warning) if any direction is missing.</summary>
        public static DirectionalCharacterSprites Load(string characterName)
        {
            if (Cache.TryGetValue(characterName, out DirectionalCharacterSprites cached))
                return cached;

            var sprites = new Sprite[DirectionIds.Length];
            for (int index = 0; index < DirectionIds.Length; index++)
            {
                string path = $"Characters/{characterName}/" +
                              $"{characterName.ToLowerInvariant()}_standing_{DirectionIds[index]}";
                sprites[index] = LoadSprite(path);
                if (sprites[index] == null)
                {
                    Object[] found = Resources.LoadAll($"Characters/{characterName}");
                    Debug.LogWarning(
                        $"[Character] Missing sprite Resources/{path}.png " +
                        $"(Resources/Characters/{characterName} contains {found.Length} " +
                        $"object(s): {string.Join(", ", System.Array.ConvertAll(found, o => $"{o.name}:{o.GetType().Name}"))})");
                    return null;
                }
            }

            var set = new DirectionalCharacterSprites(sprites);
            Cache[characterName] = set;
            return set;
        }

        /// <summary>
        /// Sprite import settings can be missing (texture imported before the
        /// importer rule existed, or reset by hand), so fall back to building the
        /// sprite from the texture with the same pivot and scale.
        /// </summary>
        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;

            Sprite[] subSprites = Resources.LoadAll<Sprite>(path);
            if (subSprites.Length > 0)
                return subSprites[0];

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
                return null;

            texture.filterMode = FilterMode.Point;
            Debug.Log($"[Character] Built sprite from texture Resources/{path} " +
                      "(import settings were not Sprite).");
            return Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                FeetPivot,
                PixelsPerUnit * texture.width / CellPixels,
                0,
                SpriteMeshType.FullRect);
        }

        public Sprite Get(int directionIndex) =>
            _sprites[((directionIndex % 8) + 8) % 8];

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
    }
}
