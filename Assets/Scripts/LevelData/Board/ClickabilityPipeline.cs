using System.Collections.Generic;
using LevelData;

namespace LevelData.Board
{
    /// <summary>Evaluates base rules plus per-behavior clickability contributors.</summary>
    public sealed class ClickabilityPipeline
    {
        readonly IReadOnlyList<IClickabilityRule> _baseRules;
        readonly TileBehaviorRegistry _behaviorRegistry;

        public ClickabilityPipeline(
            IReadOnlyList<IClickabilityRule> baseRules,
            TileBehaviorRegistry behaviorRegistry = null)
        {
            _baseRules = baseRules ?? new IClickabilityRule[] { LayerOcclusionRule.Instance };
            _behaviorRegistry = behaviorRegistry;
        }

        public static ClickabilityPipeline CreateDefault(TileBehaviorRegistry registry = null) =>
            new ClickabilityPipeline(new IClickabilityRule[] { LayerOcclusionRule.Instance }, registry);

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

            if (_behaviorRegistry == null) return true;

            var contributors = _behaviorRegistry.GetClickabilityContributors(cell.BehaviorId);
            for (var i = 0; i < contributors.Count; i++)
            {
                var c = contributors[i];
                if (c != null && !c.IsClickable(board, x, y, layer, cell))
                    return false;
            }

            return true;
        }
    }
}
