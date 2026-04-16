using System;
using System.Collections.Generic;
using Core;

namespace LevelData
{
    /// <summary>One customer order: a variable-length sequence of tile kinds (one JSON row in <c>orders</c>).</summary>
    public sealed class OrderSpec
    {
        readonly TileKind[] _icons;

        public OrderSpec(IReadOnlyList<TileKind> icons)
        {
            if (icons == null) throw new ArgumentNullException(nameof(icons));
            if (icons.Count < 1) throw new ArgumentException("Order must contain at least one icon.", nameof(icons));

            _icons = new TileKind[icons.Count];
            for (var i = 0; i < icons.Count; i++)
                _icons[i] = icons[i];
        }

        public int Length => _icons.Length;

        /// <summary>Which icon is required next given how many icons already delivered (0 .. Length-1).</summary>
        public TileKind GetRequired(int fulfilledCount) =>
            (uint)fulfilledCount < (uint)_icons.Length ? _icons[fulfilledCount] : TileKind.None;

        public TileKind GetIcon(int index) => _icons[index];

        public IReadOnlyList<TileKind> Icons => _icons;
    }
}
