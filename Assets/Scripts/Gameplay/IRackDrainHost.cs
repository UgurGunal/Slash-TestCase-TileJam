namespace Gameplay
{
    /// <summary>Gameplay slice required for rack→order drain steps.</summary>
    public interface IRackDrainHost
    {
        RackState Rack { get; }
        ActiveOrderSlots OrderSlots { get; }
        ICollectFlowLogger CollectFlowLogger { get; }
        IGameplayEventBus EventBus { get; }
        bool IsDrainInactive { get; }
        void RaiseStateChanged();
    }
}
