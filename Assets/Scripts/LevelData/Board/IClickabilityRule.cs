using Core;
using LevelData;

namespace LevelData.Board
{
    public interface IClickabilityRule
    {
        bool IsClickable(PlayableBoardState board, int x, int y, int layer);
    }

    /// <summary>Default rule: tile is blocked when any neighbour exists on a higher layer.</summary>
    public sealed class LayerOcclusionRule : IClickabilityRule
    {
        public static readonly LayerOcclusionRule Instance = new LayerOcclusionRule();

        public bool IsClickable(PlayableBoardState board, int x, int y, int layer)
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
    }
}
