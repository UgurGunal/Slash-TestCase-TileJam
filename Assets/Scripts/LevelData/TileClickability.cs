using Core;

namespace LevelData
{
    /// <summary>
    /// Which tiles can be removed: blocked if <b>any</b> tile exists in the 8-neighbourhood (|Δx|≤1, |Δy|≤1, including same cell)
    /// on <b>any</b> layer strictly above this tile (not only the layer immediately above).
    /// </summary>
    public static class TileClickability
    {
        public static bool IsClickable(PlayableBoardState board, int x, int y, int layer)
        {
            if (board == null || !board.HasTile(x, y, layer)) return false;
            if (layer >= board.Depth - 1) return true;

            for (var lz = layer + 1; lz < board.Depth; lz++)
            {
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if ((uint)nx >= (uint)board.Width || (uint)ny >= (uint)board.Height) continue;
                    if (board.HasTile(nx, ny, lz)) return false;
                }
            }

            return true;
        }

        /// <summary>Same rules as <see cref="IsClickable(PlayableBoardState,int,int,int)"/> for level-editor placement grids (<c>x,y</c> = slot indices, <c>z</c> = stack layer).</summary>
        public static bool IsClickable(TileKind?[, ,] cells, int x, int y, int layer)
        {
            if (cells == null) return false;
            var w = cells.GetLength(0);
            var h = cells.GetLength(1);
            var d = cells.GetLength(2);
            if ((uint)x >= (uint)w || (uint)y >= (uint)h || (uint)layer >= (uint)d || !cells[x, y, layer].HasValue)
                return false;
            if (layer >= d - 1) return true;

            for (var lz = layer + 1; lz < d; lz++)
            {
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
                    if (cells[nx, ny, lz].HasValue) return false;
                }
            }

            return true;
        }
    }
}
