using System.Collections.Generic;
using Core;

namespace LevelData
{
    /// <summary>
    /// Layout constraints:
    /// <list type="bullet">
    /// <item><b>Same layer:</b> no two tiles may occupy cells with both |Δrow| ≤ 1 and |Δcol| ≤ 1 (includes 8-neighbourhood + self overlap is already one tile per cell).</item>
    /// <item><b>Consecutive layers:</b> no tile at (x,y,l) and another at (x,y,l+1) — no vertical stack in the same column across adjacent layers.</item>
    /// <item><b>No floating:</b> on layer &gt; 0, every tile must have at least one tile on layer <c>l-1</c> in some cell with |Δcol|≤1 and |Δrow|≤1 (8-neighbourhood on the layer below).</item>
    /// </list>
    /// </summary>
    public static class LevelLayoutRules
    {
        public static bool Validate(LevelBoardSpec spec, out string error)
        {
            error = null;
            if (spec == null)
            {
                error = "Spec is null.";
                return false;
            }

            if (!ValidateNoAdjacentSameLayer(spec, out error)) return false;
            if (!ValidateNoStackedConsecutiveLayers(spec, out error)) return false;
            if (!ValidateNoFloatingTiles(spec, out error)) return false;
            return true;
        }

        /// <summary>Any two tiles on the same layer must have Chebyshev distance ≥ 2 (not 8-adjacent).</summary>
        static bool ValidateNoAdjacentSameLayer(LevelBoardSpec spec, out string error)
        {
            error = null;
            for (var l = 0; l < spec.Depth; l++)
            {
                var positions = new List<(int x, int y)>();
                for (var y = 0; y < spec.Height; y++)
                for (var x = 0; x < spec.Width; x++)
                {
                    if (spec.TryGet(x, y, l, out _))
                        positions.Add((x, y));
                }

                for (var i = 0; i < positions.Count; i++)
                for (var j = i + 1; j < positions.Count; j++)
                {
                    var a = positions[i];
                    var b = positions[j];
                    var dx = System.Math.Abs(a.x - b.x);
                    var dy = System.Math.Abs(a.y - b.y);
                    if (dx <= 1 && dy <= 1)
                    {
                        error =
                            $"Same-layer adjacency: tiles at ({a.x},{a.y}) and ({b.x},{b.y}) on layer {l} violate the rule (|Δcol|≤1 and |Δrow|≤1).";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>No tile at (x,y,l) and (x,y,l+1) for any x,y,l.</summary>
        static bool ValidateNoStackedConsecutiveLayers(LevelBoardSpec spec, out string error)
        {
            error = null;
            for (var y = 0; y < spec.Height; y++)
            for (var x = 0; x < spec.Width; x++)
            {
                for (var l = 0; l < spec.Depth - 1; l++)
                {
                    var a = spec.TryGet(x, y, l, out _);
                    var b = spec.TryGet(x, y, l + 1, out _);
                    if (a && b)
                    {
                        error =
                            $"Vertical stack: tiles at column {x}, row {y} on consecutive layers {l} and {l + 1} cannot share the same cell.";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Layer 0 is the ground. Any tile on layer l &gt; 0 must have at least one tile on layer l-1
        /// in the 3×3 neighbourhood (|Δcol|≤1 and |Δrow|≤1). Together with the no-stack rule,
        /// support always comes from a neighbour below, not the same (col,row).
        /// </summary>
        static bool ValidateNoFloatingTiles(LevelBoardSpec spec, out string error)
        {
            error = null;
            for (var l = 1; l < spec.Depth; l++)
            {
                for (var y = 0; y < spec.Height; y++)
                for (var x = 0; x < spec.Width; x++)
                {
                    if (!spec.TryGet(x, y, l, out _)) continue;

                    var hasSupport = false;
                    for (var dy = -1; dy <= 1 && !hasSupport; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (spec.TryGet(x + dx, y + dy, l - 1, out _))
                            hasSupport = true;
                    }

                    if (!hasSupport)
                    {
                        error =
                            $"Floating tile: cell ({x},{y}) on layer {l} has no tile underneath in a neighbouring cell on layer {l - 1} (need |Δcol|≤1 and |Δrow|≤1).";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
