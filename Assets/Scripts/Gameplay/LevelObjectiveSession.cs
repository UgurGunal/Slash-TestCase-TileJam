using System;
using System.Collections.Generic;
using Core;
using LevelData;
using UnityEngine;

namespace Gameplay
{
    public enum TileCollectResult
    {
        ConsumedForOrder,
        AddedToRack,
        FailedRackFull,
        LevelWon,
        /// <summary>Level already won or lost — tile was not collected; board tile must stay.</summary>
        SessionInactive,
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

        /// <summary>When true, <see cref="TryCollectTile"/> logs each step (board match, rack, auto rack drain). Enable on <c>LevelBoardLoader.logTileCollectFlow</c>.</summary>
        public bool LogCollectFlow { get; set; }

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
            if (LogCollectFlow)
                Debug.Log($"[TileCollect] Click {kind} — start (rack before: {_rackCount} tile(s)).");

            if (_failed || HasWon)
            {
                if (LogCollectFlow)
                    Debug.Log($"[TileCollect] Click {kind} — ignored: session already {(HasWon ? "won" : "failed (rack full)")}.");
                return TileCollectResult.SessionInactive;
            }

            if (!TryApplyKindToActiveOrders(kind, CollectApplySource.FromBoardClick, out var applyResult, out var completedWholeOrder))
            {
                if (_rackCount >= GameConstants.RackCapacity)
                {
                    _failed = true;
                    StateChanged?.Invoke();
                    if (LogCollectFlow)
                        Debug.Log($"[TileCollect] Click {kind} — no matching order; rack full → level failed.");
                    return TileCollectResult.FailedRackFull;
                }

                _rack[_rackCount++] = kind;
                StateChanged?.Invoke();
                if (LogCollectFlow)
                    Debug.Log($"[TileCollect] Click {kind} — no matching order → added to rack at index {_rackCount - 1} (rack now {_rackCount} tile(s)).");
                return TileCollectResult.AddedToRack;
            }

            if (applyResult == TileCollectResult.LevelWon)
            {
                StateChanged?.Invoke();
                return TileCollectResult.LevelWon;
            }

            if (!completedWholeOrder)
                return TileCollectResult.ConsumedForOrder;

            if (LogCollectFlow)
                Debug.Log($"[TileCollect] After click {kind}: draining rack against new / remaining orders…");

            var drainResult = TryDrainRackAgainstActiveOrders();
            if (drainResult == TileCollectResult.LevelWon)
            {
                StateChanged?.Invoke();
                if (LogCollectFlow)
                    Debug.Log($"[TileCollect] Rack drain finished → level won. Rack now {_rackCount} tile(s).");
                return TileCollectResult.LevelWon;
            }

            StateChanged?.Invoke();
            if (LogCollectFlow)
                Debug.Log($"[TileCollect] Click {kind} — done (order completed + rack drain). Rack now {_rackCount} tile(s).");
            return TileCollectResult.ConsumedForOrder;
        }

        /// <summary>
        /// Fulfills one matching requirement on the first active order strip that needs <paramref name="kind"/>.
        /// On a whole-order completion, advances the slot queue (same as a board tile) but does not add to the rack.
        /// </summary>
        /// <returns>False when no active order still needs this icon.</returns>
        bool TryApplyKindToActiveOrders(TileKind kind, CollectApplySource source, out TileCollectResult result, out bool completedWholeOrder)
        {
            result = TileCollectResult.ConsumedForOrder;
            completedWholeOrder = false;

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
                {
                    completedWholeOrder = true;
                    if (LogCollectFlow)
                    {
                        var src = source == CollectApplySource.FromBoardClick ? "Board" : "Rack auto";
                        Debug.Log($"[TileCollect] {src}: {kind} completed UI strip {s} (was level order index {oi}).");
                    }

                    result = AdvanceSlotAfterOrderComplete(s);

                    if (LogCollectFlow && result != TileCollectResult.LevelWon)
                    {
                        var nextOi = _slotOrderIndex[s];
                        if (nextOi >= 0)
                            Debug.Log($"[TileCollect] Strip {s} advanced to level order index {nextOi} ({_orders.Orders[nextOi].Length} icon(s)).");
                        else
                            Debug.Log($"[TileCollect] Strip {s} is now idle (no more queued orders in that slot).");
                    }

                    if (LogCollectFlow && result == TileCollectResult.LevelWon)
                    {
                        var src = source == CollectApplySource.FromBoardClick ? "Board" : "Rack auto";
                        Debug.Log($"[TileCollect] {src}: that completion finished the final order → level won.");
                    }

                    return true;
                }

                if (LogCollectFlow)
                {
                    var src = source == CollectApplySource.FromBoardClick ? "Board" : "Rack auto";
                    Debug.Log($"[TileCollect] {src}: {kind} matched UI strip {s} (level order index {oi}) at icon index {matchIndex} — partial (more icons needed for that customer).");
                }

                StateChanged?.Invoke();
                result = TileCollectResult.ConsumedForOrder;
                return true;
            }

            return false;
        }

        TileCollectResult AdvanceSlotAfterOrderComplete(int slot)
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
                return TileCollectResult.LevelWon;

            return TileCollectResult.ConsumedForOrder;
        }

        /// <summary>
        /// After a new order appears in a slot, consumes rack tiles left→right when they match any unfilled objective cell (same priority as <see cref="TryCollectTile"/>).
        /// </summary>
        TileCollectResult TryDrainRackAgainstActiveOrders()
        {
            if (_failed || HasWon)
                return HasWon ? TileCollectResult.LevelWon : TileCollectResult.ConsumedForOrder;

            while (true)
            {
                var progressed = false;
                for (var i = 0; i < _rackCount;)
                {
                    var kind = _rack[i].Value;
                    if (!TryApplyKindToActiveOrders(kind, CollectApplySource.FromRack, out var r, out _))
                    {
                        i++;
                        continue;
                    }

                    if (LogCollectFlow)
                        Debug.Log($"[TileCollect] Rack auto: consumed rack slot {i} ({kind}) — removed from rack and applied to orders.");

                    RemoveRackSlotAt(i);
                    progressed = true;

                    if (r == TileCollectResult.LevelWon)
                        return TileCollectResult.LevelWon;
                }

                if (!progressed)
                    break;
            }

            return TileCollectResult.ConsumedForOrder;
        }

        void RemoveRackSlotAt(int index)
        {
            if ((uint)index >= (uint)_rackCount) return;
            for (var j = index; j < _rackCount - 1; j++)
                _rack[j] = _rack[j + 1];
            _rackCount--;
            if ((uint)_rackCount < (uint)_rack.Length)
                _rack[_rackCount] = null;
        }

        enum CollectApplySource
        {
            FromBoardClick,
            FromRack,
        }
    }
}
