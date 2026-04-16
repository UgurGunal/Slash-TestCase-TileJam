using System;
using System.Collections.Generic;
using Core;
using LevelData;

namespace Gameplay
{
    public enum TileCollectResult
    {
        ConsumedForOrder,
        AddedToRack,
        FailedRackFull,
        LevelWon,
    }

    /// <summary>
    /// Order queue, active slots (<see cref="GameConstants.ActiveOrderSlotsCount"/>), and rack.
    /// Within each order, required icons may be collected in any order; duplicates use positional slots for UI.
    /// </summary>
    public sealed class LevelObjectiveSession
    {
        readonly LevelOrdersSpec _orders;
        readonly Queue<int> _pendingOrderIndices = new Queue<int>();
        readonly int[] _slotOrderIndex;
        /// <summary>Per display cell: fulfilled for current order in that slot (same length as that order).</summary>
        readonly bool[][] _slotCellFulfilled;
        readonly TileKind?[] _rack;
        int _rackCount;
        int _completedOrders;
        bool _failed;

        public LevelObjectiveSession(LevelOrdersSpec orders)
        {
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            var k = GameConstants.ActiveOrderSlotsCount;
            _slotOrderIndex = new int[k];
            _slotCellFulfilled = new bool[k][];
            _rack = new TileKind?[GameConstants.RackCapacity];

            var n = _orders.OrderCount;
            for (var s = 0; s < k; s++)
            {
                if (s < n)
                {
                    _slotOrderIndex[s] = s;
                    _slotCellFulfilled[s] = new bool[_orders.Orders[s].Length];
                }
                else
                {
                    _slotOrderIndex[s] = -1;
                    _slotCellFulfilled[s] = null;
                }
            }

            for (var i = k; i < n; i++)
                _pendingOrderIndices.Enqueue(i);
        }

        public event Action StateChanged;

        public bool HasFailed => _failed;
        public bool HasWon => _completedOrders >= _orders.OrderCount;
        public int RackUsedCount => _rackCount;
        public int CompletedOrderCount => _completedOrders;
        public int TotalOrderCount => _orders.OrderCount;

        /// <summary>Longest order this level — size order-strip UI with <see cref="GameConstants.ActiveOrderSlotsCount"/> × this many icon slots.</summary>
        public int MaxOrderIconsOnLevel => _orders.MaxIconsInAnyOrder;

        public TileKind? GetRackSlot(int index)
        {
            if ((uint)index >= (uint)GameConstants.RackCapacity) return null;
            return index < _rackCount ? _rack[index] : null;
        }

        public bool GetActiveSlot(int slot, out int levelOrderIndex, out OrderSpec orderSpec, out bool[] cellsFulfilled)
        {
            orderSpec = null;
            cellsFulfilled = null;
            levelOrderIndex = -1;
            if ((uint)slot >= (uint)_slotOrderIndex.Length) return false;

            var oi = _slotOrderIndex[slot];
            if (oi < 0) return false;

            levelOrderIndex = oi;
            orderSpec = _orders.Orders[oi];
            cellsFulfilled = _slotCellFulfilled[slot];
            return true;
        }

        public bool IsSlotIdle(int slot) =>
            (uint)slot < (uint)_slotOrderIndex.Length && _slotOrderIndex[slot] < 0;

        public TileCollectResult TryCollectTile(TileKind kind)
        {
            if (_failed || HasWon)
                return TileCollectResult.ConsumedForOrder;

            for (var s = 0; s < _slotOrderIndex.Length; s++)
            {
                var oi = _slotOrderIndex[s];
                if (oi < 0) continue;

                var order = _orders.Orders[oi];
                var fulfilled = _slotCellFulfilled[s];
                var matchIndex = -1;
                for (var i = 0; i < order.Length; i++)
                {
                    if (fulfilled[i]) continue;
                    if (order.GetIcon(i) != kind) continue;
                    matchIndex = i;
                    break;
                }

                if (matchIndex < 0) continue;

                fulfilled[matchIndex] = true;
                var allDone = true;
                for (var j = 0; j < order.Length; j++)
                {
                    if (!fulfilled[j])
                    {
                        allDone = false;
                        break;
                    }
                }

                if (allDone)
                    return FinishOrderInSlot(s);

                StateChanged?.Invoke();
                return TileCollectResult.ConsumedForOrder;
            }

            if (_rackCount >= GameConstants.RackCapacity)
            {
                _failed = true;
                StateChanged?.Invoke();
                return TileCollectResult.FailedRackFull;
            }

            _rack[_rackCount++] = kind;
            StateChanged?.Invoke();
            return TileCollectResult.AddedToRack;
        }

        TileCollectResult FinishOrderInSlot(int slot)
        {
            _completedOrders++;

            if (_pendingOrderIndices.Count > 0)
            {
                var nextOi = _pendingOrderIndices.Dequeue();
                _slotOrderIndex[slot] = nextOi;
                _slotCellFulfilled[slot] = new bool[_orders.Orders[nextOi].Length];
            }
            else
            {
                _slotOrderIndex[slot] = -1;
                _slotCellFulfilled[slot] = null;
            }

            if (_completedOrders >= _orders.OrderCount)
            {
                StateChanged?.Invoke();
                return TileCollectResult.LevelWon;
            }

            StateChanged?.Invoke();
            return TileCollectResult.ConsumedForOrder;
        }
    }
}
