using System.Collections.Generic;
using Core;
using LevelData.Board;

namespace LevelData
{
    /// <summary>Backward-compatible entry point delegating to the default <see cref="Board.ClickabilityPipeline"/>.</summary>
    public static class TileClickability
    {
        static readonly Board.ClickabilityPipeline DefaultPipeline = Board.ClickabilityPipeline.CreateDefault();

        public static void SetPipeline(Board.ClickabilityPipeline pipeline) =>
            _overridePipeline = pipeline;

        static Board.ClickabilityPipeline _overridePipeline;

        static Board.ClickabilityPipeline Pipeline => _overridePipeline ?? DefaultPipeline;

        public static bool IsClickable(PlayableBoardState board, int x, int y, int layer) =>
            Pipeline.IsClickable(board, x, y, layer);

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
