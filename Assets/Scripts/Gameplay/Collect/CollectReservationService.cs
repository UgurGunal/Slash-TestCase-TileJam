using Core;

namespace Gameplay.Collect
{
    /// <summary>
    /// Owns in-flight collect reservations and projects destinations (order icon or rack slot)
    /// from the current session state plus outstanding reservations.
    /// Projection = committed state + in-flight reservations, so concurrent flights never
    /// overfill the rack or double-book an order icon.
    /// </summary>
    public sealed class CollectReservationService
    {
        readonly CollectReservationBook _book = new CollectReservationBook();

        /// <summary>
        /// Reserves a projected collect destination (order icon or rack slot) for an in-flight transaction.
        /// Returns false when the session is inactive or the projected rack is full.
        /// </summary>
        public bool TryReserveCollect(CollectSessionContext context, TileKind kind, out CollectReservation reservation)
        {
            reservation = default;
            if (context.IsInactive) return false;

            if (TryFindProjectedOrderMatch(context, kind, out var slot, out var iconIdx))
            {
                var destination = TileCollectDestination.ForOrderSlot(slot, iconIdx);
                reservation = new CollectReservation(
                    _book.NextId(),
                    kind,
                    destination,
                    rackDelta: 0,
                    orderSlot: slot,
                    orderIcon: iconIdx);
                _book.Add(reservation);
                return true;
            }

            if (ProjectedRackIsFull(context))
                return false;

            var rackDestination = TileCollectDestination.ForRackSlot(ProjectedRackCount(context));
            reservation = new CollectReservation(_book.NextId(), kind, rackDestination, rackDelta: 1);
            _book.Add(reservation);
            return true;
        }

        /// <summary>
        /// Reserves the order icon and projected rack slot freed by a rack→order drain step.
        /// </summary>
        public bool TryReserveRackDrain(
            CollectSessionContext context,
            TileKind kind,
            TileCollectDestination orderDestination,
            out CollectReservation reservation)
        {
            reservation = default;
            if (context.IsInactive) return false;

            var slot = orderDestination.ActiveOrderSlotIndex;
            var icon = orderDestination.OrderIconIndex;
            if (_book.IsIconReserved(slot, icon))
                return false;

            if (context.OrderSlots.GetActiveSlot(slot, out _, out _, out var fulfilled) && fulfilled[icon])
                return false;

            reservation = new CollectReservation(
                _book.NextId(),
                kind,
                orderDestination,
                rackDelta: -1,
                orderSlot: slot,
                orderIcon: icon);
            _book.Add(reservation);
            return true;
        }

        /// <summary>Releases a reservation once its transaction commits or is abandoned.</summary>
        public void Release(CollectReservation reservation) => _book.Remove(reservation.Id);

        public void ReleaseAll() => _book.Clear();

        int ProjectedRackCount(CollectSessionContext context) => context.Rack.Count + _book.RackDelta;

        bool ProjectedRackIsFull(CollectSessionContext context) => ProjectedRackCount(context) >= context.Rack.Capacity;

        bool TryFindProjectedOrderMatch(
            CollectSessionContext context,
            TileKind kind,
            out int activeOrderSlot,
            out int iconIndexInOrder) =>
            context.OrderSlots.FindFirstUnfilledOrderMatch(
                kind,
                _book.IsIconReserved,
                out activeOrderSlot,
                out iconIndexInOrder,
                out _);
    }
}
