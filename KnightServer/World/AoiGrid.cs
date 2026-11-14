using System.Numerics;

namespace KnightOnline.Server.World;

/// <summary>
/// Area-of-interest rule on a square grid: an observer sees entities on the
/// same map whose cell is within one cell (3x3 neighbourhood) of its own.
/// Pure and allocation-free so it can be unit tested and called per tick.
/// </summary>
public readonly struct AoiGrid
{
    public AoiGrid(float cellSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        CellSize = cellSize;
    }

    public float CellSize { get; }

    public (int X, int Y) CellOf(Vector2 position) =>
        ((int)MathF.Floor(position.X / CellSize),
         (int)MathF.Floor(position.Y / CellSize));

    public bool IsVisible(string observerMap, Vector2 observerPosition,
        string targetMap, Vector2 targetPosition)
    {
        if (!string.Equals(observerMap, targetMap, StringComparison.Ordinal))
            return false;

        (int ox, int oy) = CellOf(observerPosition);
        (int tx, int ty) = CellOf(targetPosition);
        return Math.Abs(ox - tx) <= 1 && Math.Abs(oy - ty) <= 1;
    }
}
