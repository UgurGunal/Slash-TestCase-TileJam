using System;
using Core;
using Gameplay.Collect;
using LevelData;
using LevelData.Board;

namespace Gameplay
{
    /// <summary>Facade for order queue, rack, and board collect rules.</summary>
    public sealed class LevelObjectiveSession : IRackDrainHost
    {
        readonly ActiveOrderSlots _orderSlots;
        readonly RackState _rack;
        readonly IGameplayEventBus _eventBus;
        readonly CollectSessionContext _collectContext;
        readonly CollectPipeline _collectPipeline;
        bool _failed;

        public LevelObjectiveSession(
            LevelOrdersSpec orders,
            IGameplayEventBus eventBus = null,
            CollectPipeline collectPipeline = null)
        {
            _orderSlots = new ActiveOrderSlots(orders);
            _rack = new RackState();
            _eventBus = eventBus ?? NullGameplayEventBus.Instance;
            _collectContext = new CollectSessionContext(_orderSlots, _rack, NullCollectFlowLogger.Instance, _eventBus);
            _collectPipeline = collectPipeline ?? CollectPipeline.CreateDefault();

            _orderSlots.ActiveOrderSlotAdvanced += slot =>
                _eventBus.Publish(new OrderSlotAdvancedEvent(slot));
        }

        public ICollectFlowLogger CollectFlowLogger
        {
            get => _collectContext.Logger;
            set => _collectContext.Logger = value ?? NullCollectFlowLogger.Instance;
        }

        public event Action StateChanged;

        public event Action<int> ActiveOrderSlotAdvanced
        {
            add => _orderSlots.ActiveOrderSlotAdvanced += value;
            remove => _orderSlots.ActiveOrderSlotAdvanced -= value;
        }

        RackState IRackDrainHost.Rack => _rack;
        ActiveOrderSlots IRackDrainHost.OrderSlots => _orderSlots;
        ICollectFlowLogger IRackDrainHost.CollectFlowLogger => CollectFlowLogger;
        IGameplayEventBus IRackDrainHost.EventBus => _eventBus;
        bool IRackDrainHost.IsDrainInactive => _failed || HasWon;

        public bool HasFailed => _failed || _collectContext.Failed;
        public bool HasWon => _orderSlots.HasWon;
        public int RackUsedCount => _rack.Count;
        public int CompletedOrderCount => _orderSlots.CompletedOrders;
        public int TotalOrderCount => _orderSlots.TotalOrders;
        public int MaxOrderIconsOnLevel => _orderSlots.MaxOrderIconsOnLevel;

        public TileKind? GetRackSlot(int index) => _rack.GetSlot(index);

        public bool GetActiveSlot(int slot, out int levelOrderIndex, out OrderSpec orderSpec, out bool[] cellsFulfilled) =>
            _orderSlots.GetActiveSlot(slot, out levelOrderIndex, out orderSpec, out cellsFulfilled);

        public bool IsSlotIdle(int slot) => _orderSlots.IsSlotIdle(int slot);

        public void NotifyStateChanged() => RaiseStateChanged();

        void IRackDrainHost.RaiseStateChanged() => RaiseStateChanged();

        public bool TryPeekCollectDestination(TileKind kind, out TileCollectDestination destination, out TileCollectResult failureReason)
        {
            destination = default;
            failureReason = TileCollectResult.ConsumedForOrder;

            if (_failed || HasWon)
            {
                failureReason = TileCollectResult.SessionInactive;
                return false;
            }

            if (_orderSlots.FindFirstUnfilledOrderMatch(kind, out var slot, out var iconIdx, out _))
            {
                destination = TileCollectDestination.ForOrderSlot(slot, iconIdx);
                return true;
            }

            if (_rack.IsFull)
            {
                failureReason = TileCollectResult.FailedRackFull;
                return false;
            }

            destination = TileCollectDestination.ForRackSlot(_rack.Count);
            return true;
        }

        public TileCollectResult TryCollectTile(TileKind kind) =>
            TryCollectTile(BoardCell.FromKind(kind));

        public TileCollectResult TryCollectTile(BoardCell cell)
        {
            var result = _collectPipeline.Execute(cell, _collectContext);
            _failed = _collectContext.Failed;
            if (result != TileCollectResult.SessionInactive)
                RaiseStateChanged();
            return result;
        }

        void RaiseStateChanged() => StateChanged?.Invoke();
    }
}
