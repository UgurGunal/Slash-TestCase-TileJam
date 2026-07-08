using Core;

namespace Gameplay.Collect
{
    /// <summary>
    /// In-flight collect transaction: reserves a projected order icon or rack slot before the fly animation commits.
    /// </summary>
    public readonly struct CollectReservation
    {
        public int Id { get; }
        public TileKind Kind { get; }
        public Gameplay.TileCollectDestination Destination { get; }
        /// <summary>Net rack occupancy delta while this reservation is outstanding (+1 board→rack, -1 rack drain, 0 board→order).</summary>
        public int RackDelta { get; }
        public int OrderSlot { get; }
        public int OrderIcon { get; }

        public CollectReservation(
            int id,
            TileKind kind,
            Gameplay.TileCollectDestination destination,
            int rackDelta,
            int orderSlot = -1,
            int orderIcon = -1)
        {
            Id = id;
            Kind = kind;
            Destination = destination;
            RackDelta = rackDelta;
            OrderSlot = orderSlot;
            OrderIcon = orderIcon;
        }

        public bool TargetsOrder => OrderSlot >= 0 && OrderIcon >= 0;
    }
}
