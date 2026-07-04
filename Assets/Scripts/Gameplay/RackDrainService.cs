using Core;

namespace Gameplay
{
    /// <summary>Applies rack→order auto-match steps against <see cref="IRackDrainHost"/>.</summary>
    public sealed class RackDrainService
    {
        public bool TryPeekStep(IRackDrainHost host, out int rackIndex, out TileKind kind, out TileCollectDestination orderDestination)
        {
            rackIndex = -1;
            kind = default;
            orderDestination = default;
            if (host == null || host.IsDrainInactive) return false;

            var rack = host.Rack;
            var orders = host.OrderSlots;
            for (var i = 0; i < rack.Count; i++)
            {
                var k = rack.GetSlot(i).Value;
                if (!orders.FindFirstUnfilledOrderMatch(k, out var slot, out var iconIdx, out _))
                    continue;
                rackIndex = i;
                kind = k;
                orderDestination = TileCollectDestination.ForOrderSlot(slot, iconIdx);
                return true;
            }

            return false;
        }

        public TileCollectResult ApplyStepAt(IRackDrainHost host, int rackIndex)
        {
            if (host == null || host.IsDrainInactive)
                return host != null && host.OrderSlots.HasWon
                    ? TileCollectResult.LevelWon
                    : TileCollectResult.ConsumedForOrder;

            var rack = host.Rack;
            if ((uint)rackIndex >= (uint)rack.Count)
                return TileCollectResult.ConsumedForOrder;

            var kind = rack.GetSlot(rackIndex).Value;
            if (!host.OrderSlots.TryFulfillIcon(kind, CollectApplySource.FromRack, host.CollectFlowLogger, out var completedWholeOrder, out var r))
                return TileCollectResult.ConsumedForOrder;

            if (host.CollectFlowLogger.IsEnabled)
                host.CollectFlowLogger.Log($"[TileCollect] Rack auto: consumed rack slot {rackIndex} ({kind}) — removed from rack and applied to orders.");

            rack.RemoveAt(rackIndex);
            PublishCollectEvents(host, kind, r, completedWholeOrder);
            host.RaiseStateChanged();
            return r;
        }

        static void PublishCollectEvents(IRackDrainHost host, TileKind kind, TileCollectResult result, bool completedWholeOrder)
        {
            if (result == TileCollectResult.LevelWon)
                host.EventBus.Publish(new VictoryEvent());

            if (completedWholeOrder)
                host.EventBus.Publish(new OrderCompletedEvent(kind, fromRack: true));
            else
                host.EventBus.Publish(new TileMatchedOrderEvent(kind));
        }
    }
}
