using System;
using Core;
using Gameplay.Collect;
using LevelData;
using LevelData.Board;

namespace Gameplay
{
    /// <summary>Facade for order queue, rack, and board collect rules.</summary>
    public sealed class LevelObjectiveSession : IRackDrainHost, IObjectiveHudState
    {
        readonly ActiveOrderSlots _orderSlots;
        readonly RackState _rack;
        readonly IGameplayEventBus _eventBus;
        readonly CollectSessionContext _collectContext;
        readonly CollectPipeline _collectPipeline;
        readonly CollectReservationService _reservations = new CollectReservationService();
        bool _failed;

        public LevelObjectiveSession(
            LevelOrdersSpec orders,
            IGameplayEventBus eventBus = null,
            CollectPipeline collectPipeline = null,
            int rackCapacity = GameConstants.RackCapacity,
            int activeOrderSlotCount = GameConstants.ActiveOrderSlotsCount)
        {
            _orderSlots = new ActiveOrderSlots(orders, activeOrderSlotCount);
            _rack = new RackState(rackCapacity);
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

        RackState IRackDrainHost.Rack => _rack;
        ActiveOrderSlots IRackDrainHost.OrderSlots => _orderSlots;
        ICollectFlowLogger IRackDrainHost.CollectFlowLogger => CollectFlowLogger;
        IGameplayEventBus IRackDrainHost.EventBus => _eventBus;
        bool IRackDrainHost.IsDrainInactive => _failed || HasWon;

        public bool HasFailed => _failed || _collectContext.Failed;
        public bool HasWon => _orderSlots.HasWon;
        public int MaxOrderIconsOnLevel => _orderSlots.MaxOrderIconsOnLevel;

        public TileKind? GetRackSlot(int index) => _rack.GetSlot(index);

        public bool GetActiveSlot(int slot, out int levelOrderIndex, out OrderSpec orderSpec, out bool[] cellsFulfilled) =>
            _orderSlots.GetActiveSlot(slot, out levelOrderIndex, out orderSpec, out cellsFulfilled);

        public bool IsSlotIdle(int slot) => _orderSlots.IsSlotIdle(slot);

        bool IObjectiveHudState.IsOrderSlotIdle(int hudSlotIndex) => IsSlotIdle(hudSlotIndex);

        public bool TryGetOrderRow(int hudSlotIndex, out ObjectiveHudRowView row)
        {
            if (GetActiveSlot(hudSlotIndex, out var levelOrderIndex, out var orderSpec, out var cellsFulfilled))
            {
                row = new ObjectiveHudRowView(levelOrderIndex, orderSpec, cellsFulfilled);
                return true;
            }

            row = default;
            return false;
        }

        public void NotifyStateChanged() => RaiseStateChanged();

        void IRackDrainHost.RaiseStateChanged() => RaiseStateChanged();

        /// <summary>
        /// Reserves a projected collect destination (order icon or rack slot) for an in-flight transaction.
        /// Returns false when the session is inactive or the projected rack is full.
        /// </summary>
        public bool TryReserveCollect(TileKind kind, out CollectReservation reservation) =>
            _reservations.TryReserveCollect(_collectContext, kind, out reservation);

        /// <summary>
        /// Reserves the order icon and projected rack slot freed by a rack→order drain step.
        /// </summary>
        public bool TryReserveRackDrain(TileKind kind, TileCollectDestination orderDestination, out CollectReservation reservation) =>
            _reservations.TryReserveRackDrain(_collectContext, kind, orderDestination, out reservation);

        public TileCollectResult CommitReservation(CollectReservation reservation)
        {
            _reservations.Release(reservation);
            return TryCollectTile(reservation.Kind);
        }

        public void CancelReservation(CollectReservation reservation) => _reservations.Release(reservation);

        public void CancelAllReservations() => _reservations.ReleaseAll();

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
