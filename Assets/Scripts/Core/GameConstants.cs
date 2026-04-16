namespace Core
{
    /// <summary>Fixed rules from the design brief — reference these instead of magic numbers.</summary>
    public static class GameConstants
    {
        public const int RackCapacity = 6;

        /// <summary>
        /// How many order strips are active at once. This build uses 3 (each strip shows one order’s icons).
        /// For other layouts (2 / 4 / 5 strips), change this or use a separate build/prefab that expects a different count.
        /// </summary>
        public const int ActiveOrderSlotsCount = 3;

        /// <summary>Playable icon types (<see cref="TileKind.Type0"/> … <see cref="TileKind.Type14"/>), excluding <see cref="TileKind.None"/>.</summary>
        public const int PlayableTileKindCount = 15;
    }
}
