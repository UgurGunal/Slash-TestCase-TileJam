using System.Collections.Generic;
using LevelData;

namespace LevelData.Board
{
    /// <summary>Evaluates base rules plus the per-behavior clickability gate resolved from the catalog.</summary>
    public sealed class ClickabilityPipeline
    {
        readonly IReadOnlyList<IClickabilityRule> _baseRules;
        readonly TileBehaviorCatalog _behaviors;

        public ClickabilityPipeline(IReadOnlyList<IClickabilityRule> baseRules, TileBehaviorCatalog behaviors = null)
        {
            _baseRules = baseRules ?? new IClickabilityRule[] { LayerOcclusionRule.Instance };
            _behaviors = behaviors ?? TileBehaviorCatalog.CreateDefault();
        }

        public static ClickabilityPipeline CreateDefault(TileBehaviorCatalog behaviors = null) =>
            new ClickabilityPipeline(new IClickabilityRule[] { LayerOcclusionRule.Instance }, behaviors);

        public bool IsClickable(PlayableBoardState board, int x, int y, int layer)
        {
            if (board == null || !board.HasTile(x, y, layer)) return false;
            var cell = board.GetCell(x, y, layer);
            return IsClickable(board, x, y, layer, cell);
        }

        public bool IsClickable(PlayableBoardState board, int x, int y, int layer, BoardCell cell)
        {
            if (board == null || !cell.HasTile) return false;

            for (var i = 0; i < _baseRules.Count; i++)
            {
                if (!_baseRules[i].IsClickable(board, x, y, layer))
                    return false;
            }

            return _behaviors.Resolve(cell.BehaviorId).IsClickable(board, x, y, layer, cell);
        }
    }
}
