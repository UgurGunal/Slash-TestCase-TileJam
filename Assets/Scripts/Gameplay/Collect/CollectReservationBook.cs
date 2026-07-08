using System.Collections.Generic;

namespace Gameplay.Collect
{
    /// <summary>Tracks outstanding collect reservations and their projected rack delta.</summary>
    public sealed class CollectReservationBook
    {
        readonly List<CollectReservation> _reservations = new List<CollectReservation>();
        int _nextId = 1;
        int _rackDelta;

        public int RackDelta => _rackDelta;
        public int Count => _reservations.Count;

        public int NextId() => _nextId++;

        public bool IsIconReserved(int slot, int icon)
        {
            for (var i = 0; i < _reservations.Count; i++)
            {
                var r = _reservations[i];
                if (r.OrderSlot == slot && r.OrderIcon == icon)
                    return true;
            }

            return false;
        }

        public void Add(CollectReservation reservation)
        {
            _reservations.Add(reservation);
            _rackDelta += reservation.RackDelta;
        }

        public bool Remove(int id)
        {
            for (var i = 0; i < _reservations.Count; i++)
            {
                if (_reservations[i].Id != id) continue;
                _rackDelta -= _reservations[i].RackDelta;
                _reservations.RemoveAt(i);
                return true;
            }

            return false;
        }

        public void Clear()
        {
            _reservations.Clear();
            _rackDelta = 0;
        }
    }
}
