using System.Collections.Generic;

namespace LevelData.Board
{
    /// <summary>Evaluates registered <see cref="IClickabilityRule"/> instances (all must pass).</summary>
    public static class ClickabilityService
    {
        static readonly IClickabilityRule[] DefaultRules = { LayerOcclusionRule.Instance };

        public static bool IsClickable(PlayableBoardState board, int x, int y, int layer)
        {
            for (var i = 0; i < DefaultRules.Length; i++)
            {
                if (!DefaultRules[i].IsClickable(board, x, y, layer))
                    return false;
            }

            return true;
        }

        public static bool IsClickable(PlayableBoardState board, int x, int y, int layer, IEnumerable<IClickabilityRule> extraRules)
        {
            if (!IsClickable(board, x, y, layer)) return false;
            if (extraRules == null) return true;
            foreach (var rule in extraRules)
            {
                if (rule != null && !rule.IsClickable(board, x, y, layer))
                    return false;
            }

            return true;
        }
    }
}
