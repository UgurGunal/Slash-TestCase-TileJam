using Core;

namespace Gameplay.Collect
{
    /// <summary>Default collect behaviour: match active orders or add to rack.</summary>
    public sealed class MatchOrRackCollectHandler : ICollectHandler
    {
        public static readonly MatchOrRackCollectHandler Instance = new MatchOrRackCollectHandler();

        public int Priority => 0;

        public bool CanHandle(TileKind kind, CollectSessionContext context) =>
            kind != TileKind.None && context != null && !context.IsInactive;

        public TileCollectResult Handle(TileKind kind, CollectSessionContext context)
        {
            if (context.Logger.IsEnabled)
                context.Logger.Log($"[TileCollect] Click {kind} — start (rack before: {context.Rack.Count} tile(s)).");

            if (context.IsInactive)
            {
                if (context.Logger.IsEnabled)
                    context.Logger.Log($"[TileCollect] Click {kind} — ignored: session already {(context.OrderSlots.HasWon ? "won" : "failed (rack full)")}.");
                return TileCollectResult.SessionInactive;
            }

            if (context.OrderSlots.TryFulfillIcon(kind, CollectApplySource.FromBoardClick, context.Logger, out var completedWholeOrder, out var applyResult))
            {
                PublishCollectEvents(context, kind, applyResult, completedWholeOrder, fromRack: false);

                if (applyResult == TileCollectResult.LevelWon)
                    return TileCollectResult.LevelWon;

                if (completedWholeOrder)
                {
                    if (context.Logger.IsEnabled)
                        context.Logger.Log($"[TileCollect] After click {kind}: order completed — rack drain deferred to presentation.");
                    return TileCollectResult.OrderCompleted;
                }

                return TileCollectResult.ConsumedForOrder;
            }

            if (context.Rack.IsFull)
            {
                context.Failed = true;
                context.EventBus.Publish(new RackFullEvent());
                if (context.Logger.IsEnabled)
                    context.Logger.Log($"[TileCollect] Click {kind} — no matching order; rack full → level failed.");
                return TileCollectResult.FailedRackFull;
            }

            context.Rack.TryAdd(kind);
            if (context.Rack.IsFull)
            {
                context.Failed = true;
                context.EventBus.Publish(new RackFullEvent());
                if (context.Logger.IsEnabled)
                    context.Logger.Log($"[TileCollect] Click {kind} — rack filled to capacity ({context.Rack.Count}/{GameConstants.RackCapacity}) → level failed.");
            }
            else if (context.Logger.IsEnabled)
                context.Logger.Log($"[TileCollect] Click {kind} — no matching order → added to rack at index {context.Rack.Count - 1} (rack now {context.Rack.Count} tile(s)).");

            context.EventBus.Publish(new TileSentToRackEvent(kind, context.Rack.Count));
            return TileCollectResult.AddedToRack;
        }

        static void PublishCollectEvents(CollectSessionContext context, TileKind kind, TileCollectResult result, bool completedWholeOrder, bool fromRack)
        {
            if (result == TileCollectResult.LevelWon)
                context.EventBus.Publish(new VictoryEvent());

            if (completedWholeOrder)
                context.EventBus.Publish(new OrderCompletedEvent(kind, fromRack));
            else
                context.EventBus.Publish(new TileMatchedOrderEvent(kind));
        }
    }
}
