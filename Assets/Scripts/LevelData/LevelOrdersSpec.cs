using System;
using System.Collections.Generic;

namespace LevelData
{
    /// <summary>All orders for a level. Total icons in all orders must equal the number of tiles on the board.</summary>
    public sealed class LevelOrdersSpec
    {
        public LevelOrdersSpec(IReadOnlyList<OrderSpec> orders)
        {
            Orders = orders ?? throw new ArgumentNullException(nameof(orders));
            if (Orders.Count < 1) throw new ArgumentException("At least one order is required.", nameof(orders));

            var max = 0;
            for (var i = 0; i < Orders.Count; i++)
                max = Math.Max(max, Orders[i].Length);
            MaxIconsInAnyOrder = max;
            if (MaxIconsInAnyOrder < 1) throw new ArgumentException("Each order must have at least one icon.");
        }

        public IReadOnlyList<OrderSpec> Orders { get; }

        /// <summary>Longest order in this level — use to size UI strips (each slot shows up to this many icon cells).</summary>
        public int MaxIconsInAnyOrder { get; }

        public int OrderCount => Orders.Count;

        /// <summary>Sum of <see cref="OrderSpec.Length"/> across all orders (must match board tile count).</summary>
        public int TotalOrderIcons()
        {
            var n = 0;
            for (var i = 0; i < Orders.Count; i++)
                n += Orders[i].Length;
            return n;
        }
    }
}
