using System;
using System.Collections.Generic;
using Core;
using LevelData;

namespace Gameplay
{
    public sealed class ActiveOrderSlots
    {
        readonly LevelOrdersSpec _orders;
        readonly Queue<int> _pendingOrderIndices = new Queue<int>();
        readonly int[] _slotOrderIndex;
        readonly bool[][] _slotCellFulfilled;
        int _completedOrders;

        public ActiveOrderSlots(LevelOrdersSpec orders)
        {
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            var k = GameConstants.ActiveOrderSlotsCount;
            _slotOrderIndex = new int[k];
            _slotCellFulfilled = new bool[k][];

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

        public event Action<int> ActiveOrderSlotAdvanced;

        public int CompletedOrders => _completedOrders;
        public int TotalOrders => _orders.OrderCount;
        public bool HasWon => _completedOrders >= _orders.OrderCount;
        public int MaxOrderIconsOnLevel => _orders.MaxIconsInAnyOrder;

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

        /// <summary>Lowest active order slot index, then left-to-right first unfilled cell matching <paramref name="kind"/>.</summary>
        public bool FindFirstUnfilledOrderMatch(TileKind kind, out int activeOrderSlot, out int iconIndexInOrder, out int levelOrderIndex)
        {
            activeOrderSlot = -1;
            iconIndexInOrder = -1;
            levelOrderIndex = -1;

            for (var s = 0; s < _slotOrderIndex.Length; s++)
            {
                var oi = _slotOrderIndex[s];
                if (oi < 0) continue;

                var order = _orders.Orders[oi];
                var fulfilled = _slotCellFulfilled[s];
                for (var i = 0; i < order.Length; i++)
                {
                    if (fulfilled[i]) continue;
                    if (order.GetIcon(i) != kind) continue;
                    activeOrderSlot = s;
                    iconIndexInOrder = i;
                    levelOrderIndex = oi;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Marks one matching icon fulfilled. Returns false when no active order needs <paramref name="kind"/>.
        /// When <paramref name="completedWholeOrder"/> is true, the slot queue has already advanced.
        /// </summary>
        public bool TryFulfillIcon(
            TileKind kind,
            CollectApplySource source,
            ICollectFlowLogger logger,
            out bool completedWholeOrder,
            out TileCollectResult result)
        {
            result = TileCollectResult.ConsumedForOrder;
            completedWholeOrder = false;

            if (!FindFirstUnfilledOrderMatch(kind, out var s, out var matchIndex, out var oi))
                return false;

            var order = _orders.Orders[oi];
            var fulfilled = _slotCellFulfilled[s];
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
            {
                completedWholeOrder = true;
                if (logger.IsEnabled)
                {
                    var src = source == CollectApplySource.FromBoardClick ? "Board" : "Rack auto";
                    logger.Log($"[TileCollect] {src}: {kind} completed active order slot {s} (was level order index {oi}).");
                }

                result = AdvanceSlotAfterOrderComplete(s, logger);

                if (logger.IsEnabled && result != TileCollectResult.LevelWon)
                {
                    var nextOi = _slotOrderIndex[s];
                    if (nextOi >= 0)
                        logger.Log($"[TileCollect] Active order slot {s} advanced to level order index {nextOi} ({_orders.Orders[nextOi].Length} icon(s)).");
                    else
                        logger.Log($"[TileCollect] Active order slot {s} is now idle (no more queued orders in that slot).");
                }

                if (logger.IsEnabled && result == TileCollectResult.LevelWon)
                {
                    var src = source == CollectApplySource.FromBoardClick ? "Board" : "Rack auto";
                    logger.Log($"[TileCollect] {src}: that completion finished the final order → level won.");
                }

                return true;
            }

            if (logger.IsEnabled)
            {
                var src = source == CollectApplySource.FromBoardClick ? "Board" : "Rack auto";
                logger.Log($"[TileCollect] {src}: {kind} matched active order slot {s} (level order index {oi}) at icon index {matchIndex} — partial (more icons needed for that customer).");
            }

            result = TileCollectResult.ConsumedForOrder;
            return true;
        }

        TileCollectResult AdvanceSlotAfterOrderComplete(int slot, ICollectFlowLogger logger)
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

            ActiveOrderSlotAdvanced?.Invoke(slot);

            if (_completedOrders >= _orders.OrderCount)
                return TileCollectResult.LevelWon;

            return TileCollectResult.OrderCompleted;
        }
    }

    public enum CollectApplySource
    {
        FromBoardClick,
        FromRack,
    }
}
