namespace Gameplay
{
    /// <summary>Gameplay-facing HUD capacity (rack slots and visible order rows). Independent of Unity prefabs.</summary>
    public readonly struct ObjectiveLayoutSpec
    {
        public int RackCapacity { get; }
        public int ActiveOrderSlotCount { get; }

        public ObjectiveLayoutSpec(int rackCapacity, int activeOrderSlotCount)
        {
            RackCapacity = rackCapacity < 1 ? 1 : rackCapacity;
            ActiveOrderSlotCount = activeOrderSlotCount < 1 ? 1 : activeOrderSlotCount;
        }
    }
}
