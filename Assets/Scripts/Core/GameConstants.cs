namespace Core
{
    /// <summary>Fixed rules from the design brief — reference these instead of magic numbers.</summary>
    public static class GameConstants
    {
        public const int RackCapacity = 6;

        /// <summary>Default: 2 parallel customers (two HUD order strips). Increase for 3+ strips.</summary>
        public const int ActiveOrderSlotsCount = 2;

        /// <summary>Playable icon types (<see cref="TileKind.Type0"/> … <see cref="TileKind.Type14"/>), excluding <see cref="TileKind.None"/>.</summary>
        public const int PlayableTileKindCount = 15;
    }
}
