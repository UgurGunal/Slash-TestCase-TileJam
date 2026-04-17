using Core;

namespace LevelData
{
    /// <summary>Which tiles can be removed: blocked if any tile sits in the 8-neighbourhood (or same cell) on <c>layer + 1</c>.</summary>
    public static class TileClickability
    {
        public static bool IsClickable(PlayableBoardState board, int x, int y, int layer)
        {
            if (board == null || !board.HasTile(x, y, layer)) return false;
            if (layer >= board.Depth - 1) return true;

            var above = layer + 1;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = x + dx;
                var ny = y + dy;
                if ((uint)nx >= (uint)board.Width || (uint)ny >= (uint)board.Height) continue;
                if (board.HasTile(nx, ny, above)) return false;
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

            var above = layer + 1;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = x + dx;
                var ny = y + dy;
                if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
                if (cells[nx, ny, above].HasValue) return false;
            }

            return true;
        }
    }
}
