namespace Core
{
    public sealed class NullTileMatchObserver : ITileMatchObserver
    {
        public void OnPhaseChanged(GamePhase phase) { }
        public void OnOrderProgress(int orderIndex, int filledSlotsInOrder, bool hasNextRequirement, TileKind nextRequired) { }
        public void OnTileMatchedOrder(TileKind kind) { }
        public void OnTileSentToRack(TileKind kind, int rackCountAfter) { }
        public void OnRackFull() { }
        public void OnVictory() { }
    }
}
