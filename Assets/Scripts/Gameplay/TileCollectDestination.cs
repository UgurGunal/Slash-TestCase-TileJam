namespace Gameplay
{
    public enum CollectTargetKind
    {
        OrderSlot,
        RackSlot,
    }

    /// <summary>Domain destination for a collected tile — order cell or rack slot (no presentation semantics).</summary>
    public readonly struct TileCollectDestination
    {
        public CollectTargetKind Kind { get; }
        public int ActiveOrderSlotIndex { get; }
        public int OrderIconIndex { get; }
        public int RackSlotIndex { get; }

        TileCollectDestination(CollectTargetKind kind, int orderSlot, int iconIdx, int rackSlot)
        {
            Kind = kind;
            ActiveOrderSlotIndex = orderSlot;
            OrderIconIndex = iconIdx;
            RackSlotIndex = rackSlot;
        }

        public static TileCollectDestination ForOrderSlot(int activeOrderSlotIndex, int iconIndexInOrder) =>
            new TileCollectDestination(CollectTargetKind.OrderSlot, activeOrderSlotIndex, iconIndexInOrder, -1);

        public static TileCollectDestination ForRackSlot(int nextEmptySlotIndex) =>
            new TileCollectDestination(CollectTargetKind.RackSlot, -1, -1, nextEmptySlotIndex);
    }
}
