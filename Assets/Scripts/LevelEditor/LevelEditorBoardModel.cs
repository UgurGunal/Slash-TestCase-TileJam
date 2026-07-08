#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Core;
using LevelData;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>Placement-slot board, rack, and JSON import/export for the level editor window.</summary>
    sealed class LevelEditorBoardModel
    {
        TileKind?[] _rackSlots;
        TileKind?[, ,] _cells;
        // Parallel to _cells: original order column / icon index (-1 = unknown).
        int[, ,] _orderCol;
        int[, ,] _orderIcon;
        int[] _rackOrderCol;
        int[] _rackOrderIcon;
        int _majorWidth = 4;
        int _majorHeight = 4;
        int _depth = 16;

        public int MajorWidth
        {
            get => _majorWidth;
            set => _majorWidth = Mathf.Max(1, value);
        }

        public int MajorHeight
        {
            get => _majorHeight;
            set => _majorHeight = Mathf.Max(1, value);
        }

        public int Depth
        {
            get => _depth;
            set => _depth = Mathf.Max(1, value);
        }

        public int PlacementWidth { get; private set; }
        public int PlacementHeight { get; private set; }
        public TileKind?[, ,] Cells => _cells;
        public TileKind?[] RackSlots => _rackSlots;

        public static void GetPlacementSlotCounts(int majorWidth, int majorHeight, out int placeW, out int placeH)
        {
            placeW = majorWidth <= 1 ? 3 : 2 * majorWidth - 1;
            placeH = majorHeight <= 1 ? 3 : 2 * majorHeight - 1;
        }

        public static void InferMajorFromPlacement(int placeW, int placeH, out int majorW, out int majorH)
        {
            majorW = placeW <= 3 ? 1 : (placeW + 1) / 2;
            majorH = placeH <= 3 ? 1 : (placeH + 1) / 2;
        }

        public void EnsureGrid(bool preserveOverlap = true)
        {
            GetPlacementSlotCounts(_majorWidth, _majorHeight, out var pw, out var ph);
            PlacementWidth = pw;
            PlacementHeight = ph;
            _depth = Mathf.Max(1, _depth);

            var next = new TileKind?[pw, ph, _depth];
            var nextCol = new int[pw, ph, _depth];
            var nextIcon = new int[pw, ph, _depth];
            for (var x = 0; x < pw; x++)
            for (var y = 0; y < ph; y++)
            for (var l = 0; l < _depth; l++)
            {
                nextCol[x, y, l] = -1;
                nextIcon[x, y, l] = -1;
            }

            if (preserveOverlap && _cells != null)
            {
                var cw = Mathf.Min(pw, _cells.GetLength(0));
                var ch = Mathf.Min(ph, _cells.GetLength(1));
                var cd = Mathf.Min(_depth, _cells.GetLength(2));
                for (var x = 0; x < cw; x++)
                for (var y = 0; y < ch; y++)
                for (var l = 0; l < cd; l++)
                {
                    next[x, y, l] = _cells[x, y, l];
                    if (_orderCol != null)
                    {
                        nextCol[x, y, l] = _orderCol[x, y, l];
                        nextIcon[x, y, l] = _orderIcon[x, y, l];
                    }
                }
            }

            _cells = next;
            _orderCol = nextCol;
            _orderIcon = nextIcon;
            if (_rackSlots == null || _rackSlots.Length != GameConstants.RackCapacity)
            {
                _rackSlots = new TileKind?[GameConstants.RackCapacity];
                _rackOrderCol = new int[GameConstants.RackCapacity];
                _rackOrderIcon = new int[GameConstants.RackCapacity];
                for (var i = 0; i < GameConstants.RackCapacity; i++)
                {
                    _rackOrderCol[i] = -1;
                    _rackOrderIcon[i] = -1;
                }
            }
        }

        public void ClearBoardAndRack()
        {
            if (_cells != null)
            {
                var pw = _cells.GetLength(0);
                var ph = _cells.GetLength(1);
                var d = _cells.GetLength(2);
                for (var x = 0; x < pw; x++)
                for (var y = 0; y < ph; y++)
                for (var l = 0; l < d; l++)
                {
                    _cells[x, y, l] = null;
                    _orderCol[x, y, l] = -1;
                    _orderIcon[x, y, l] = -1;
                }
            }

            ClearRack();
        }

        public void ClearRack()
        {
            if (_rackSlots == null) return;
            for (var i = 0; i < _rackSlots.Length; i++)
            {
                _rackSlots[i] = null;
                if (_rackOrderCol != null)
                {
                    _rackOrderCol[i] = -1;
                    _rackOrderIcon[i] = -1;
                }
            }
        }

        public void LoadFromSpec(LevelBoardSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            InferMajorFromPlacement(spec.Width, spec.Height, out _majorWidth, out _majorHeight);
            _depth = Mathf.Max(1, spec.Depth);
            EnsureGrid(preserveOverlap: false);

            for (var l = 0; l < spec.Depth; l++)
            for (var y = 0; y < spec.Height; y++)
            for (var x = 0; x < spec.Width; x++)
            {
                if (x < PlacementWidth && y < PlacementHeight && l < _depth)
                {
                    _cells[x, y, l] = spec.GetNullable(x, y, l);
                    _orderCol[x, y, l] = -1;
                    _orderIcon[x, y, l] = -1;
                }
            }
        }

        /// <summary>
        /// Assign each board tile an order origin by matching kinds to unused snapshot slots
        /// (column-major, layer-major board walk). Used when loading an existing level.
        /// </summary>
        public void AssignOriginsFromSnapshot(IReadOnlyList<IReadOnlyList<TileKind>> snapshot)
        {
            if (_cells == null || snapshot == null) return;

            var used = new bool[snapshot.Count][];
            for (var c = 0; c < snapshot.Count; c++)
            {
                var col = snapshot[c];
                used[c] = col == null ? Array.Empty<bool>() : new bool[col.Count];
            }

            var pw = _cells.GetLength(0);
            var ph = _cells.GetLength(1);
            var d = _cells.GetLength(2);
            for (var l = 0; l < d; l++)
            for (var y = 0; y < ph; y++)
            for (var x = 0; x < pw; x++)
            {
                var v = _cells[x, y, l];
                if (!v.HasValue) continue;
                if (_orderCol[x, y, l] >= 0) continue;
                if (!TryClaimSnapshotSlot(snapshot, used, v.Value, out var oc, out var oi))
                    continue;
                _orderCol[x, y, l] = oc;
                _orderIcon[x, y, l] = oi;
            }
        }

        static bool TryClaimSnapshotSlot(
            IReadOnlyList<IReadOnlyList<TileKind>> snapshot,
            bool[][] used,
            TileKind kind,
            out int orderCol,
            out int orderIcon)
        {
            orderCol = -1;
            orderIcon = -1;
            for (var c = 0; c < snapshot.Count; c++)
            {
                var col = snapshot[c];
                if (col == null) continue;
                for (var i = 0; i < col.Count; i++)
                {
                    if (used[c][i]) continue;
                    if (col[i] != kind) continue;
                    used[c][i] = true;
                    orderCol = c;
                    orderIcon = i;
                    return true;
                }
            }

            return false;
        }

        public int ComputePlacementLayer(int px, int py)
        {
            if (_cells == null) return 0;
            var pw = _cells.GetLength(0);
            var ph = _cells.GetLength(1);
            var d = _cells.GetLength(2);
            var maxL = -1;
            for (var x = 0; x < pw; x++)
            for (var y = 0; y < ph; y++)
            {
                if (Mathf.Abs(x - px) > 1 || Mathf.Abs(y - py) > 1)
                    continue;
                for (var l = 0; l < d; l++)
                {
                    if (_cells[x, y, l].HasValue)
                        maxL = Mathf.Max(maxL, l);
                }
            }

            return maxL + 1;
        }

        public bool TryGetTopTileAt(int px, int py, out int layer, out TileKind kind)
        {
            layer = -1;
            kind = TileKind.None;
            if (_cells == null) return false;
            var d = _cells.GetLength(2);
            for (var l = d - 1; l >= 0; l--)
            {
                var v = _cells[px, py, l];
                if (!v.HasValue) continue;
                layer = l;
                kind = v.Value;
                return true;
            }

            return false;
        }

        public bool CanPlaceAt(int px, int py)
        {
            if (_cells == null) return false;
            if ((uint)px >= (uint)PlacementWidth || (uint)py >= (uint)PlacementHeight) return false;
            var layer = ComputePlacementLayer(px, py);
            if (layer < 0 || layer >= _depth) return false;
            return !_cells[px, py, layer].HasValue;
        }

        public bool TryPlaceAt(int px, int py, TileKind kind) =>
            TryPlaceAt(px, py, kind, -1, -1);

        public bool TryPlaceAt(int px, int py, TileKind kind, int orderCol, int orderIcon)
        {
            if (kind == TileKind.None || !CanPlaceAt(px, py)) return false;
            var layer = ComputePlacementLayer(px, py);
            _cells[px, py, layer] = kind;
            _orderCol[px, py, layer] = orderCol;
            _orderIcon[px, py, layer] = orderIcon;
            return true;
        }

        public bool TryRemoveTopAt(int px, int py)
        {
            return TryRemoveTopAt(px, py, out _, out _, out _, out _);
        }

        public bool TryRemoveTopAt(int px, int py, out TileKind removed, out int layer)
        {
            return TryRemoveTopAt(px, py, out removed, out layer, out _, out _);
        }

        public bool TryRemoveTopAt(
            int px,
            int py,
            out TileKind removed,
            out int layer,
            out int orderCol,
            out int orderIcon)
        {
            removed = TileKind.None;
            layer = -1;
            orderCol = -1;
            orderIcon = -1;
            if (_cells == null) return false;
            if (!TryGetTopTileAt(px, py, out layer, out removed)) return false;
            orderCol = _orderCol[px, py, layer];
            orderIcon = _orderIcon[px, py, layer];
            _cells[px, py, layer] = null;
            _orderCol[px, py, layer] = -1;
            _orderIcon[px, py, layer] = -1;
            return true;
        }

        public bool RackHasAnyTile()
        {
            if (_rackSlots == null) return false;
            for (var i = 0; i < _rackSlots.Length; i++)
            {
                if (_rackSlots[i].HasValue) return true;
            }

            return false;
        }

        public void CollectTilesOriginatingFromOrderColumnAtLeast(
            int minOrderCol,
            List<(TileKind kind, int orderCol, int orderIcon)> results)
        {
            if (results == null) return;
            if (_cells != null)
            {
                var pw = _cells.GetLength(0);
                var ph = _cells.GetLength(1);
                var d = _cells.GetLength(2);
                for (var l = 0; l < d; l++)
                for (var y = 0; y < ph; y++)
                for (var x = 0; x < pw; x++)
                {
                    var v = _cells[x, y, l];
                    if (!v.HasValue) continue;
                    var col = _orderCol[x, y, l];
                    if (col < minOrderCol) continue;
                    results.Add((v.Value, col, _orderIcon[x, y, l]));
                }
            }

            if (_rackSlots == null || _rackOrderCol == null) return;
            for (var i = 0; i < _rackSlots.Length; i++)
            {
                if (!_rackSlots[i].HasValue) continue;
                var col = _rackOrderCol[i];
                if (col < minOrderCol) continue;
                results.Add((_rackSlots[i].Value, col, _rackOrderIcon[i]));
            }
        }

        public int GetMaxOriginOrderColumn()
        {
            var max = -1;
            if (_cells != null)
            {
                var pw = _cells.GetLength(0);
                var ph = _cells.GetLength(1);
                var d = _cells.GetLength(2);
                for (var l = 0; l < d; l++)
                for (var y = 0; y < ph; y++)
                for (var x = 0; x < pw; x++)
                {
                    if (!_cells[x, y, l].HasValue) continue;
                    max = Mathf.Max(max, _orderCol[x, y, l]);
                }
            }

            if (_rackSlots != null && _rackOrderCol != null)
            {
                for (var i = 0; i < _rackSlots.Length; i++)
                {
                    if (!_rackSlots[i].HasValue) continue;
                    max = Mathf.Max(max, _rackOrderCol[i]);
                }
            }

            return max;
        }

        public void RemapOrderColumnIndices(IReadOnlyDictionary<int, int> oldToNew, int minAffectedColumn)
        {
            if (oldToNew == null || oldToNew.Count == 0) return;

            if (_cells != null)
            {
                var pw = _cells.GetLength(0);
                var ph = _cells.GetLength(1);
                var d = _cells.GetLength(2);
                for (var l = 0; l < d; l++)
                for (var y = 0; y < ph; y++)
                for (var x = 0; x < pw; x++)
                {
                    if (!_cells[x, y, l].HasValue) continue;
                    var col = _orderCol[x, y, l];
                    if (col < minAffectedColumn) continue;
                    if (oldToNew.TryGetValue(col, out var mapped))
                        _orderCol[x, y, l] = mapped;
                }
            }

            if (_rackSlots == null || _rackOrderCol == null) return;
            for (var i = 0; i < _rackSlots.Length; i++)
            {
                if (!_rackSlots[i].HasValue) continue;
                var col = _rackOrderCol[i];
                if (col < minAffectedColumn) continue;
                if (oldToNew.TryGetValue(col, out var mapped))
                    _rackOrderCol[i] = mapped;
            }
        }

        public bool TryPlaceInFirstEmptyRack(TileKind kind) =>
            TryPlaceInFirstEmptyRack(kind, -1, -1);

        public bool TryPlaceInFirstEmptyRack(TileKind kind, int orderCol, int orderIcon)
        {
            if (_rackSlots == null || kind == TileKind.None) return false;
            for (var i = 0; i < _rackSlots.Length; i++)
            {
                if (!_rackSlots[i].HasValue)
                    return TryPlaceInRack(i, kind, orderCol, orderIcon);
            }

            return false;
        }

        public bool TryPlaceInRack(int slot, TileKind kind) =>
            TryPlaceInRack(slot, kind, -1, -1);

        public bool TryPlaceInRack(int slot, TileKind kind, int orderCol, int orderIcon)
        {
            if (_rackSlots == null || slot < 0 || slot >= _rackSlots.Length || kind == TileKind.None)
                return false;
            if (_rackSlots[slot].HasValue) return false;
            _rackSlots[slot] = kind;
            if (_rackOrderCol != null)
            {
                _rackOrderCol[slot] = orderCol;
                _rackOrderIcon[slot] = orderIcon;
            }

            return true;
        }

        public bool TryTakeFromRack(int slot, out TileKind kind) =>
            TryTakeFromRack(slot, out kind, out _, out _);

        public bool TryTakeFromRack(int slot, out TileKind kind, out int orderCol, out int orderIcon)
        {
            kind = TileKind.None;
            orderCol = -1;
            orderIcon = -1;
            if (_rackSlots == null || slot < 0 || slot >= _rackSlots.Length) return false;
            if (!_rackSlots[slot].HasValue) return false;
            kind = _rackSlots[slot].Value;
            if (_rackOrderCol != null)
            {
                orderCol = _rackOrderCol[slot];
                orderIcon = _rackOrderIcon[slot];
                _rackOrderCol[slot] = -1;
                _rackOrderIcon[slot] = -1;
            }

            _rackSlots[slot] = null;
            return true;
        }

        public bool TrySwapRack(int slot, TileKind handKind, out TileKind taken)
        {
            taken = TileKind.None;
            if (_rackSlots == null || slot < 0 || slot >= _rackSlots.Length) return false;
            if (!_rackSlots[slot].HasValue) return false;
            taken = _rackSlots[slot].Value;
            _rackSlots[slot] = handKind == TileKind.None ? null : handKind;
            return true;
        }

        public List<int> CollectBoardKindInts()
        {
            var kinds = new List<int>();
            if (_cells == null) return kinds;
            var pw = _cells.GetLength(0);
            var ph = _cells.GetLength(1);
            var d = _cells.GetLength(2);
            for (var l = 0; l < d; l++)
            for (var y = 0; y < ph; y++)
            for (var x = 0; x < pw; x++)
            {
                var v = _cells[x, y, l];
                if (v.HasValue)
                    kinds.Add((int)v.Value);
            }

            return kinds;
        }

        public Matrix3DLevelJsonDto BuildDto()
        {
            if (_cells == null) EnsureGrid();
            var pw = _cells.GetLength(0);
            var ph = _cells.GetLength(1);
            var depth = _cells.GetLength(2);
            var layers = new List<List<List<int>>>();
            for (var l = 0; l < depth; l++)
            {
                var layer = new List<List<int>>();
                for (var y = 0; y < ph; y++)
                {
                    var row = new List<int>();
                    for (var x = 0; x < pw; x++)
                    {
                        var v = _cells[x, y, l];
                        row.Add(v.HasValue ? (int)v.Value : -1);
                    }

                    layer.Add(row);
                }

                layers.Add(layer);
            }

            return new Matrix3DLevelJsonDto
            {
                Width = pw,
                Height = ph,
                Depth = depth,
                Matrix3D = layers,
            };
        }

        public static bool MultisetsEqualSorted(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            a.Sort();
            b.Sort();
            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }

        public static List<int> FlattenOrders(IReadOnlyList<List<int>> orders)
        {
            var flat = new List<int>();
            if (orders == null) return flat;
            for (var oi = 0; oi < orders.Count; oi++)
            {
                var row = orders[oi];
                if (row == null) continue;
                for (var i = 0; i < row.Count; i++)
                    flat.Add(row[i]);
            }

            return flat;
        }
    }
}
#endif
