using Core;

namespace Gameplay.Collect
{
    /// <summary>Mutable session slice passed through collect handlers.</summary>
    public sealed class CollectSessionContext
    {
        public ActiveOrderSlots OrderSlots { get; }
        public RackState Rack { get; }
        public ICollectFlowLogger Logger { get; set; }
        public IGameplayEventBus EventBus { get; }
        public bool Failed { get; set; }

        public CollectSessionContext(
            ActiveOrderSlots orderSlots,
            RackState rack,
            ICollectFlowLogger logger,
            IGameplayEventBus eventBus)
        {
            OrderSlots = orderSlots;
            Rack = rack;
            Logger = logger ?? NullCollectFlowLogger.Instance;
            EventBus = eventBus ?? NullGameplayEventBus.Instance;
        }

        public bool IsInactive => Failed || OrderSlots.HasWon;
    }

    public interface ICollectHandler
    {
        int Priority { get; }
        bool CanHandle(TileKind kind, CollectSessionContext context);
        TileCollectResult Handle(TileKind kind, CollectSessionContext context);
    }
}
