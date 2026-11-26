using UnityEngine;

namespace KnightOnline.Client.Gameplay.World.Environment
{
    /// <summary>
    /// Cell lookup for a layout's ground, plus the grass-edge autotiling rule.
    /// Grass is drawn as an overlay: wherever grass meets another floor, a
    /// ragged grass edge tile (Floors_Tiles "flat" cross, named
    /// grass_edge_RC with R/C its row/column in that 5x5 cross) is laid on top
    /// of the other floor's fill.
    /// </summary>
    public sealed class GroundGrid
    {
        private readonly MapEnvironmentLayout _layout;
        private readonly float _cellSize;
        private readonly Vector2 _origin;

        public GroundGrid(MapEnvironmentLayout layout, Vector2 outerMargin, float cellSize,
            Vector2 origin)
        {
            _layout = layout;
            _cellSize = cellSize;
            _origin = origin;
            Rect play = layout.PlayArea;
            MinCell = CellAt(new Vector2(play.xMin - outerMargin.x, play.yMin - outerMargin.y));
            MaxCell = CellAt(new Vector2(play.xMax + outerMargin.x, play.yMax + outerMargin.y));
        }

        public Vector2Int MinCell { get; }
        public Vector2Int MaxCell { get; }

        public Vector2Int CellAt(Vector2 world) => new(
            Mathf.FloorToInt((world.x - _origin.x) / _cellSize),
            Mathf.FloorToInt((world.y - _origin.y) / _cellSize));

        /// <summary>Floor of a cell: the last patch containing the cell centre wins.</summary>
        public GroundKind KindAt(int x, int y)
        {
            var centre = new Vector2(
                _origin.x + (x + 0.5f) * _cellSize,
                _origin.y + (y + 0.5f) * _cellSize);
            GroundKind kind = GroundKind.Grass;
            foreach (GroundPatch patch in _layout.Patches)
            {
                Rect area = patch.Area;
                if (centre.x >= area.xMin - 0.01f && centre.x <= area.xMax + 0.01f &&
                    centre.y >= area.yMin - 0.01f && centre.y <= area.yMax + 0.01f)
                {
                    kind = patch.Kind;
                }
            }
            return kind;
        }

        /// <summary>The fill drawn on a cell and the grass edge drawn over it (or null).</summary>
        public (GroundKind Floor, string Edge) Resolve(int x, int y)
        {
            GroundKind kind = KindAt(x, y);
            if (kind != GroundKind.Grass)
            {
                bool north = IsGrass(x, y + 1);
                bool south = IsGrass(x, y - 1);
                bool west = IsGrass(x - 1, y);
                bool east = IsGrass(x + 1, y);
                if (!north && !south && !west && !east)
                    return (kind, null);
                int row = north ? 1 : south ? 3 : 2;
                int column = west ? 1 : east ? 3 : 2;
                return (kind, EdgeName(row, column));
            }

            // Grass cell bordering another floor: draw that floor below the edge.
            if (!IsGrass(x, y - 1))
                return (KindAt(x, y - 1), EdgeName(0, EndVariant(x, y - 1, horizontal: true)));
            if (!IsGrass(x, y + 1))
                return (KindAt(x, y + 1), EdgeName(4, EndVariant(x, y + 1, horizontal: true)));
            if (!IsGrass(x + 1, y))
                return (KindAt(x + 1, y), EdgeName(EndVariant(x + 1, y, horizontal: false), 0));
            if (!IsGrass(x - 1, y))
                return (KindAt(x - 1, y), EdgeName(EndVariant(x - 1, y, horizontal: false), 4));
            return (GroundKind.Grass, null);
        }

        /// <summary>
        /// Props stand only on grass (and not hugging a road edge); flat
        /// decals may also lie on dirt.
        /// </summary>
        public bool AllowsProp(Vector2 world, PropLayer layer)
        {
            Vector2Int cell = CellAt(world);
            GroundKind kind = KindAt(cell.x, cell.y);
            if (layer == PropLayer.Decal)
                return kind is GroundKind.Grass or GroundKind.Dirt;
            return kind == GroundKind.Grass &&
                   IsGrass(cell.x + 1, cell.y) && IsGrass(cell.x - 1, cell.y) &&
                   IsGrass(cell.x, cell.y + 1) && IsGrass(cell.x, cell.y - 1);
        }

        private bool IsGrass(int x, int y) => KindAt(x, y) == GroundKind.Grass;

        /// <summary>
        /// 1 / 3 when the bordering floor cell is the first / last of its run
        /// (rounded ends of the cross), 2 in the middle.
        /// </summary>
        private int EndVariant(int x, int y, bool horizontal)
        {
            if (horizontal)
                return IsGrass(x - 1, y) ? 1 : IsGrass(x + 1, y) ? 3 : 2;
            // Rows of the cross go top (1) to bottom (3); world y grows upward.
            return IsGrass(x, y + 1) ? 1 : IsGrass(x, y - 1) ? 3 : 2;
        }

        private static string EdgeName(int row, int column) => $"grass_edge_{row}{column}";
    }
}
