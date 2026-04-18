namespace Gameplay
{
    /// <summary>Read-only progress snapshot from <see cref="LevelObjectiveSession"/> (e.g. at win/lose).</summary>
    public readonly struct GameStatsSnapshot
    {
        public int CompletedOrders { get; }
        public int TotalOrders { get; }
        public int RackTilesHeld { get; }

        public GameStatsSnapshot(int completedOrders, int totalOrders, int rackTilesHeld)
        {
            CompletedOrders = completedOrders;
            TotalOrders = totalOrders;
            RackTilesHeld = rackTilesHeld;
        }

        public static GameStatsSnapshot FromSession(LevelObjectiveSession session)
        {
            if (session == null)
                return default;
            return new GameStatsSnapshot(session.CompletedOrderCount, session.TotalOrderCount, session.RackUsedCount);
        }
    }
}
