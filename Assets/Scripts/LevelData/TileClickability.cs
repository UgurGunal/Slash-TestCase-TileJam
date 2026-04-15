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
    }
}
