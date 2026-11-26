using System.Collections.Generic;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.World.Environment
{
    /// <summary>Floor material of one ground cell. Grass is the default.</summary>
    public enum GroundKind
    {
        Grass,
        Cobble,
        Dirt,
        Brick,
        Water,
    }

    /// <summary>
    /// Draw order of a prop. <see cref="Decal"/> lies flat under every
    /// character; <see cref="Standing"/> is Y-sorted against the hero.
    /// </summary>
    public enum PropLayer
    {
        Decal,
        Standing,
    }

    /// <summary>A rectangle of non-grass floor, in world units.</summary>
    public readonly struct GroundPatch
    {
        public GroundPatch(GroundKind kind, float minX, float minY, float maxX, float maxY)
        {
            Kind = kind;
            Area = Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public GroundKind Kind { get; }
        public Rect Area { get; }
    }

    /// <summary>One hand-placed prop. Sprite names live under World/PixelCrawler/Props.</summary>
    public readonly struct PropPlacement
    {
        public PropPlacement(string sprite, float x, float y, PropLayer layer = PropLayer.Standing,
            bool flip = false)
        {
            Sprite = sprite;
            Position = new Vector2(x, y);
            Layer = layer;
            Flip = flip;
        }

        public string Sprite { get; }
        public Vector2 Position { get; }
        public PropLayer Layer { get; }
        public bool Flip { get; }
    }

    /// <summary>Where a scatter rule may place props, relative to the play area.</summary>
    public enum ScatterZone
    {
        /// <summary>Inside the server's walkable rectangle.</summary>
        Inside,
        /// <summary>Above or beside the walkable rectangle: tall props are fine.</summary>
        OutsideBehind,
        /// <summary>Below the walkable rectangle: only short props, they would cover the hero.</summary>
        OutsideFront,
    }

    /// <summary>Randomly (but deterministically) spreads props over a zone.</summary>
    public sealed class ScatterRule
    {
        public ScatterRule(ScatterZone zone, PropLayer layer, int count, float spacing,
            params string[] sprites)
        {
            Zone = zone;
            Layer = layer;
            Count = count;
            Spacing = spacing;
            Sprites = sprites;
        }

        public ScatterZone Zone { get; }
        public PropLayer Layer { get; }
        public int Count { get; }
        /// <summary>Minimum distance to other props placed on the same layer.</summary>
        public float Spacing { get; }
        public IReadOnlyList<string> Sprites { get; }
    }

    /// <summary>
    /// Visual dressing of one map. Presentation only: the server's walkable
    /// rectangle (MapCatalog) is unchanged and nothing here blocks movement.
    /// </summary>
    public sealed class MapEnvironmentLayout
    {
        public MapEnvironmentLayout(Rect playArea, Color backdrop)
        {
            PlayArea = playArea;
            Backdrop = backdrop;
        }

        /// <summary>Must match the server map bounds (serverSettings.json).</summary>
        public Rect PlayArea { get; }
        public Color Backdrop { get; }
        public List<GroundPatch> Patches { get; } = new();
        public List<PropPlacement> Props { get; } = new();
        public List<Vector2> Bonfires { get; } = new();
        public List<ScatterRule> Scatters { get; } = new();
        /// <summary>Spots (spawns, NPCs, portals) where scattered props must not land.</summary>
        public List<Vector2> KeepClear { get; } = new();
    }
}
