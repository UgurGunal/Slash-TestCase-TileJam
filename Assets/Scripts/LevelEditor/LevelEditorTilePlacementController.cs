using System.Collections.Generic;
using Core;
using LevelData;
using UnityEngine;

namespace Presentation
{
    /// <summary>High-level editor mode: after orders are finalized, the last tile of the last order is held until placed on the board grid.</summary>
    public enum LevelEditorAuthoringPhase
    {
        OrderCreation = 0,
        PlacingTilesFromOrders = 1,
    }

    /// <summary>
    /// After <see cref="LevelEditorOrderAuthoringPanel"/> finalizes orders, the last tile in the last order column is shown in <see cref="handTileParent"/>.
    /// Clicks use the half-cell placement lattice (same as hover): (2W−1)×(2H−1) slots on a W×H major grid.
    /// Empty board uses layer 0. Chebyshev |Δ|≤1 in <b>slot</b> indices picks stacking layer max+1.
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

        LevelEditorAuthoringPhase _phase = LevelEditorAuthoringPhase.OrderCreation;
        TileKind _handKind = TileKind.None;
        BoardTileView _handView;
        TileKind?[, ,] _cells;
        readonly Dictionary<(int x, int y, int z), BoardTileView> _tileViews = new Dictionary<(int x, int y, int z), BoardTileView>();

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

        void Update()
        {
            if (_phase != LevelEditorAuthoringPhase.PlacingTilesFromOrders || _handKind == TileKind.None)
                return;
            if (orderPanel != null && !orderPanel.OrdersAuthoringFinalized)
            {
                ExitPlacement();
                return;
            }

            if (!Input.GetMouseButtonDown(0))
                return;

            if (grid == null || boardTilesRoot == null || tilePrefab == null || _cells == null)
                return;

            var linesRt = grid.LinesContentRect;
            if (linesRt == null)
                return;

            var canvas = canvasOverride != null ? canvasOverride : GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

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

            if (orderPanel == null || !orderPanel.TryConsumeLastTileFromOrdersForPlacement(out _))
            {
                Debug.LogWarning("[LevelEditorTilePlacementController] Could not remove placed tile from order data.");
                ClearHand();
                _phase = LevelEditorAuthoringPhase.OrderCreation;
                return;
            }

            if (orderPanel.TryGetLastTileOfLastOrder(out var nextHand) && nextHand != TileKind.None)
            {
                _handKind = nextHand;
                RebuildHandVisual();
            }
            else
            {
                ClearHand();
                _phase = LevelEditorAuthoringPhase.OrderCreation;
            }
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

            LevelEditorGridLinesView.GetPlacementSlotCounts(grid.GridWidthCells, grid.GridHeightCells, out var pw, out var ph);
            var d = Mathf.Max(1, maxBoardDepth);
            _cells = new TileKind?[pw, ph, d];
            _tileViews.Clear();
            ClearChildren(boardTilesRoot);

            _handKind = kind;
            _phase = LevelEditorAuthoringPhase.PlacingTilesFromOrders;
            RebuildHandVisual();
        }

        void ExitPlacement()
        {
            _phase = LevelEditorAuthoringPhase.OrderCreation;
            _handKind = TileKind.None;
            ClearHandViewOnly();
            _cells = null;
            _tileViews.Clear();
            if (boardTilesRoot != null)
                ClearChildren(boardTilesRoot);
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

            var majorW = grid.GridWidthCells;
            var majorH = grid.GridHeightCells;
            var cw = grid.GridCellWidth;
            var ch = grid.GridCellHeight;
            var gridW = majorW * cw;
            var gridH = majorH * ch;
            var cell = new Vector2(cw, ch);
            _handView = Instantiate(tilePrefab, handTileParent, false);
            _handView.gameObject.name = "HandTile";
            _handView.Bind(_handKind, -1, -1, -1, Vector2.zero, cell, boardTileScaleInCell, iconLibrary);
            _handView.SetClickableVisual(true);
            _handView.SetClickHandler(null);
        }

        void ClearHand()
        {
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
