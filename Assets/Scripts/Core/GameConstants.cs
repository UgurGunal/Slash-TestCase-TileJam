namespace Core
{
    /// <summary>Fixed rules from the design brief — reference these instead of magic numbers.</summary>
    public static class GameConstants
    {
        public const int OrderIconCount = 3;
        public const int RackCapacity = 6;

        /// <summary>Playable icon types (<see cref="TileKind.Type0"/> … <see cref="TileKind.Type14"/>), excluding <see cref="TileKind.None"/>.</summary>
        public const int PlayableTileKindCount = 15;
    }
}
