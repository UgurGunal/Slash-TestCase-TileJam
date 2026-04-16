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

    /// <summary>Order queue, active slots (<see cref="GameConstants.ActiveOrderSlotsCount"/>), and rack for one level run.</summary>
    public sealed class LevelObjectiveSession
    {
        readonly LevelOrdersSpec _orders;
        readonly Queue<int> _pendingOrderIndices = new Queue<int>();
        readonly int[] _slotOrderIndex;
        readonly int[] _slotProgress;
        readonly TileKind?[] _rack;
        int _rackCount;
        int _completedOrders;
        bool _failed;

        public LevelObjectiveSession(LevelOrdersSpec orders)
        {
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            var k = GameConstants.ActiveOrderSlotsCount;
            _slotOrderIndex = new int[k];
            _slotProgress = new int[k];
            _rack = new TileKind?[GameConstants.RackCapacity];

            var n = _orders.OrderCount;
            for (var s = 0; s < k; s++)
            {
                if (s < n)
                {
                    _slotOrderIndex[s] = s;
                    _slotProgress[s] = 0;
                }
                else
                {
                    _slotOrderIndex[s] = -1;
                    _slotProgress[s] = 0;
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

        public bool GetActiveSlot(int slot, out int levelOrderIndex, out int fulfilledIcons, out OrderSpec orderSpec)
        {
            orderSpec = null;
            fulfilledIcons = 0;
            levelOrderIndex = -1;
            if ((uint)slot >= (uint)_slotOrderIndex.Length) return false;

            var oi = _slotOrderIndex[slot];
            if (oi < 0) return false;

            levelOrderIndex = oi;
            fulfilledIcons = _slotProgress[slot];
            orderSpec = _orders.Orders[oi];
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
                var need = order.GetRequired(_slotProgress[s]);
                if (need != kind) continue;

                _slotProgress[s]++;
                if (_slotProgress[s] >= order.Length)
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
            _slotProgress[slot] = 0;

            if (_pendingOrderIndices.Count > 0)
                _slotOrderIndex[slot] = _pendingOrderIndices.Dequeue();
            else
                _slotOrderIndex[slot] = -1;

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
