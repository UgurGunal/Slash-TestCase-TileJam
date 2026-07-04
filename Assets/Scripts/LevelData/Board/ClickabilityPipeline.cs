using System.Collections.Generic;
using LevelData;

namespace LevelData.Board
{
    /// <summary>Evaluates base rules plus per-behavior clickability contributors.</summary>
    public sealed class ClickabilityPipeline
    {
        readonly IReadOnlyList<IClickabilityRule> _baseRules;

        public ClickabilityPipeline(IReadOnlyList<IClickabilityRule> baseRules)
        {
            _baseRules = baseRules ?? new IClickabilityRule[] { LayerOcclusionRule.Instance };
        }

        public static ClickabilityPipeline CreateDefault() =>
            new ClickabilityPipeline(new IClickabilityRule[] { LayerOcclusionRule.Instance });

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

            var contributors = TileBehaviorContributorProvider.GetClickabilityContributors(cell.BehaviorId);
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
