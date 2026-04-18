using System;
using System.Collections.Generic;
using System.Text;
using Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>How to restore horizontal scroll after order UI is rebuilt (clear + recreate children).</summary>
    public enum OrderScrollAfterRebuild
    {
        /// <summary>Unity default; content rebuild often snaps scroll to the start.</summary>
        None = 0,
        /// <summary>Keep the same horizontal normalized position (stay where you were when width unchanged).</summary>
        PreserveHorizontalNormalized = 1,
        /// <summary>After layout, jump to the right (newest columns). Applied next frame so Content Size Fitter can run first.</summary>
        ScrollToRightEnd = 2,
    }

    /// <summary>
    /// Level editor UI: palette picks tile kinds; orders live in one panel — columns left-to-right, tiles top-to-bottom
    /// per column. After finalize, the <b>two rightmost non-empty</b> columns are “active” (slight green tint). During placement, green tiles are clickable to swap with the hand (<see cref="OnActiveOrderTileClickedForPlacement"/>).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LevelEditorOrderAuthoringPanel : MonoBehaviour
    {
        [Header("Palette (15 kinds)")]
        [SerializeField] BoardTileView tilePrefab;
        [SerializeField] RectTransform paletteRow;
        [SerializeField] TileIconLibrary iconLibrary;
        [Tooltip("Logical cell size passed to Bind (stride between tile centers ≈ this + layout spacing).")]
        [SerializeField] Vector2 paletteCellSize = new Vector2(88f, 100f);
        [Tooltip("Uniform scale on each palette tile root after layout (visual size in slot). Slot size is unchanged.")]
        [SerializeField] [Range(0.05f, 1.5f)] float paletteTileScale = 0.92f;
        [SerializeField] float paletteLayoutSpacing = 6f;
        [Tooltip("Palette wraps into horizontal lines under this parent; each line has at most this many tiles.")]
        [SerializeField] [Range(1, 15)] int paletteMaxTilesPerRow = 8;
        [Tooltip("Vertical gap between palette lines.")]
        [SerializeField] float paletteRowSpacing = 6f;
        [Tooltip("Nudge all palette tiles from the parent’s upper-left: +X = right, +Y = down (pixels).")]
        [SerializeField] Vector2 paletteTilesPosition;

        [Header("Orders (one panel: columns →, tiles ↓)")]
        [Tooltip("ScrollRect content: horizontal row of order columns. Add Content Size Fitter (horizontal Preferred) here for scroll width.")]
        [SerializeField] RectTransform orderColumnsPanel;
        [Tooltip("Optional: parent ScrollRect when many order columns.")]
        [SerializeField] ScrollRect orderAreaScroll;
        [Tooltip("Rebuild clears children, which resets scroll until the next layout pass. Deferred restore avoids snapping to the start.")]
        [SerializeField] OrderScrollAfterRebuild orderScrollAfterRebuild = OrderScrollAfterRebuild.PreserveHorizontalNormalized;
        [SerializeField] Vector2 orderTileCellSize = new Vector2(72f, 90f);
        [Tooltip("Uniform scale on each order tile root.")]
        [SerializeField] [Range(0.05f, 1.5f)] float orderTileScale = 0.92f;
        [Tooltip("Horizontal gap between order columns.")]
        [SerializeField] float orderColumnSpacing = 8f;
        [Tooltip("Vertical gap between tiles inside a column.")]
        [SerializeField] float orderTileVerticalSpacing = 4f;
        [Header("Order columns panel padding")]
        [Tooltip("Applied every rebuild to the Order Columns Panel’s Horizontal Layout Group. Set here only (values are pushed from this script each rebuild).")]
        [SerializeField] int orderAreaHLayoutPadLeft = 10;
        [SerializeField] int orderAreaHLayoutPadRight;
        [SerializeField] int orderAreaHLayoutPadTop = 20;
        [SerializeField] int orderAreaHLayoutPadBottom;
        [Tooltip("Optional: starts the next column (current column must be non-empty).")]
        [SerializeField] Button addOrderButton;
        [Tooltip("Locks order editing: palette and order tiles stop accepting clicks; Add Order / Finalize are disabled.")]
        [SerializeField] Button finalizeOrdersButton;
        [Header("Finalize visuals")]
        [Tooltip("If unset, uses a Graphic on Order Columns Panel. Tint when orders are finalized.")]
        [SerializeField] Graphic orderContentColorTarget;
        [SerializeField] Color orderContentFinalizedTint = new Color(0.9f, 0.92f, 0.98f, 1f);
        [Header("Active orders (after finalize)")]
        [Tooltip("Tiles in the two rightmost non-empty order columns get this tint (active orders). When a column empties, the next columns become active.")]
        [SerializeField] Color activeOrderTileTint = new Color(0.82f, 1f, 0.88f, 1f);

        [Header("Events")]
        [SerializeField] UnityEvent<int> onPaletteTileKindClicked;
        [SerializeField] UnityEvent onDraftChanged;
        [SerializeField] UnityEvent onFinalizedOrdersChanged;
        [Tooltip("Invoked when Finalize Orders is used and editing is locked.")]
        [SerializeField] UnityEvent onOrdersAuthoringFinalized;

        readonly List<List<TileKind>> _orderColumns = new List<List<TileKind>>();

        /// <summary>Deep copy of <see cref="_orderColumns"/> taken when orders are finalized (before placement consumes tiles). Used for JSON export so customer groupings survive an empty queue.</summary>
        List<List<TileKind>> _ordersSnapshotAtFinalize;

        bool _ordersAuthoringFinalized;
        Color _orderContentBaseColor;
        bool _orderContentBaseCaptured;

        UnityEngine.Object _lastPrefab;
        RectTransform _lastPaletteRow;
        RectTransform _lastOrderPanel;
        Vector2 _lastPaletteCell;
        Vector2 _lastOrderCell;
        float _lastPaletteTileScale;
        float _lastOrderTileScale;
        Vector2 _lastPaletteTilesPosition;
        int _lastOrderPadL;
        int _lastOrderPadR;
        int _lastOrderPadT;
        int _lastOrderPadB;
        float _lastPaletteSpacing;
        float _lastOrderColumnSpacing;
        float _lastOrderTileVSpacing;
        int _lastPaletteMaxPerRow;
        float _lastPaletteRowSpacing;
        UnityEngine.Object _lastOrderScroll;
        OrderScrollAfterRebuild _lastOrderScrollAfterRebuild;
        bool _lastOrdersAuthoringFinalized;
        bool _rebuildQueued = true;

        OrderScrollAfterRebuild _pendingScrollMode;
        float _pendingScrollNormalized;
        bool _pendingScrollApply;

        readonly HashSet<int> _activeOrderColumnIndicesScratch = new HashSet<int>();

        /// <summary>Rightmost column — the one palette clicks append to.</summary>
        public IReadOnlyList<TileKind> ActiveColumn =>
            _orderColumns.Count > 0 ? _orderColumns[_orderColumns.Count - 1] : System.Array.Empty<TileKind>();

        /// <summary>Same as active column (legacy name).</summary>
        public IReadOnlyList<TileKind> DraftOrder => ActiveColumn;

        /// <summary>All columns left-to-right; last entry is the active column.</summary>
        public IReadOnlyList<List<TileKind>> OrderColumns => _orderColumns;

        /// <summary>Completed columns only (excludes trailing active column).</summary>
        public IReadOnlyList<IReadOnlyList<TileKind>> FinalizedOrders => new FinalizedColumnsView(_orderColumns);

        /// <summary>True after <see cref="FinalizeOrders"/> until <see cref="ClearFinalizedOrders"/> or <see cref="UnlockOrderAuthoring"/>.</summary>
        public bool OrdersAuthoringFinalized => _ordersAuthoringFinalized;

        /// <summary>Logical cell size for order column tiles (argument to <see cref="BoardTileView.Bind"/> with scale 1).</summary>
        public Vector2 OrderTileCellSize => orderTileCellSize;

        /// <summary>Uniform <see cref="RectTransform.localScale"/> applied to order tiles after <see cref="BoardTileView.Bind"/>.</summary>
        public float OrderTileUniformScale => orderTileScale;

        /// <summary>Invoked with <see cref="onOrdersAuthoringFinalized"/> when orders are locked.</summary>
        public event Action OnOrdersAuthoringFinalized;

        /// <summary>Invoked when authoring is unlocked or orders are cleared.</summary>
        public event Action OnOrdersAuthoringUnlocked;

        /// <summary>After finalize, during tile placement: user clicked a tile in an active (green) order column. Args: column index, tile index in that column.</summary>
        public event Action<int, int> OnActiveOrderTileClickedForPlacement;

        void OnEnable()
        {
            EnsureAtLeastOneColumn();
            _rebuildQueued = true;
            if (addOrderButton != null)
                addOrderButton.onClick.AddListener(AddOrderFromDraft);
            if (finalizeOrdersButton != null)
                finalizeOrdersButton.onClick.AddListener(FinalizeOrders);
        }

        void OnDisable()
        {
            if (addOrderButton != null)
                addOrderButton.onClick.RemoveListener(AddOrderFromDraft);
            if (finalizeOrdersButton != null)
                finalizeOrdersButton.onClick.RemoveListener(FinalizeOrders);
        }

        void OnValidate()
        {
            paletteCellSize = new Vector2(Mathf.Max(1f, paletteCellSize.x), Mathf.Max(1f, paletteCellSize.y));
            orderTileCellSize = new Vector2(Mathf.Max(1f, orderTileCellSize.x), Mathf.Max(1f, orderTileCellSize.y));
            paletteLayoutSpacing = Mathf.Max(0f, paletteLayoutSpacing);
            orderColumnSpacing = Mathf.Max(0f, orderColumnSpacing);
            orderTileVerticalSpacing = Mathf.Max(0f, orderTileVerticalSpacing);
            orderAreaHLayoutPadLeft = Mathf.Max(0, orderAreaHLayoutPadLeft);
            orderAreaHLayoutPadRight = Mathf.Max(0, orderAreaHLayoutPadRight);
            orderAreaHLayoutPadTop = Mathf.Max(0, orderAreaHLayoutPadTop);
            orderAreaHLayoutPadBottom = Mathf.Max(0, orderAreaHLayoutPadBottom);
            paletteRowSpacing = Mathf.Max(0f, paletteRowSpacing);
            paletteMaxTilesPerRow = Mathf.Clamp(paletteMaxTilesPerRow, 1, GameConstants.PlayableTileKindCount);
            _rebuildQueued = true;
        }

        void Update()
        {
            if (_rebuildQueued)
            {
                _rebuildQueued = false;
                RebuildAll(force: true);
                return;
            }

            RebuildAll(force: false);
        }

        void RebuildAll(bool force)
        {
            if (tilePrefab == null || paletteRow == null || orderColumnsPanel == null)
                return;

            EnsureAtLeastOneColumn();

            if (!force &&
                _lastPrefab == tilePrefab &&
                _lastPaletteRow == paletteRow &&
                _lastOrderPanel == orderColumnsPanel &&
                _lastPaletteCell == paletteCellSize &&
                _lastOrderCell == orderTileCellSize &&
                Mathf.Approximately(_lastPaletteTileScale, paletteTileScale) &&
                Mathf.Approximately(_lastOrderTileScale, orderTileScale) &&
                _lastPaletteTilesPosition == paletteTilesPosition &&
                _lastOrderPadL == orderAreaHLayoutPadLeft &&
                _lastOrderPadR == orderAreaHLayoutPadRight &&
                _lastOrderPadT == orderAreaHLayoutPadTop &&
                _lastOrderPadB == orderAreaHLayoutPadBottom &&
                Mathf.Approximately(_lastPaletteSpacing, paletteLayoutSpacing) &&
                Mathf.Approximately(_lastOrderColumnSpacing, orderColumnSpacing) &&
                Mathf.Approximately(_lastOrderTileVSpacing, orderTileVerticalSpacing) &&
                _lastPaletteMaxPerRow == paletteMaxTilesPerRow &&
                Mathf.Approximately(_lastPaletteRowSpacing, paletteRowSpacing) &&
                _lastOrderScroll == orderAreaScroll &&
                _lastOrderScrollAfterRebuild == orderScrollAfterRebuild &&
                _lastOrdersAuthoringFinalized == _ordersAuthoringFinalized)
                return;

            ConfigureOrderScrollForPanel();
            EnsureVerticalStackLayout(paletteRow, paletteRowSpacing, paletteTilesPosition);

            ClearChildren(paletteRow);
            var paletteEffCell = paletteCellSize;
            RectTransform paletteLineRt = null;
            for (var i = 0; i < GameConstants.PlayableTileKindCount; i++)
            {
                if (i % paletteMaxTilesPerRow == 0)
                    paletteLineRt = CreateHorizontalLayoutRow(
                        paletteRow,
                        $"PaletteLine_{i / paletteMaxTilesPerRow}",
                        paletteLayoutSpacing,
                        paletteEffCell.y);

                var kind = (TileKind)i;
                var slot = CreateFixedLayoutSlot(paletteLineRt, $"PaletteSlot_{kind}", paletteEffCell);
                var tile = Instantiate(tilePrefab, slot, false);
                tile.gameObject.name = $"Palette_{kind}";
                var tileRt = (RectTransform)tile.transform;
                PrepareTileRectInSlot(tileRt);
                StripLayoutElementFrom(tile.gameObject);
                tile.Bind(kind, i, 0, 0, Vector2.zero, paletteEffCell, 1f, iconLibrary);
                ApplyUniformVisualScale(tileRt, paletteTileScale);
                var paletteInteractive = !_ordersAuthoringFinalized;
                tile.SetClickableVisual(paletteInteractive);
                tile.SetClickHandler(paletteInteractive ? OnPaletteTileClicked : null);
            }

            RebuildOrderColumnsVisuals();
            ApplyOrderContentVisual();
            RefreshOrderActionButtons();

            _lastPrefab = tilePrefab;
            _lastPaletteRow = paletteRow;
            _lastOrderPanel = orderColumnsPanel;
            _lastPaletteCell = paletteCellSize;
            _lastOrderCell = orderTileCellSize;
            _lastPaletteTileScale = paletteTileScale;
            _lastOrderTileScale = orderTileScale;
            _lastPaletteTilesPosition = paletteTilesPosition;
            _lastOrderPadL = orderAreaHLayoutPadLeft;
            _lastOrderPadR = orderAreaHLayoutPadRight;
            _lastOrderPadT = orderAreaHLayoutPadTop;
            _lastOrderPadB = orderAreaHLayoutPadBottom;
            _lastPaletteSpacing = paletteLayoutSpacing;
            _lastOrderColumnSpacing = orderColumnSpacing;
            _lastOrderTileVSpacing = orderTileVerticalSpacing;
            _lastPaletteMaxPerRow = paletteMaxTilesPerRow;
            _lastPaletteRowSpacing = paletteRowSpacing;
            _lastOrderScroll = orderAreaScroll;
            _lastOrderScrollAfterRebuild = orderScrollAfterRebuild;
            _lastOrdersAuthoringFinalized = _ordersAuthoringFinalized;
        }

        void LateUpdate()
        {
            if (!_pendingScrollApply)
                return;
            _pendingScrollApply = false;
            if (orderAreaScroll == null || orderAreaScroll.content != orderColumnsPanel)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(orderColumnsPanel);
            Canvas.ForceUpdateCanvases();

            if (_pendingScrollMode == OrderScrollAfterRebuild.PreserveHorizontalNormalized)
                orderAreaScroll.horizontalNormalizedPosition = Mathf.Clamp01(_pendingScrollNormalized);
            else if (_pendingScrollMode == OrderScrollAfterRebuild.ScrollToRightEnd)
                orderAreaScroll.horizontalNormalizedPosition = 1f;
        }

        void EnsureAtLeastOneColumn()
        {
            if (_orderColumns.Count == 0)
                _orderColumns.Add(new List<TileKind>());
        }

        void OnPaletteTileClicked(BoardTileView view)
        {
            if (_ordersAuthoringFinalized) return;
            var kind = view.Kind;
            if (kind == TileKind.None) return;
            EnsureAtLeastOneColumn();
            _orderColumns[_orderColumns.Count - 1].Add(kind);
            onPaletteTileKindClicked?.Invoke((int)kind);
            RebuildOrderColumnsVisuals();
            onDraftChanged?.Invoke();
        }

        /// <summary>Clears tiles in the active (rightmost) column only.</summary>
        public void ClearDraft()
        {
            if (_ordersAuthoringFinalized) return;
            EnsureAtLeastOneColumn();
            _orderColumns[_orderColumns.Count - 1].Clear();
            RebuildOrderColumnsVisuals();
            onDraftChanged?.Invoke();
        }

        public void RemoveLastFromDraft()
        {
            if (_ordersAuthoringFinalized) return;
            EnsureAtLeastOneColumn();
            var col = _orderColumns[_orderColumns.Count - 1];
            if (col.Count == 0) return;
            col.RemoveAt(col.Count - 1);
            RebuildOrderColumnsVisuals();
            onDraftChanged?.Invoke();
        }

        /// <summary>If the active column has at least one tile, appends a new empty column to the right.</summary>
        public void AddOrderFromDraft()
        {
            if (_ordersAuthoringFinalized) return;
            EnsureAtLeastOneColumn();
            var active = _orderColumns[_orderColumns.Count - 1];
            if (active.Count == 0) return;
            _orderColumns.Add(new List<TileKind>());
            RebuildOrderColumnsVisuals();
            onDraftChanged?.Invoke();
            onFinalizedOrdersChanged?.Invoke();
        }

        /// <summary>Removes all columns and starts one empty active column.</summary>
        public void ClearFinalizedOrders()
        {
            _ordersAuthoringFinalized = false;
            _ordersSnapshotAtFinalize = null;
            _orderColumns.Clear();
            _orderColumns.Add(new List<TileKind>());
            if (orderAreaScroll != null)
                orderAreaScroll.horizontalNormalizedPosition = 0f;
            _rebuildQueued = true;
            onFinalizedOrdersChanged?.Invoke();
            onDraftChanged?.Invoke();
            OnOrdersAuthoringUnlocked?.Invoke();
        }

        /// <summary>Locks editing after the user finalizes (same as the Finalize Orders button).</summary>
        public void FinalizeOrders()
        {
            if (_ordersAuthoringFinalized) return;
            _ordersAuthoringFinalized = true;
            CaptureOrdersSnapshotAtFinalize();
            _rebuildQueued = true;
            onOrdersAuthoringFinalized?.Invoke();
            OnOrdersAuthoringFinalized?.Invoke();
        }

        /// <summary>Re-enables palette, order tile edits, and action buttons without clearing order data.</summary>
        public void UnlockOrderAuthoring()
        {
            if (!_ordersAuthoringFinalized) return;
            _ordersAuthoringFinalized = false;
            _ordersSnapshotAtFinalize = null;
            _rebuildQueued = true;
            OnOrdersAuthoringUnlocked?.Invoke();
        }

        void CaptureOrdersSnapshotAtFinalize()
        {
            _ordersSnapshotAtFinalize = new List<List<TileKind>>();
            for (var i = 0; i < _orderColumns.Count; i++)
            {
                var col = _orderColumns[i];
                _ordersSnapshotAtFinalize.Add(col != null ? new List<TileKind>(col) : new List<TileKind>());
            }
        }

        /// <summary>True when every order column is empty (all tiles placed on the board / rack pipeline cleared for export).</summary>
        public bool AreAllOrderColumnsEmptyForExport()
        {
            EnsureAtLeastOneColumn();
            for (var i = 0; i < _orderColumns.Count; i++)
            {
                var col = _orderColumns[i];
                if (col != null && col.Count > 0)
                    return false;
            }

            return true;
        }

        /// <summary>Builds the level JSON <c>orders</c> array from the finalize snapshot (non-empty columns only).</summary>
        public bool TryGetSnapshotOrdersForExport(out List<List<int>> orders, out string error)
        {
            orders = null;
            error = null;
            if (_ordersSnapshotAtFinalize == null)
            {
                error = "No finalized order snapshot. Finalize orders again.";
                return false;
            }

            orders = new List<List<int>>();
            for (var c = 0; c < _ordersSnapshotAtFinalize.Count; c++)
            {
                var col = _ordersSnapshotAtFinalize[c];
                if (col == null || col.Count == 0)
                    continue;
                var row = new List<int>(col.Count);
                for (var i = 0; i < col.Count; i++)
                    row.Add((int)col[i]);
                orders.Add(row);
            }

            if (orders.Count == 0)
            {
                error = "Finalized orders snapshot is empty.";
                return false;
            }

            return true;
        }

        /// <summary>Last icon in the rightmost non-empty order column (the tile meant for board placement after finalize).</summary>
        public bool TryGetLastTileOfLastOrder(out TileKind kind)
        {
            kind = TileKind.None;
            for (var c = _orderColumns.Count - 1; c >= 0; c--)
            {
                var col = _orderColumns[c];
                if (col == null || col.Count == 0) continue;
                kind = col[col.Count - 1];
                return kind != TileKind.None;
            }

            return false;
        }

        /// <summary>
        /// Removes the last tile from the rightmost non-empty order column — when it becomes the hand tile (placement phase) or when advancing the hand after a place/rack action.
        /// <paramref name="sourceOrderColumnIndex"/> is the column index (0 = leftmost order) <i>before</i> removal, so returned tiles can be put back on the same customer order.
        /// </summary>
        public bool TryConsumeLastTileFromOrdersForPlacement(out TileKind removed, out int sourceOrderColumnIndex)
        {
            removed = TileKind.None;
            sourceOrderColumnIndex = -1;
            for (var c = _orderColumns.Count - 1; c >= 0; c--)
            {
                var col = _orderColumns[c];
                if (col == null || col.Count == 0) continue;
                sourceOrderColumnIndex = c;
                removed = col[col.Count - 1];
                col.RemoveAt(col.Count - 1);
                while (_orderColumns.Count > 1 && _orderColumns[_orderColumns.Count - 1].Count == 0)
                    _orderColumns.RemoveAt(_orderColumns.Count - 1);
                EnsureAtLeastOneColumn();
                RebuildOrderColumnsVisuals();
                onDraftChanged?.Invoke();
                return removed != TileKind.None;
            }

            return false;
        }

        /// <summary>
        /// Appends a tile to the end of the order column at <paramref name="orderColumnIndex"/> (left-to-right customer index). Inserts empty columns if needed so the index is valid.
        /// </summary>
        public void AppendTileToOrderColumnIndex(int orderColumnIndex, TileKind kind)
        {
            if (kind == TileKind.None || orderColumnIndex < 0) return;
            EnsureAtLeastOneColumn();
            while (_orderColumns.Count <= orderColumnIndex)
                _orderColumns.Add(new List<TileKind>());
            _orderColumns[orderColumnIndex].Add(kind);
            RebuildOrderColumnsVisuals();
            onDraftChanged?.Invoke();
        }

        /// <summary>
        /// Removes the tile at (<paramref name="columnIndex"/>, <paramref name="tileIndex"/>) from the draft and returns it as <paramref name="takenFromCell"/>.
        /// Only allowed for <b>active</b> (green) order columns. The caller returns the previous hand tile to its own source column or rack slot.
        /// </summary>
        public bool TryTakeActiveOrderTileToHand(int columnIndex, int tileIndex, out TileKind takenFromCell)
        {
            takenFromCell = TileKind.None;
            if (!_ordersAuthoringFinalized) return false;
            RecomputeActiveOrderColumnIndicesForHighlight();
            if (!_activeOrderColumnIndicesScratch.Contains(columnIndex)) return false;
            if ((uint)columnIndex >= (uint)_orderColumns.Count) return false;
            var col = _orderColumns[columnIndex];
            if (col == null || (uint)tileIndex >= (uint)col.Count) return false;
            takenFromCell = col[tileIndex];
            col.RemoveAt(tileIndex);
            while (_orderColumns.Count > 1 && _orderColumns[_orderColumns.Count - 1].Count == 0)
                _orderColumns.RemoveAt(_orderColumns.Count - 1);
            EnsureAtLeastOneColumn();
            RebuildOrderColumnsVisuals();
            onDraftChanged?.Invoke();
            return takenFromCell != TileKind.None;
        }

        public string GetDraftAsOrderTextEntry()
        {
            EnsureAtLeastOneColumn();
            var col = _orderColumns[_orderColumns.Count - 1];
            if (col.Count == 0) return "{}";
            var sb = new StringBuilder();
            sb.Append('{');
            for (var i = 0; i < col.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append((int)col[i]);
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Non-empty columns in Tile Level Editor form, e.g. <c>{{1,2},{3,3,3}}</c>.</summary>
        public string GetFinalizedOrdersAsEditorText()
        {
            var sb = new StringBuilder();
            var any = false;
            sb.Append('{');
            for (var c = 0; c < _orderColumns.Count; c++)
            {
                var col = _orderColumns[c];
                if (col.Count == 0) continue;
                if (any) sb.Append(',');
                any = true;
                sb.Append('{');
                for (var i = 0; i < col.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append((int)col[i]);
                }

                sb.Append('}');
            }

            sb.Append('}');
            return any ? sb.ToString() : string.Empty;
        }

        /// <summary>
        /// Active orders = the two columns with the <b>largest indices</b> that still have at least one tile (the rightmost non-empty columns).
        /// When the rightmost column empties, the next columns become active automatically on rebuild.
        /// </summary>
        void RecomputeActiveOrderColumnIndicesForHighlight()
        {
            _activeOrderColumnIndicesScratch.Clear();
            var nonempty = new List<int>();
            for (var i = 0; i < _orderColumns.Count; i++)
            {
                if (_orderColumns[i] != null && _orderColumns[i].Count > 0)
                    nonempty.Add(i);
            }

            if (nonempty.Count == 0) return;
            nonempty.Sort();
            var n = nonempty.Count;
            _activeOrderColumnIndicesScratch.Add(nonempty[n - 1]);
            if (n >= 2)
                _activeOrderColumnIndicesScratch.Add(nonempty[n - 2]);
        }

        void RebuildOrderColumnsVisuals()
        {
            if (orderColumnsPanel == null || tilePrefab == null) return;
            EnsureAtLeastOneColumn();

            var hadScroll = orderAreaScroll != null && orderAreaScroll.content == orderColumnsPanel;
            var savedNorm = hadScroll ? orderAreaScroll.horizontalNormalizedPosition : 0f;

            ClearChildren(orderColumnsPanel);
            EnsureHorizontalOrdersLayout(
                orderColumnsPanel,
                orderColumnSpacing,
                orderAreaHLayoutPadLeft,
                orderAreaHLayoutPadRight,
                orderAreaHLayoutPadTop,
                orderAreaHLayoutPadBottom);

            RecomputeActiveOrderColumnIndicesForHighlight();

            for (var ci = 0; ci < _orderColumns.Count; ci++)
            {
                var kinds = _orderColumns[ci];
                var colRt = CreateOrderColumn(
                    orderColumnsPanel,
                    $"OrderColumn_{ci}",
                    orderTileVerticalSpacing,
                    orderTileCellSize.x,
                    kinds.Count,
                    orderTileCellSize);

                var isActiveColumn = ci == _orderColumns.Count - 1;
                for (var ti = 0; ti < kinds.Count; ti++)
                {
                    var kind = kinds[ti];
                    var slot = CreateFixedLayoutSlot(colRt, $"O{ci}_T{ti}_{kind}", orderTileCellSize);
                    var tile = Instantiate(tilePrefab, slot, false);
                    tile.gameObject.name = $"Order_{ci}_{ti}_{kind}";
                    var tileRt = (RectTransform)tile.transform;
                    PrepareTileRectInSlot(tileRt);
                    StripLayoutElementFrom(tile.gameObject);
                    tile.Bind(kind, ti, ci, 0, Vector2.zero, orderTileCellSize, 1f, iconLibrary);
                    ApplyUniformVisualScale(tileRt, orderTileScale);
                    if (_ordersAuthoringFinalized && _activeOrderColumnIndicesScratch.Contains(ci))
                        tile.SetActiveOrderHighlight(true, activeOrderTileTint);
                    else
                        tile.SetActiveOrderHighlight(false, activeOrderTileTint);
                    var orderEditable = !_ordersAuthoringFinalized;
                    tile.SetClickableVisual(true);
                    if (orderEditable && isActiveColumn)
                    {
                        var c = ci;
                        var t = ti;
                        tile.SetClickHandler(_ => RemoveTileAt(c, t));
                    }
                    else if (_ordersAuthoringFinalized && _activeOrderColumnIndicesScratch.Contains(ci))
                    {
                        var c = ci;
                        var t = ti;
                        tile.SetClickHandler(_ => OnActiveOrderTileClickedForPlacement?.Invoke(c, t));
                    }
                    else
                        tile.SetClickHandler(null);
                }
            }

            ScheduleOrderScrollRestore(hadScroll, savedNorm);
        }

        void ApplyOrderContentVisual()
        {
            var g = orderContentColorTarget != null
                ? orderContentColorTarget
                : orderColumnsPanel != null
                    ? orderColumnsPanel.GetComponent<Graphic>()
                    : null;
            if (g == null) return;
            if (!_orderContentBaseCaptured)
            {
                _orderContentBaseColor = g.color;
                _orderContentBaseCaptured = true;
            }

            g.color = _ordersAuthoringFinalized ? orderContentFinalizedTint : _orderContentBaseColor;
        }

        void RefreshOrderActionButtons()
        {
            var allow = !_ordersAuthoringFinalized;
            if (addOrderButton != null)
                addOrderButton.interactable = allow;
            if (finalizeOrdersButton != null)
                finalizeOrdersButton.interactable = allow;
        }

        void ScheduleOrderScrollRestore(bool hadScroll, float savedNormalized)
        {
            if (!hadScroll || orderScrollAfterRebuild == OrderScrollAfterRebuild.None)
                return;
            _pendingScrollMode = orderScrollAfterRebuild;
            _pendingScrollNormalized = savedNormalized;
            _pendingScrollApply = true;
        }

        void ConfigureOrderScrollForPanel()
        {
            if (orderColumnsPanel == null || orderAreaScroll == null)
                return;

            if (orderAreaScroll.content != orderColumnsPanel)
            {
                Debug.LogWarning(
                    $"[{nameof(LevelEditorOrderAuthoringPanel)}] Assign Scroll Rect → Content to the same object as Order Columns Panel so horizontal scroll works.",
                    this);
                return;
            }

            orderAreaScroll.horizontal = true;
            orderAreaScroll.vertical = false;
            orderAreaScroll.movementType = ScrollRect.MovementType.Clamped;
        }

        void RemoveTileAt(int columnIndex, int tileIndex)
        {
            if (_ordersAuthoringFinalized) return;
            if ((uint)columnIndex >= (uint)_orderColumns.Count) return;
            if (columnIndex != _orderColumns.Count - 1) return;
            var col = _orderColumns[columnIndex];
            if ((uint)tileIndex >= (uint)col.Count) return;
            col.RemoveAt(tileIndex);
            RebuildOrderColumnsVisuals();
            onDraftChanged?.Invoke();
        }

        static RectTransform CreateOrderColumn(
            RectTransform horizontalParent,
            string name,
            float verticalSpacing,
            float columnWidth,
            int tileCount,
            Vector2 cellSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(horizontalParent, false);
            rt.localScale = Vector3.one;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = columnWidth;
            le.minWidth = columnWidth;
            var colH = tileCount <= 0
                ? cellSize.y
                : tileCount * cellSize.y + (tileCount - 1) * verticalSpacing;
            colH = Mathf.Max(cellSize.y, colH);
            le.preferredHeight = colH;
            le.minHeight = colH;
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = verticalSpacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            return rt;
        }

        static void EnsureHorizontalOrdersLayout(
            RectTransform row,
            float columnSpacing,
            int padLeft,
            int padRight,
            int padTop,
            int padBottom)
        {
            var oldV = row.GetComponent<VerticalLayoutGroup>();
            if (oldV != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(oldV);
                else
                    UnityEngine.Object.DestroyImmediate(oldV);
            }

            var h = row.GetComponent<HorizontalLayoutGroup>();
            var addedNew = false;
            if (h == null)
            {
                h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                addedNew = true;
            }

            h.spacing = columnSpacing;
            h.childAlignment = TextAnchor.UpperLeft;
            h.padding.left = padLeft;
            h.padding.right = padRight;
            h.padding.top = padTop;
            h.padding.bottom = padBottom;

            // Only set layout driver flags when we create the group so inspector tweaks (e.g. child force expand) stay intact.
            if (addedNew)
            {
                h.childControlWidth = false;
                h.childControlHeight = false;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
            }
        }

        /// <summary>Read-only view of columns <c>[0 .. Count-2]</c> when <c>Count ≥ 2</c>, else empty.</summary>
        sealed class FinalizedColumnsView : IReadOnlyList<IReadOnlyList<TileKind>>
        {
            readonly List<List<TileKind>> _inner;

            public FinalizedColumnsView(List<List<TileKind>> inner) => _inner = inner;

            public int Count
            {
                get
                {
                    if (_inner.Count < 2) return 0;
                    return _inner.Count - 1;
                }
            }

            public IReadOnlyList<TileKind> this[int index] => _inner[index];

            public IEnumerator<IReadOnlyList<TileKind>> GetEnumerator()
            {
                var n = Count;
                for (var i = 0; i < n; i++)
                    yield return _inner[i];
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        static RectTransform CreateFixedLayoutSlot(RectTransform row, string name, Vector2 slotSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row, false);
            rt.localScale = Vector3.one;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = slotSize.x;
            le.preferredHeight = slotSize.y;
            le.minWidth = slotSize.x;
            le.minHeight = slotSize.y;
            return rt;
        }

        static void PrepareTileRectInSlot(RectTransform tileRt)
        {
            tileRt.anchorMin = tileRt.anchorMax = tileRt.pivot = new Vector2(0.5f, 0.5f);
            tileRt.anchoredPosition = Vector2.zero;
            tileRt.localScale = Vector3.one;
        }

        static void ApplyUniformVisualScale(RectTransform tileRt, float uniformScale)
        {
            var s = Mathf.Max(0.01f, uniformScale);
            tileRt.localScale = new Vector3(s, s, 1f);
        }

        static void StripLayoutElementFrom(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(le);
            else
                UnityEngine.Object.DestroyImmediate(le);
        }

        static void EnsureVerticalStackLayout(RectTransform column, float rowSpacing, Vector2 contentOffsetPixels)
        {
            var oldH = column.GetComponent<HorizontalLayoutGroup>();
            if (oldH != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(oldH);
                else
                    UnityEngine.Object.DestroyImmediate(oldH);
            }

            var v = column.GetComponent<VerticalLayoutGroup>();
            if (v == null)
                v = column.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = rowSpacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            v.padding.left = Mathf.RoundToInt(contentOffsetPixels.x);
            v.padding.top = Mathf.RoundToInt(contentOffsetPixels.y);
            v.padding.right = 0;
            v.padding.bottom = 0;
        }

        static RectTransform CreateHorizontalLayoutRow(RectTransform column, string name, float spacing, float rowHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(column, false);
            rt.localScale = Vector3.one;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = rowHeight;
            le.minHeight = rowHeight;
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return rt;
        }

        static void ClearChildren(RectTransform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}
