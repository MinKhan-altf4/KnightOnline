using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace KnightOnline.Client.Gameplay.World.Environment
{
    /// <summary>
    /// Builds the ground tiles and props of the current map from a
    /// <see cref="MapEnvironmentLayout"/>. Pure presentation: no colliders,
    /// movement stays bound only by the server's map rectangle.
    /// </summary>
    public sealed class MapEnvironmentView : MonoBehaviour
    {
        /// <summary>16 px tiles at 20 px/unit: the map's 20 x 12 units are exactly 25 x 15 cells.</summary>
        private const float CellSize = 0.8f;
        /// <summary>Puts cell edges on whole map bounds (-10, -6, 6, 10).</summary>
        private static readonly Vector3 GridOrigin = new(0.4f, 0.4f, 0f);
        /// <summary>How far the dressed ground reaches past the play area (camera half-extents + 2).</summary>
        private static readonly Vector2 OuterMargin = new(10f, 6f);
        private const string TilePath = "World/PixelCrawler/Tiles/";
        private const string PropPath = "World/PixelCrawler/Props/";
        private const int GroundOrder = -100;
        private const int GroundEdgeOrder = -99;
        private const int DecalOrder = -50;
        /// <summary>Same order as the hero (Player.prefab) so Y-sorting decides who is in front.</summary>
        private const int StandingOrder = 1;
        private const float KeepClearRadius = 1.6f;

        private readonly Dictionary<string, Sprite> _sprites = new();
        private readonly Dictionary<Sprite, Tile> _tiles = new();
        private readonly HashSet<string> _missing = new();
        private Transform _root;

        public void Show(string mapId)
        {
            if (_root != null)
                Destroy(_root.gameObject);
            _root = new GameObject("Environment_" + mapId).transform;
            _root.SetParent(transform, false);

            MapEnvironmentLayout layout = MapEnvironmentLayouts.Get(mapId);
            ConfigureCamera(layout);
            var ground = new GroundGrid(layout, OuterMargin, CellSize, GridOrigin);
            BuildGround(ground);
            var random = new System.Random(StableHash(mapId));
            var standing = new List<Vector2>();
            var decals = new List<Vector2>();
            foreach (PropPlacement prop in layout.Props)
            {
                SpawnProp(prop.Sprite, prop.Position, prop.Layer, prop.Flip);
                (prop.Layer == PropLayer.Standing ? standing : decals).Add(prop.Position);
            }
            foreach (Vector2 position in layout.Bonfires)
            {
                SpawnBonfire(position);
                standing.Add(position);
            }
            foreach (ScatterRule rule in layout.Scatters)
                Scatter(rule, layout, ground, random,
                    rule.Layer == PropLayer.Standing ? standing : decals);
        }

        private static void ConfigureCamera(MapEnvironmentLayout layout)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            camera.backgroundColor = layout.Backdrop;
            // Lower on screen = closer to the viewer, drawn on top.
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = Vector3.up;
        }

        // ---------------------------------------------------------------- ground

        private void BuildGround(GroundGrid ground)
        {
            var gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(_root, false);
            gridObject.transform.localPosition = GridOrigin;
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(CellSize, CellSize, 0f);
            Tilemap floor = CreateTilemap(gridObject.transform, "Floor", GroundOrder);
            Tilemap edges = CreateTilemap(gridObject.transform, "GrassEdges", GroundEdgeOrder);

            var floorCells = new List<Vector3Int>();
            var floorTiles = new List<TileBase>();
            var edgeCells = new List<Vector3Int>();
            var edgeTiles = new List<TileBase>();
            for (int y = ground.MinCell.y; y <= ground.MaxCell.y; y++)
            for (int x = ground.MinCell.x; x <= ground.MaxCell.x; x++)
            {
                (GroundKind floorKind, string edge) = ground.Resolve(x, y);
                var cell = new Vector3Int(x, y, 0);
                Tile floorTile = TileFor(FillName(floorKind, x, y));
                if (floorTile != null)
                {
                    floorCells.Add(cell);
                    floorTiles.Add(floorTile);
                }
                if (edge == null) continue;
                Tile edgeTile = TileFor(edge);
                if (edgeTile == null) continue;
                edgeCells.Add(cell);
                edgeTiles.Add(edgeTile);
            }
            floor.SetTiles(floorCells.ToArray(), floorTiles.ToArray());
            edges.SetTiles(edgeCells.ToArray(), edgeTiles.ToArray());
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int order)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            return tilemap;
        }

        private static string FillName(GroundKind kind, int x, int y)
        {
            int variant = (int)((uint)(x * 73856093 ^ y * 19349663) % 3u);
            return kind switch
            {
                GroundKind.Cobble => "cobble_" + variant,
                GroundKind.Dirt => "dirt_" + variant,
                GroundKind.Brick => "brick_" + variant,
                GroundKind.Water => "water_" + variant,
                _ => "grass_" + variant,
            };
        }

        private Tile TileFor(string name)
        {
            Sprite sprite = LoadSprite(TilePath + name);
            if (sprite == null) return null;
            if (_tiles.TryGetValue(sprite, out Tile tile)) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            _tiles[sprite] = tile;
            return tile;
        }

        // ----------------------------------------------------------------- props

        private SpriteRenderer SpawnProp(string spriteName, Vector2 position, PropLayer layer, bool flip)
        {
            Sprite sprite = LoadSprite(PropPath + spriteName);
            if (sprite == null) return null;
            var propObject = new GameObject(spriteName);
            propObject.transform.SetParent(_root, false);
            propObject.transform.localPosition = position;
            SpriteRenderer renderer = propObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipX = flip;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderer.sortingOrder = layer == PropLayer.Standing ? StandingOrder : DecalOrder;
            return renderer;
        }

        private void SpawnBonfire(Vector2 position)
        {
            SpawnProp("bonfire_base", position, PropLayer.Standing, false);
            // A hair lower than the stones so the flames draw over the ring.
            SpriteRenderer fire = SpawnProp("fire_0", position + new Vector2(0f, -0.02f),
                PropLayer.Standing, false);
            if (fire == null) return;
            var frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = LoadSprite(PropPath + "fire_" + i);
            fire.gameObject.AddComponent<SpriteFrameAnimator>().Initialize(fire, frames, 9f);
        }

        private void Scatter(ScatterRule rule, MapEnvironmentLayout layout, GroundGrid ground,
            System.Random random, List<Vector2> placed)
        {
            Rect area = ZoneBounds(rule.Zone, layout.PlayArea);
            int spawned = 0;
            for (int attempt = 0; attempt < rule.Count * 30 && spawned < rule.Count; attempt++)
            {
                var point = new Vector2(
                    Mathf.Lerp(area.xMin, area.xMax, (float)random.NextDouble()),
                    Mathf.Lerp(area.yMin, area.yMax, (float)random.NextDouble()));
                string sprite = rule.Sprites[random.Next(rule.Sprites.Count)];
                bool flip = random.Next(2) == 0;
                if (!InZone(rule.Zone, layout.PlayArea, point) ||
                    !ground.AllowsProp(point, rule.Layer) ||
                    IsNear(point, layout.KeepClear, KeepClearRadius) ||
                    IsNear(point, placed, rule.Spacing))
                {
                    continue;
                }
                SpawnProp(sprite, point, rule.Layer, flip);
                placed.Add(point);
                spawned++;
            }
        }

        private static Rect ZoneBounds(ScatterZone zone, Rect play)
        {
            Rect outer = Rect.MinMaxRect(play.xMin - OuterMargin.x, play.yMin - OuterMargin.y,
                play.xMax + OuterMargin.x, play.yMax + OuterMargin.y);
            return zone switch
            {
                ScatterZone.Inside => Rect.MinMaxRect(play.xMin + 0.4f, play.yMin + 0.3f,
                    play.xMax - 0.4f, play.yMax - 0.3f),
                ScatterZone.OutsideFront => Rect.MinMaxRect(outer.xMin, outer.yMin,
                    outer.xMax, play.yMin - 0.5f),
                _ => Rect.MinMaxRect(outer.xMin, play.yMin + 1.5f, outer.xMax, outer.yMax),
            };
        }

        private static bool InZone(ScatterZone zone, Rect play, Vector2 point)
        {
            if (zone != ScatterZone.OutsideBehind) return true;
            // Tall trees: keep trunks clear of the walkable rectangle; side
            // canopies are about 1.8 units wide on each side of the trunk.
            bool beside = point.x < play.xMin - 2f || point.x > play.xMax + 2f;
            bool above = point.y > play.yMax + 0.4f;
            return beside || above;
        }

        private static bool IsNear(Vector2 point, List<Vector2> others, float radius)
        {
            float squared = radius * radius;
            foreach (Vector2 other in others)
            {
                if ((other - point).sqrMagnitude < squared)
                    return true;
            }
            return false;
        }

        private Sprite LoadSprite(string path)
        {
            if (_sprites.TryGetValue(path, out Sprite sprite)) return sprite;
            sprite = Resources.Load<Sprite>(path);
            if (sprite == null && _missing.Add(path))
                Debug.LogWarning($"[MapEnvironment] Missing sprite Resources/{path}");
            _sprites[path] = sprite;
            return sprite;
        }

        /// <summary>FNV-1a: string.GetHashCode is not stable across runtimes.</summary>
        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                    hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }

        private void OnDestroy()
        {
            foreach (Tile tile in _tiles.Values)
            {
                if (tile != null)
                    Destroy(tile);
            }
        }
    }
}
