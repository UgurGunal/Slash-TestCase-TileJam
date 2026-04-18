using System.Collections.Generic;
using Core;
using LevelData;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>High-level editor mode: after orders are finalized, the last tile of the last order is held until placed on the rack or board grid.</summary>
    public enum LevelEditorAuthoringPhase
    {
        OrderCreation = 0,
        PlacingTilesFromOrders = 1,
    }

    /// <summary>
    /// After <see cref="LevelEditorOrderAuthoringPanel"/> finalizes orders, the last tile in the last order column is shown in <see cref="handTileParent"/>.
    /// Rack: an <b>empty</b> slot stores the hand tile (advances order queue). An <b>occupied</b> slot picks that tile into the hand; order hand goes back to its column; rack hand <b>swaps</b> into the clicked slot.
    /// Advancing the hand removes the next order-column tile when available. If orders are empty but the rack still has tiles, placement continues with an empty hand until you pick from the rack. Exits to order authoring only when both queue and rack are done.
    /// Grid: half-cell placement lattice (2W−1)×(2H−1) slots; layer 0 on empty board; Chebyshev |Δ|≤1 in slot indices for stacking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelEditorTilePlacementController : MonoBehaviour
    {
        [SerializeField] LevelEditorOrderAuthoringPanel orderPanel;
        [SerializeField] LevelEditorGridLinesView grid;
        [SerializeField] RectTransform boardTilesRoot;
        [SerializeField] BoardTileView tilePrefab;
        [SerializeField] TileIconLibrary iconLibrary;
        [Tooltip("Hand preview (last order tile). Tile is instantiated here until placed.")]
        [SerializeField] RectTransform handTileParent;
        [Tooltip("Visual size of board tiles vs placement slot (same idea as gameplay board).")]
        [SerializeField] [Range(0.05f, 1.5f)] float boardTileScaleInCell = 0.92f;
        [Tooltip("Max stacking depth for placement (board JSON depth).")]
        [SerializeField] int maxBoardDepth = 16;
        [SerializeField] Canvas canvasOverride;
        [Tooltip("Optional: six UI slot RectTransforms (e.g. left→right). Pointer over an empty slot sends the tile to the rack instead of the grid. Leave empty to disable rack.")]
        [SerializeField] RectTransform[] editorRackSlotRects;
        [Tooltip("Extra multiplier on rack tile scale vs order-column tiles (1 = match orders). Order tiles use Order Tile Cell Size + Order Tile Scale on the order panel.")]
        [SerializeField] [Range(0.2f, 1.5f)] float rackTileScaleInCell = 1f;

        LevelEditorAuthoringPhase _phase = LevelEditorAuthoringPhase.OrderCreation;
        TileKind _handKind = TileKind.None;
        BoardTileView _handView;
        TileKind?[, ,] _cells;
        TileKind?[] _rackSlots;
        BoardTileView[] _rackTileViews;
        readonly Dictionary<(int x, int y, int z), BoardTileView> _tileViews = new Dictionary<(int x, int y, int z), BoardTileView>();

        /// <summary>True when the hand shows a tile taken from the rack (grid/rack placement does not consume the order queue).</summary>
        bool _handFromRack;

        /// <summary>Order column index (0 = leftmost) for the current hand tile when it came from <see cref="LevelEditorOrderAuthoringPanel"/>; used to return a tile to the same customer when picking from the rack.</summary>
        int _handOrderColumnIndex = -1;

        public LevelEditorAuthoringPhase Phase => _phase;

        void OnEnable()
        {
            if (orderPanel != null)
            {
                orderPanel.OnOrdersAuthoringFinalized += EnterPlacementFromOrders;
                orderPanel.OnOrdersAuthoringUnlocked += ExitPlacement;
            }
        }

        void OnDisable()
        {
            if (orderPanel != null)
            {
                orderPanel.OnOrdersAuthoringFinalized -= EnterPlacementFromOrders;
                orderPanel.OnOrdersAuthoringUnlocked -= ExitPlacement;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (editorRackSlotRects != null && editorRackSlotRects.Length > 0 &&
                editorRackSlotRects.Length != GameConstants.RackCapacity)
            {
                Debug.LogWarning(
                    $"[LevelEditorTilePlacementController] Assign exactly {GameConstants.RackCapacity} entries in {nameof(editorRackSlotRects)} (or leave empty to disable rack).",
                    this);
            }
        }
#endif

        void Update()
        {
            if (_phase != LevelEditorAuthoringPhase.PlacingTilesFromOrders)
                return;
            // Allow input with an empty hand while the rack still has tiles (orders finished; pick from rack to place on grid).
            if (_handKind == TileKind.None && !RackHasAnyTile())
                return;
            if (orderPanel != null && !orderPanel.OrdersAuthoringFinalized)
            {
                ExitPlacement();
                return;
            }

            if (!Input.GetMouseButtonDown(0))
                return;

            if (tilePrefab == null || _cells == null)
                return;

            var canvas = canvasOverride != null ? canvasOverride : GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            if (TryHandleRackClick(canvas, cam))
                return;

            if (_handKind == TileKind.None)
                return;

            if (grid == null || boardTilesRoot == null)
                return;

            var linesRt = grid.LinesContentRect;
            if (linesRt == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(linesRt, Input.mousePosition, cam, out var localInLines))
                return;
            if (!linesRt.rect.Contains(localInLines))
                return;

            var majorW = grid.GridWidthCells;
            var majorH = grid.GridHeightCells;
            var cw = grid.GridCellWidth;
            var ch = grid.GridCellHeight;
            var gridW = majorW * cw;
            var gridH = majorH * ch;

            LevelEditorGridLinesView.LocalToPlacementIndices(localInLines, majorW, majorH, gridW, gridH, out var px, out var py);

            var layer = ComputePlacementLayer(px, py);
            if (layer < 0 || layer >= maxBoardDepth)
            {
                Debug.LogWarning($"[LevelEditorTilePlacementController] Cannot place: layer {layer} out of range [0,{maxBoardDepth}).");
                return;
            }

            if (_cells[px, py, layer].HasValue)
            {
                Debug.LogWarning("[LevelEditorTilePlacementController] Target slot/layer already occupied.");
                return;
            }

            PlaceTileAt(px, py, layer, _handKind);
            RefreshTileClickabilityVisuals();
            AdvanceHandAfterSuccessfulPlacement();
        }

        bool TryHandleRackClick(Canvas canvas, Camera cam)
        {
            if (_rackSlots == null || editorRackSlotRects == null || editorRackSlotRects.Length == 0)
                return false;
            if (!TryGetRackSlotUnderPointer(canvas, cam, out var slot))
                return false;

            if (_rackSlots[slot].HasValue)
            {
                PickHandFromOccupiedRackSlot(slot);
                return true;
            }

            if (_handKind == TileKind.None)
                return false;

            if (_handFromRack)
            {
                PlaceTileInRack(slot, _handKind);
                AdvanceHandAfterSuccessfulPlacement();
                return true;
            }

            PlaceTileInRack(slot, _handKind);
            AdvanceHandAfterSuccessfulPlacement();
            return true;
        }

        /// <summary>Occupied rack slot: take its tile into the hand. Order hand → previous tile returns to its order column. Rack hand → <b>swap</b>: previous hand tile fills the <i>clicked</i> slot (same slot as the tile you picked up).</summary>
        void PickHandFromOccupiedRackSlot(int slot)
        {
            if (_rackSlots == null || !_rackSlots[slot].HasValue) return;

            var taken = _rackSlots[slot].Value;
            var previousHand = _handKind;
            var previousFromRack = _handFromRack;
            ClearRackSlotAt(slot);

            if (!previousFromRack && previousHand != TileKind.None && orderPanel != null && _handOrderColumnIndex >= 0)
                orderPanel.AppendTileToOrderColumnIndex(_handOrderColumnIndex, previousHand);

            if (previousFromRack && previousHand != TileKind.None &&
                editorRackSlotRects != null && slot >= 0 && slot < editorRackSlotRects.Length &&
                editorRackSlotRects[slot] != null)
                PlaceTileInRack(slot, previousHand);

            _handKind = taken;
            _handFromRack = true;
            _handOrderColumnIndex = -1;
            RebuildHandVisual();
        }

        void ClearRackSlotAt(int slot)
        {
            if (_rackTileViews != null && slot >= 0 && slot < _rackTileViews.Length && _rackTileViews[slot] != null)
            {
                Destroy(_rackTileViews[slot].gameObject);
                _rackTileViews[slot] = null;
            }

            if (_rackSlots != null && slot >= 0 && slot < _rackSlots.Length)
                _rackSlots[slot] = null;
        }

        bool TryGetRackSlotUnderPointer(Canvas canvas, Camera cam, out int slotIndex)
        {
            slotIndex = -1;
            var screen = Input.mousePosition;
            for (var i = 0; i < editorRackSlotRects.Length && i < GameConstants.RackCapacity; i++)
            {
                var rt = editorRackSlotRects[i];
                if (rt == null) continue;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, cam, out var local) &&
                    rt.rect.Contains(local))
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        void PlaceTileInRack(int slotIndex, TileKind kind)
        {
            ClearRackSlotAt(slotIndex);
            _rackSlots[slotIndex] = kind;
            var parent = editorRackSlotRects[slotIndex];
            var view = Instantiate(tilePrefab, parent, false);
            view.gameObject.name = $"Rack_{slotIndex}_{kind}";
            var tileRt = (RectTransform)view.transform;
            ConfigureTileVisualLikeOrders(view, kind, tileRt, slotIndex, -1, -1, rackTileScaleInCell);
            view.SetClickableVisual(true);
            view.SetClickHandler(null);
            _rackTileViews[slotIndex] = view;
        }

        /// <summary>Same as order-column tiles: <see cref="LevelEditorOrderAuthoringPanel.OrderTileCellSize"/> + Bind scale 1 + uniform scale.</summary>
        void ConfigureTileVisualLikeOrders(
            BoardTileView view,
            TileKind kind,
            RectTransform tileRt,
            int gridX,
            int gridY,
            int layerIndex,
            float extraUniformScale)
        {
            tileRt.anchorMin = tileRt.anchorMax = tileRt.pivot = new Vector2(0.5f, 0.5f);
            tileRt.anchoredPosition = Vector2.zero;
            tileRt.localScale = Vector3.one;
            StripLayoutElementIfAny(view.gameObject);

            Vector2 cell;
            float uniformScale;
            if (orderPanel != null)
            {
                cell = orderPanel.OrderTileCellSize;
                uniformScale = orderPanel.OrderTileUniformScale * extraUniformScale;
            }
            else
            {
                var cw = grid.GridCellWidth;
                var ch = grid.GridCellHeight;
                cell = new Vector2(cw, ch);
                uniformScale = boardTileScaleInCell * extraUniformScale;
            }

            view.Bind(kind, gridX, gridY, layerIndex, Vector2.zero, cell, 1f, iconLibrary);
            ApplyUniformVisualScale(tileRt, uniformScale);
        }

        static void ApplyUniformVisualScale(RectTransform tileRt, float uniformScale)
        {
            var s = Mathf.Max(0.01f, uniformScale);
            tileRt.localScale = new Vector3(s, s, 1f);
        }

        static void StripLayoutElementIfAny(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) return;
            Destroy(le);
        }

        void AdvanceHandAfterSuccessfulPlacement()
        {
            _handFromRack = false;
            if (orderPanel == null ||
                !orderPanel.TryConsumeLastTileFromOrdersForPlacement(out var removed, out var sourceCol) ||
                removed == TileKind.None)
            {
                if (RackHasAnyTile())
                {
                    ClearHandViewOnly();
                    _handKind = TileKind.None;
                    _handOrderColumnIndex = -1;
                    return;
                }

                ClearHand();
                _phase = LevelEditorAuthoringPhase.OrderCreation;
                return;
            }

            _handKind = removed;
            _handOrderColumnIndex = sourceCol;
            RebuildHandVisual();
        }

        bool RackHasAnyTile()
        {
            if (_rackSlots == null) return false;
            for (var i = 0; i < _rackSlots.Length; i++)
            {
                if (_rackSlots[i].HasValue)
                    return true;
            }

            return false;
        }

        void EnterPlacementFromOrders()
        {
            if (grid == null || boardTilesRoot == null || tilePrefab == null)
            {
                Debug.LogWarning("[LevelEditorTilePlacementController] Assign grid, board tiles root, and tile prefab.");
                return;
            }

            if (orderPanel == null || !orderPanel.TryGetLastTileOfLastOrder(out var kind) || kind == TileKind.None)
            {
                Debug.LogWarning("[LevelEditorTilePlacementController] Finalize orders: no last tile found for placement.");
                return;
            }

            if (!orderPanel.TryConsumeLastTileFromOrdersForPlacement(out var consumed, out var sourceCol) ||
                consumed != kind)
            {
                Debug.LogWarning("[LevelEditorTilePlacementController] Could not reserve hand tile from order column.");
                return;
            }

            LevelEditorGridLinesView.GetPlacementSlotCounts(grid.GridWidthCells, grid.GridHeightCells, out var pw, out var ph);
            var d = Mathf.Max(1, maxBoardDepth);
            _cells = new TileKind?[pw, ph, d];
            _tileViews.Clear();
            ClearChildren(boardTilesRoot);
            ClearRackState();
            _rackSlots = new TileKind?[GameConstants.RackCapacity];
            _rackTileViews = new BoardTileView[GameConstants.RackCapacity];

            _handKind = kind;
            _handOrderColumnIndex = sourceCol;
            _handFromRack = false;
            _phase = LevelEditorAuthoringPhase.PlacingTilesFromOrders;
            RebuildHandVisual();
        }

        void ExitPlacement()
        {
            _phase = LevelEditorAuthoringPhase.OrderCreation;
            _handFromRack = false;
            _handOrderColumnIndex = -1;
            _handKind = TileKind.None;
            ClearHandViewOnly();
            _cells = null;
            _tileViews.Clear();
            ClearRackState();
            _rackSlots = null;
            _rackTileViews = null;
            if (boardTilesRoot != null)
                ClearChildren(boardTilesRoot);
        }

        void ClearRackState()
        {
            if (_rackTileViews == null) return;
            for (var i = 0; i < _rackTileViews.Length; i++)
            {
                var v = _rackTileViews[i];
                if (v == null) continue;
                Destroy(v.gameObject);
                _rackTileViews[i] = null;
            }
        }

        int ComputePlacementLayer(int px, int py)
        {
            if (_cells == null)
                return 0;

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

        void PlaceTileAt(int px, int py, int layer, TileKind kind)
        {
            _cells[px, py, layer] = kind;
            var majorW = grid.GridWidthCells;
            var majorH = grid.GridHeightCells;
            var cw = grid.GridCellWidth;
            var ch = grid.GridCellHeight;
            var gridW = majorW * cw;
            var gridH = majorH * ch;
            var pos = LevelEditorGridLinesView.PlacementSlotCenterLocal(px, py, gridW, gridH, majorW, majorH);
            var cell = new Vector2(cw, ch);
            var view = Instantiate(tilePrefab, boardTilesRoot, false);
            view.gameObject.name = $"Place_L{layer}_P{py}_P{px}";
            view.Bind(kind, px, py, layer, pos, cell, boardTileScaleInCell, iconLibrary);
            view.SetClickHandler(null);
            _tileViews[(px, py, layer)] = view;
        }

        void RefreshTileClickabilityVisuals()
        {
            if (_cells == null) return;
            foreach (var kv in _tileViews)
            {
                var (x, y, l) = kv.Key;
                var clickable = TileClickability.IsClickable(_cells, x, y, l);
                kv.Value.SetClickableVisual(clickable);
            }
        }

        void RebuildHandVisual()
        {
            ClearHandViewOnly();
            if (handTileParent == null || tilePrefab == null || _handKind == TileKind.None || grid == null)
                return;

            _handView = Instantiate(tilePrefab, handTileParent, false);
            _handView.gameObject.name = "HandTile";
            var tileRt = (RectTransform)_handView.transform;
            ConfigureTileVisualLikeOrders(_handView, _handKind, tileRt, -1, -1, -1, 1f);
            _handView.SetClickableVisual(true);
            _handView.SetClickHandler(null);
        }

        void ClearHand()
        {
            _handFromRack = false;
            _handOrderColumnIndex = -1;
            _handKind = TileKind.None;
            ClearHandViewOnly();
        }

        void ClearHandViewOnly()
        {
            if (_handView != null)
            {
                Destroy(_handView.gameObject);
                _handView = null;
            }
        }

        static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i).gameObject;
                Destroy(c);
            }
        }
    }
}
