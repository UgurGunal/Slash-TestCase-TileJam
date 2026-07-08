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
        readonly CollectReservationBook _reservationBook = new CollectReservationBook();
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

        public bool IsSlotIdle(int slot) => _orderSlots.IsSlotIdle(slot);

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

            if (TryFindProjectedOrderMatch(kind, out var slot, out var iconIdx))
            {
                destination = TileCollectDestination.ForOrderSlot(slot, iconIdx);
                return true;
            }

            var projectedRackCount = _rack.Count + _reservationBook.RackDelta;
            if (projectedRackCount >= _rack.Capacity)
            {
                failureReason = TileCollectResult.FailedRackFull;
                return false;
            }

            destination = TileCollectDestination.ForRackSlot(projectedRackCount);
            return true;
        }

        /// <summary>
        /// Reserves a projected collect destination (order icon or rack slot) for an in-flight transaction.
        /// Returns false when the session is inactive or the projected rack is full.
        /// </summary>
        public bool TryReserveCollect(TileKind kind, out CollectReservation reservation)
        {
            reservation = default;
            if (_failed || HasWon) return false;

            if (TryFindProjectedOrderMatch(kind, out var slot, out var iconIdx))
            {
                var destination = TileCollectDestination.ForOrderSlot(slot, iconIdx);
                reservation = new CollectReservation(
                    _reservationBook.NextId(),
                    kind,
                    destination,
                    rackDelta: 0,
                    orderSlot: slot,
                    orderIcon: iconIdx);
                _reservationBook.Add(reservation);
                return true;
            }

            var projectedRackCount = _rack.Count + _reservationBook.RackDelta;
            if (projectedRackCount >= _rack.Capacity)
                return false;

            var rackDestination = TileCollectDestination.ForRackSlot(projectedRackCount);
            reservation = new CollectReservation(
                _reservationBook.NextId(),
                kind,
                rackDestination,
                rackDelta: 1);
            _reservationBook.Add(reservation);
            return true;
        }

        /// <summary>
        /// Reserves the order icon and projected rack slot freed by a rack→order drain step.
        /// </summary>
        public bool TryReserveRackDrain(
            TileKind kind,
            TileCollectDestination orderDestination,
            out CollectReservation reservation)
        {
            reservation = default;
            if (_failed || HasWon) return false;

            var slot = orderDestination.ActiveOrderSlotIndex;
            var icon = orderDestination.OrderIconIndex;
            if (_reservationBook.IsIconReserved(slot, icon))
                return false;

            if (_orderSlots.GetActiveSlot(slot, out _, out _, out var fulfilled) && fulfilled[icon])
                return false;

            reservation = new CollectReservation(
                _reservationBook.NextId(),
                kind,
                orderDestination,
                rackDelta: -1,
                orderSlot: slot,
                orderIcon: icon);
            _reservationBook.Add(reservation);
            return true;
        }

        public TileCollectResult CommitReservation(CollectReservation reservation)
        {
            _reservationBook.Remove(reservation.Id);
            return TryCollectTile(reservation.Kind);
        }

        public void CancelReservation(CollectReservation reservation) => _reservationBook.Remove(reservation.Id);

        public void CancelAllReservations() => _reservationBook.Clear();

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

        bool TryFindProjectedOrderMatch(TileKind kind, out int activeOrderSlot, out int iconIndexInOrder)
        {
            activeOrderSlot = -1;
            iconIndexInOrder = -1;

            for (var s = 0; ; s++)
            {
                if (_orderSlots.IsSlotIdle(s))
                    continue;

                if (!_orderSlots.GetActiveSlot(s, out _, out var orderSpec, out var fulfilled))
                    break;

                for (var i = 0; i < orderSpec.Length; i++)
                {
                    if (fulfilled[i]) continue;
                    if (_reservationBook.IsIconReserved(s, i)) continue;
                    if (orderSpec.GetIcon(i) != kind) continue;

                    activeOrderSlot = s;
                    iconIndexInOrder = i;
                    return true;
                }
            }

            return false;
        }
    }
}
