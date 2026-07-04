namespace Gameplay
{
    public enum TileCollectResult
    {
        ConsumedForOrder,
        AddedToRack,
        FailedRackFull,
        LevelWon,
        /// <summary>Level already won or lost — tile was not collected; board tile must stay.</summary>
        SessionInactive,
        /// <summary>An entire customer order was completed; presentation may drain matching rack tiles.</summary>
        OrderCompleted,
    }
}
