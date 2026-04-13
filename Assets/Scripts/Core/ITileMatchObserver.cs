namespace Core
{
    /// <summary>
    /// Extension point: UI, audio, analytics, tutorials subscribe without changing session logic.
    /// Add new methods only when new events exist; prefer many small events over one mega-callback.
    /// </summary>
    public interface ITileMatchObserver
    {
        void OnPhaseChanged(GamePhase phase);
        void OnOrderProgress(int orderIndex, int filledSlotsInOrder, bool hasNextRequirement, TileKind nextRequired);
        void OnTileMatchedOrder(TileKind kind);
        void OnTileSentToRack(TileKind kind, int rackCountAfter);
        void OnRackFull();
        void OnVictory();
    }
}
