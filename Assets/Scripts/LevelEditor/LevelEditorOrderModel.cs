#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Core;
using LevelData;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>Order columns, finalize snapshot, and reverse-build queue (editor-only, no UGUI).</summary>
    sealed class LevelEditorOrderModel
    {
        readonly List<List<TileKind>> _columns = new List<List<TileKind>>();
        // Parallel to _columns: for each live icon, the original snapshot icon index in that column.
        readonly List<List<int>> _liveSnapIcons = new List<List<int>>();
        List<List<TileKind>> _snapshotAtFinalize;
        readonly List<ReverseItem> _reverseQueue = new List<ReverseItem>();
        readonly List<int> _plannedRackTiles = new List<int>();
        int _reverseIndex;
        int _selectedRackTileIndex = -1;
        bool _finalized;
        bool _reverseActive;

        readonly struct ReverseItem
        {
            public readonly int ColumnIndex;
            public readonly int TileIndexInColumn;
            public readonly TileKind Kind;

            public ReverseItem(int columnIndex, int tileIndexInColumn, TileKind kind)
            {
                ColumnIndex = columnIndex;
                TileIndexInColumn = tileIndexInColumn;
                Kind = kind;
            }
        }

        public LevelEditorOrderModel() => EnsureAtLeastOneColumn();

        public IReadOnlyList<IReadOnlyList<TileKind>> Columns => _columns;
        public bool IsFinalized => _finalized;
        public bool ReverseBuildActive => _reverseActive;
        public IReadOnlyList<int> PlannedRackTiles => _plannedRackTiles;
        public int SelectedRackTileIndex => _selectedRackTileIndex;

        public bool HasOrderSnapshot => _snapshotAtFinalize != null && SnapshotTileCount() > 0;

        public IReadOnlyList<IReadOnlyList<TileKind>> SnapshotColumns =>
            _snapshotAtFinalize == null ? _columns : _snapshotAtFinalize;

        int SnapshotTileCount()
        {
            if (_snapshotAtFinalize == null) return 0;
            var total = 0;
            for (var c = 0; c < _snapshotAtFinalize.Count; c++)
            {
                var col = _snapshotAtFinalize[c];
                if (col != null) total += col.Count;
            }

            return total;
        }

        public void EnsureAtLeastOneColumn()
        {
            if (_columns.Count == 0)
            {
                _columns.Add(new List<TileKind>());
                _liveSnapIcons.Add(new List<int>());
            }

            while (_liveSnapIcons.Count < _columns.Count)
                _liveSnapIcons.Add(new List<int>());
            while (_liveSnapIcons.Count > _columns.Count)
                _liveSnapIcons.RemoveAt(_liveSnapIcons.Count - 1);
        }

        public void ClearAll()
        {
            _columns.Clear();
            _liveSnapIcons.Clear();
            _columns.Add(new List<TileKind>());
            _liveSnapIcons.Add(new List<int>());
            _snapshotAtFinalize = null;
            _finalized = false;
            StopReverseBuild();
        }

        /// <summary>Clears live columns after a full level load; keeps finalize snapshot for export.</summary>
        public void ClearColumnsKeepSnapshot()
        {
            _columns.Clear();
            _liveSnapIcons.Clear();
            _columns.Add(new List<TileKind>());
            _liveSnapIcons.Add(new List<int>());
            StopReverseBuild();
        }

        public void AddFromPalette(TileKind kind, int amount)
        {
            if (_finalized || kind == TileKind.None || amount < 1) return;
            EnsureAtLeastOneColumn();
            var col = _columns[_columns.Count - 1];
            var icons = _liveSnapIcons[_columns.Count - 1];
            for (var i = 0; i < amount; i++)
            {
                icons.Add(col.Count); // provisional index until finalized into snapshot
                col.Add(kind);
            }
        }

        public void ClearActiveColumn()
        {
            if (_finalized) return;
            EnsureAtLeastOneColumn();
            _columns[_columns.Count - 1].Clear();
            _liveSnapIcons[_columns.Count - 1].Clear();
        }

        public void RemoveTileFromActiveColumn(int tileIndex)
        {
            if (_finalized) return;
            EnsureAtLeastOneColumn();
            var c = _columns.Count - 1;
            var col = _columns[c];
            if ((uint)tileIndex >= (uint)col.Count) return;
            col.RemoveAt(tileIndex);
            _liveSnapIcons[c].RemoveAt(tileIndex);
        }

        public void RemoveTileFromColumn(int columnIndex, int tileIndex)
        {
            if (_finalized) return;
            if ((uint)columnIndex >= (uint)_columns.Count) return;
            if (columnIndex != _columns.Count - 1) return;
            var col = _columns[columnIndex];
            if ((uint)tileIndex >= (uint)col.Count) return;
            col.RemoveAt(tileIndex);
            _liveSnapIcons[columnIndex].RemoveAt(tileIndex);
        }

        public void AddOrderColumn()
        {
            if (_finalized) return;
            EnsureAtLeastOneColumn();
            if (_columns[_columns.Count - 1].Count == 0) return;
            _columns.Add(new List<TileKind>());
            _liveSnapIcons.Add(new List<int>());
        }

        public void Finalize()
        {
            if (_finalized) return;
            _finalized = true;

            // Keep an existing snapshot intact (editing an already-defined level).
            // Rebuilding/merging would desync board provenance (orderCol/orderIcon on cells).
            if (_snapshotAtFinalize == null)
                CaptureSnapshot();

            StartReverseBuild();
        }

        public void Unlock()
        {
            if (!_finalized) return;
            RestoreColumnsFromSnapshot();
            _finalized = false;
            _snapshotAtFinalize = null;
            StopReverseBuild();
        }

        /// <summary>
        /// Unlock for editing, but keep the current live columns as-is.
        /// This is important during edit flow: if you've already placed (fulfilled) all tiles on the board,
        /// the live columns may be empty and unlocking should not re-populate them from the snapshot.
        /// </summary>
        public void UnlockPreserveLiveState()
        {
            if (!_finalized) return;
            _finalized = false;
            StopReverseBuild();
        }

        /// <summary>
        /// During edit: remove-all-board should make orders "full again" so the user can place every tile.
        /// This restores live order columns from the finalized snapshot (does not change the snapshot itself).
        /// </summary>
        public void LoadLiveFromSnapshotForPlacement()
        {
            if (_snapshotAtFinalize == null) return;

            _columns.Clear();
            _liveSnapIcons.Clear();
            for (var c = 0; c < _snapshotAtFinalize.Count; c++)
            {
                var src = _snapshotAtFinalize[c];
                var kinds = src != null ? new List<TileKind>(src) : new List<TileKind>();
                var icons = new List<int>(kinds.Count);
                for (var i = 0; i < kinds.Count; i++)
                    icons.Add(i);
                _columns.Add(kinds);
                _liveSnapIcons.Add(icons);
            }
            EnsureAtLeastOneColumn();
            StartReverseBuildFromSnapshot();
        }

        public void LoadFromOrders(LevelOrdersSpec orders, bool startFinalized)
        {
            _columns.Clear();
            _liveSnapIcons.Clear();
            if (orders == null || orders.OrderCount == 0)
            {
                _columns.Add(new List<TileKind>());
                _liveSnapIcons.Add(new List<int>());
            }
            else
            {
                for (var i = 0; i < orders.OrderCount; i++)
                {
                    var order = orders.Orders[i];
                    var col = new List<TileKind>(order.Length);
                    var icons = new List<int>(order.Length);
                    for (var j = 0; j < order.Length; j++)
                    {
                        col.Add(order.GetIcon(j));
                        icons.Add(j);
                    }
                    _columns.Add(col);
                    _liveSnapIcons.Add(icons);
                }
            }

            EnsureAtLeastOneColumn();
            _finalized = startFinalized;
            if (_finalized)
            {
                CaptureSnapshot();
                StopReverseBuild();
            }
            else
            {
                _snapshotAtFinalize = null;
                StopReverseBuild();
            }
        }

        /// <summary>
        /// Loads an existing level as "finalized, everything already placed on the board":
        /// the snapshot holds the real orders (for export), while live columns start empty because
        /// all order tiles currently sit on the board. Removing board tiles refills the live columns.
        /// </summary>
        public void LoadFinalizedAllPlaced(LevelOrdersSpec orders)
        {
            _columns.Clear();
            _liveSnapIcons.Clear();
            _snapshotAtFinalize = new List<List<TileKind>>();
            var n = orders?.OrderCount ?? 0;
            for (var i = 0; i < n; i++)
            {
                var order = orders.Orders[i];
                var snap = new List<TileKind>(order.Length);
                for (var j = 0; j < order.Length; j++)
                    snap.Add(order.GetIcon(j));
                _snapshotAtFinalize.Add(snap);
                _columns.Add(new List<TileKind>());
                _liveSnapIcons.Add(new List<int>());
            }

            EnsureAtLeastOneColumn();
            _finalized = true;
            StartReverseBuildFromSnapshot();
        }

        /// <summary>
        /// Return a board tile into its exact original order slot (column + snapshot icon index).
        /// Live columns stay sorted by snap icon so order strips rebuild correctly for re-placement.
        /// </summary>
        public bool TryReturnTileToOrders(TileKind kind, int orderCol, int orderIcon)
        {
            if (_snapshotAtFinalize == null || kind == TileKind.None) return false;

            if (orderCol >= 0 && orderIcon >= 0 &&
                (uint)orderCol < (uint)_snapshotAtFinalize.Count)
            {
                var snap = _snapshotAtFinalize[orderCol];
                if (snap != null &&
                    (uint)orderIcon < (uint)snap.Count &&
                    snap[orderIcon] == kind)
                {
                    return InsertLiveAtSnapSlot(orderCol, orderIcon, kind);
                }
            }

            // Fallback when origin was lost: reuse kind-based insert into a column that still needs this kind.
            return TryReturnTileToOrders(kind);
        }

        /// <summary>
        /// Kind-only fallback when origin includes no exact slot (e.g. old placements without provenance).
        /// </summary>
        public bool TryReturnTileToOrders(TileKind kind)
        {
            if (_snapshotAtFinalize == null || kind == TileKind.None) return false;
            if (CountInLiveColumns(kind) >= CountInSnapshot(kind)) return false;

            for (var c = 0; c < _columns.Count && c < _snapshotAtFinalize.Count; c++)
            {
                var snap = _snapshotAtFinalize[c];
                if (snap == null) continue;
                for (var i = 0; i < snap.Count; i++)
                {
                    if (snap[i] != kind) continue;
                    if (LiveHasSnapIcon(c, i)) continue;
                    return InsertLiveAtSnapSlot(c, i, kind);
                }
            }

            EnsureAtLeastOneColumn();
            var last = _columns.Count - 1;
            _columns[last].Add(kind);
            _liveSnapIcons[last].Add(_columns[last].Count - 1);
            return true;
        }

        bool InsertLiveAtSnapSlot(int orderCol, int orderIcon, TileKind kind)
        {
            EnsureColumnCapacity(orderCol);
            if (LiveHasSnapIcon(orderCol, orderIcon)) return false;

            var live = _columns[orderCol];
            var icons = _liveSnapIcons[orderCol];
            var insertAt = 0;
            while (insertAt < icons.Count && icons[insertAt] < orderIcon)
                insertAt++;
            live.Insert(insertAt, kind);
            icons.Insert(insertAt, orderIcon);
            return true;
        }

        bool LiveHasSnapIcon(int orderCol, int orderIcon)
        {
            if ((uint)orderCol >= (uint)_liveSnapIcons.Count) return false;
            var icons = _liveSnapIcons[orderCol];
            for (var i = 0; i < icons.Count; i++)
            {
                if (icons[i] == orderIcon) return true;
            }

            return false;
        }

        void EnsureColumnCapacity(int columnIndex)
        {
            EnsureAtLeastOneColumn();
            while (_columns.Count <= columnIndex)
            {
                _columns.Add(new List<TileKind>());
                _liveSnapIcons.Add(new List<int>());
            }
        }

        int CountInSnapshot(TileKind kind)
        {
            var total = 0;
            if (_snapshotAtFinalize == null) return 0;
            for (var c = 0; c < _snapshotAtFinalize.Count; c++)
                total += CountKind(_snapshotAtFinalize[c], kind);
            return total;
        }

        int CountInLiveColumns(TileKind kind)
        {
            var total = 0;
            for (var c = 0; c < _columns.Count; c++)
                total += CountKind(_columns[c], kind);
            return total;
        }

        static int CountKind(List<TileKind> col, TileKind kind)
        {
            if (col == null) return 0;
            var count = 0;
            for (var i = 0; i < col.Count; i++)
            {
                if (col[i] == kind) count++;
            }

            return count;
        }

        public void StartReverseBuild()
        {
            _reverseQueue.Clear();
            _plannedRackTiles.Clear();
            _reverseIndex = 0;
            _selectedRackTileIndex = -1;

            for (var c = _columns.Count - 1; c >= 0; c--)
            {
                var col = _columns[c];
                if (col == null || col.Count == 0) continue;
                for (var i = col.Count - 1; i >= 0; i--)
                    _reverseQueue.Add(new ReverseItem(c, i, col[i]));
            }

            _reverseActive = _reverseQueue.Count > 0;
        }

        void StartReverseBuildFromSnapshot()
        {
            if (_snapshotAtFinalize == null)
            {
                StopReverseBuild();
                return;
            }

            _reverseQueue.Clear();
            _plannedRackTiles.Clear();
            _reverseIndex = 0;
            _selectedRackTileIndex = -1;

            for (var c = _snapshotAtFinalize.Count - 1; c >= 0; c--)
            {
                var col = _snapshotAtFinalize[c];
                if (col == null || col.Count == 0) continue;
                for (var i = col.Count - 1; i >= 0; i--)
                    _reverseQueue.Add(new ReverseItem(c, i, col[i]));
            }

            _reverseActive = _reverseQueue.Count > 0;
        }

        public void StopReverseBuild()
        {
            _reverseActive = false;
            _reverseIndex = 0;
            _reverseQueue.Clear();
            _plannedRackTiles.Clear();
            _selectedRackTileIndex = -1;
        }

        public bool TryGetCurrentReverseTile(out TileKind tile)
        {
            tile = TileKind.None;
            if (!_reverseActive || _reverseIndex >= _reverseQueue.Count) return false;
            tile = _reverseQueue[_reverseIndex].Kind;
            return tile != TileKind.None;
        }

        public bool TryGetCurrentReverseItem(out int columnIndex, out int tileIndexInColumn, out TileKind kind)
        {
            columnIndex = -1;
            tileIndexInColumn = -1;
            kind = TileKind.None;
            if (!_reverseActive || _reverseIndex >= _reverseQueue.Count) return false;
            var item = _reverseQueue[_reverseIndex];
            columnIndex = item.ColumnIndex;
            tileIndexInColumn = item.TileIndexInColumn;
            kind = item.Kind;
            return kind != TileKind.None;
        }

        public bool TryGetReverseItem(int offsetFromCurrent, out int columnIndex, out int tileIndexInColumn, out TileKind kind)
        {
            columnIndex = -1;
            tileIndexInColumn = -1;
            kind = TileKind.None;
            if (!_reverseActive || _reverseQueue.Count == 0) return false;
            var idx = _reverseIndex + offsetFromCurrent;
            if ((uint)idx >= (uint)_reverseQueue.Count) return false;
            var item = _reverseQueue[idx];
            columnIndex = item.ColumnIndex;
            tileIndexInColumn = item.TileIndexInColumn;
            kind = item.Kind;
            return kind != TileKind.None;
        }

        public void ConsumeCurrentReverseTile(bool toRack)
        {
            if (!TryGetCurrentReverseTile(out var current)) return;
            if (toRack)
            {
                if (_plannedRackTiles.Count >= GameConstants.RackCapacity) return;
                _plannedRackTiles.Add((int)current);
            }

            _reverseIndex++;
            if (_reverseIndex >= _reverseQueue.Count)
                _reverseActive = false;
        }

        public void SyncReverseCursorFromLiveColumns()
        {
            // Live columns represent tiles already collected back into orders while editing.
            // Reverse cursor must match how many matching tiles have been collected.
            if (_reverseQueue.Count == 0) return;

            var collected = 0;
            for (var c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                if (col == null) continue;
                collected += col.Count;
            }

            _reverseIndex = Mathf.Clamp(collected, 0, _reverseQueue.Count);
            _reverseActive = _reverseIndex < _reverseQueue.Count;
        }

        public bool TryCollectCurrentReverseTileToOrders()
        {
            if (!_finalized || _snapshotAtFinalize == null) return false;
            if (!_reverseActive || _reverseIndex >= _reverseQueue.Count) return false;

            var item = _reverseQueue[_reverseIndex];
            if ((uint)item.ColumnIndex >= (uint)_columns.Count) return false;
            if ((uint)item.ColumnIndex >= (uint)_snapshotAtFinalize.Count) return false;

            var snapCol = _snapshotAtFinalize[item.ColumnIndex];
            if (snapCol == null || snapCol.Count == 0) return false;
            if (_columns[item.ColumnIndex].Count >= snapCol.Count) return false;

            // Prefer exact snap slot insertion over naive append.
            return InsertLiveAtSnapSlot(item.ColumnIndex, item.TileIndexInColumn, item.Kind);
        }

        public bool CanSendCurrentToRack() => _plannedRackTiles.Count < GameConstants.RackCapacity;

        public void ToggleRackTileSelection(int index)
        {
            _selectedRackTileIndex = _selectedRackTileIndex == index ? -1 : index;
        }

        public void ClearRackSelection() => _selectedRackTileIndex = -1;

        public bool TryPopSelectedRackTile(out TileKind tile)
        {
            tile = TileKind.None;
            if (_selectedRackTileIndex < 0 || _selectedRackTileIndex >= _plannedRackTiles.Count) return false;
            tile = (TileKind)_plannedRackTiles[_selectedRackTileIndex];
            _plannedRackTiles.RemoveAt(_selectedRackTileIndex);
            if (_selectedRackTileIndex >= _plannedRackTiles.Count)
                _selectedRackTileIndex = _plannedRackTiles.Count - 1;
            return tile != TileKind.None;
        }

        /// <summary>Scene-editor flow: take last tile from rightmost non-empty column into hand.</summary>
        public bool TryConsumeLastTileFromOrdersForPlacement(out TileKind removed, out int sourceColumnIndex) =>
            TryConsumeLastTileFromOrdersForPlacement(out removed, out sourceColumnIndex, out _);

        public bool TryConsumeLastTileFromOrdersForPlacement(
            out TileKind removed,
            out int sourceColumnIndex,
            out int sourceSnapIcon)
        {
            removed = TileKind.None;
            sourceColumnIndex = -1;
            sourceSnapIcon = -1;
            for (var c = _columns.Count - 1; c >= 0; c--)
            {
                var col = _columns[c];
                if (col == null || col.Count == 0) continue;
                sourceColumnIndex = c;
                var last = col.Count - 1;
                removed = col[last];
                sourceSnapIcon = _liveSnapIcons[c][last];
                col.RemoveAt(last);
                _liveSnapIcons[c].RemoveAt(last);
                while (_columns.Count > 1 && _columns[_columns.Count - 1].Count == 0)
                {
                    _columns.RemoveAt(_columns.Count - 1);
                    _liveSnapIcons.RemoveAt(_liveSnapIcons.Count - 1);
                }
                EnsureAtLeastOneColumn();
                return removed != TileKind.None;
            }

            return false;
        }

        public void AppendTileToColumn(int columnIndex, TileKind kind) =>
            AppendTileToColumn(columnIndex, kind, -1);

        public void AppendTileToColumn(int columnIndex, TileKind kind, int snapIcon)
        {
            if (kind == TileKind.None || columnIndex < 0) return;
            EnsureColumnCapacity(columnIndex);
            if (snapIcon < 0)
            {
                // Unknown origin: append and stamp a provisional trailing index.
                snapIcon = _columns[columnIndex].Count;
                while (LiveHasSnapIcon(columnIndex, snapIcon))
                    snapIcon++;
            }

            if (!InsertLiveAtSnapSlot(columnIndex, snapIcon, kind))
            {
                // Slot already occupied — still keep the tile visible as a trailing entry.
                _columns[columnIndex].Add(kind);
                _liveSnapIcons[columnIndex].Add(snapIcon);
            }
        }

        public bool TryTakeActiveOrderTileToHand(int columnIndex, int tileIndex, out TileKind taken) =>
            TryTakeActiveOrderTileToHand(columnIndex, tileIndex, out taken, out _);

        public bool TryTakeActiveOrderTileToHand(int columnIndex, int tileIndex, out TileKind taken, out int snapIcon)
        {
            taken = TileKind.None;
            snapIcon = -1;
            if (!_finalized) return false;
            RecomputeActiveColumnIndices(out var active);
            if (!active.Contains(columnIndex)) return false;
            return TryTakeOrderTileAt(columnIndex, tileIndex, out taken, out snapIcon);
        }

        /// <summary>Pick any order icon into the hand (used while unlocked but still placing/removing on the board).</summary>
        public bool TryTakeOrderTileToHand(int columnIndex, int tileIndex, out TileKind taken) =>
            TryTakeOrderTileAt(columnIndex, tileIndex, out taken, out _);

        public bool TryTakeOrderTileToHand(int columnIndex, int tileIndex, out TileKind taken, out int snapIcon) =>
            TryTakeOrderTileAt(columnIndex, tileIndex, out taken, out snapIcon);

        bool TryTakeOrderTileAt(int columnIndex, int tileIndex, out TileKind taken, out int snapIcon)
        {
            taken = TileKind.None;
            snapIcon = -1;
            if ((uint)columnIndex >= (uint)_columns.Count) return false;
            var col = _columns[columnIndex];
            if (col == null || (uint)tileIndex >= (uint)col.Count) return false;
            taken = col[tileIndex];
            snapIcon = _liveSnapIcons[columnIndex][tileIndex];
            col.RemoveAt(tileIndex);
            _liveSnapIcons[columnIndex].RemoveAt(tileIndex);
            while (_columns.Count > 1 && _columns[_columns.Count - 1].Count == 0)
            {
                _columns.RemoveAt(_columns.Count - 1);
                _liveSnapIcons.RemoveAt(_liveSnapIcons.Count - 1);
            }
            EnsureAtLeastOneColumn();
            return taken != TileKind.None;
        }

        public void RecomputeActiveColumnIndices(out HashSet<int> active)
        {
            active = new HashSet<int>();
            var nonempty = new List<int>();
            for (var i = 0; i < _columns.Count; i++)
            {
                if (_columns[i] != null && _columns[i].Count > 0)
                    nonempty.Add(i);
            }

            if (nonempty.Count == 0) return;
            nonempty.Sort();
            var n = nonempty.Count;
            active.Add(nonempty[n - 1]);
            if (n >= 2)
                active.Add(nonempty[n - 2]);
        }

        public bool AreAllColumnsEmpty()
        {
            for (var i = 0; i < _columns.Count; i++)
            {
                if (_columns[i] != null && _columns[i].Count > 0)
                    return false;
            }

            return true;
        }

        public bool TryGetSnapshotOrdersForExport(out List<List<int>> orders, out string error)
        {
            orders = null;
            error = null;
            if (_snapshotAtFinalize == null)
            {
                error = "No finalized order snapshot. Finalize orders again.";
                return false;
            }

            orders = new List<List<int>>();
            var total = 0;
            for (var c = 0; c < _snapshotAtFinalize.Count; c++)
            {
                var col = _snapshotAtFinalize[c];
                if (col == null || col.Count == 0) continue;
                var row = new List<int>(col.Count);
                for (var i = 0; i < col.Count; i++)
                    row.Add((int)col[i]);
                orders.Add(row);
                total += row.Count;
            }

            if (total < 1)
            {
                error = "Finalized orders snapshot is empty.";
                return false;
            }

            return true;
        }

        public List<List<int>> BuildLiveOrdersDto()
        {
            var orders = new List<List<int>>();
            for (var c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                if (col == null || col.Count == 0) continue;
                var row = new List<int>(col.Count);
                for (var i = 0; i < col.Count; i++)
                    row.Add((int)col[i]);
                orders.Add(row);
            }

            return orders;
        }

        void CaptureSnapshot()
        {
            _snapshotAtFinalize = new List<List<TileKind>>();
            for (var i = 0; i < _columns.Count; i++)
            {
                var col = _columns[i];
                _snapshotAtFinalize.Add(col != null ? new List<TileKind>(col) : new List<TileKind>());
                // Re-index live snap icons 0..n-1 to match the new snapshot.
                EnsureColumnCapacity(i);
                var icons = _liveSnapIcons[i];
                icons.Clear();
                var n = _columns[i].Count;
                for (var j = 0; j < n; j++)
                    icons.Add(j);
            }
        }

        void RestoreColumnsFromSnapshot()
        {
            _columns.Clear();
            _liveSnapIcons.Clear();
            if (_snapshotAtFinalize != null)
            {
                for (var i = 0; i < _snapshotAtFinalize.Count; i++)
                {
                    var src = _snapshotAtFinalize[i];
                    var kinds = src != null ? new List<TileKind>(src) : new List<TileKind>();
                    var icons = new List<int>(kinds.Count);
                    for (var j = 0; j < kinds.Count; j++)
                        icons.Add(j);
                    _columns.Add(kinds);
                    _liveSnapIcons.Add(icons);
                }
            }

            EnsureAtLeastOneColumn();
        }
    }
}
#endif
