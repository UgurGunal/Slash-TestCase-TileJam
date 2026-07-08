#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Core;
using LevelData;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>Order-driven level editor with image tiles, placement lattice, hover, and x1/x3 palette.</summary>
    public sealed class TileLevelEditorWindow : EditorWindow
    {
        enum TileAddAmount
        {
            One = 1,
            Three = 3,
        }

        enum EditorPhase
        {
            OrderAuthoring = 0,
            PlacingTiles = 1,
        }

        // Sub-modes available once orders are finalized (PlacingTiles phase).
        enum PlaceMode
        {
            // Editor mode: place tiles from orders / rack onto the board (like creating a new level).
            Place = 0,
            // Game-like removal: click the top tile to pull it off — matching tiles go back to orders, others to the rack.
            Remove = 1,
        }

        const float TileButtonSize = 52f;
        const float RackCellSize = 40f;

        // The grid is drawn at fine (half-cell) resolution. A tile spans a 2x2 block of fine cells and is
        // centered on a grid intersection, so its borders snap exactly onto the grid lines. Placement picks
        // the intersection nearest the pointer and fills the 4 cells around it.
        const float FineCellSize = 26f;
        const float TileDrawSize = 2f * FineCellSize;
        const float BoardMargin = 10f;

        readonly LevelEditorOrderModel _orders = new LevelEditorOrderModel();
        readonly LevelEditorBoardModel _board = new LevelEditorBoardModel();

        Vector2 _orderScroll;
        Vector2 _boardScroll;
        TileAddAmount _addAmount = TileAddAmount.One;
        EditorPhase _phase = EditorPhase.OrderAuthoring;
        PlaceMode _placeMode = PlaceMode.Place;
        TileKind _handKind = TileKind.None;
        bool _handFromRack;
        int _handOrderColumnIndex = -1;
        int _handOrderSnapIcon = -1;
        int _handRackSlotIndex = -1;
        string _statusMessage = "";
        string _validationMessage = "";
        int _hoverPx = -1;
        int _hoverPy = -1;
        bool _hoverValid;
        readonly List<(int layer, int px, int py, TileKind kind)> _drawOrderScratch =
            new List<(int layer, int px, int py, TileKind kind)>();
        readonly List<(TileKind kind, int orderCol, int orderIcon)> _pendingOrderTilesScratch =
            new List<(TileKind kind, int orderCol, int orderIcon)>();
        int _maxVisibleLayer;
        GUIStyle _layerBadgeStyle;

        GUIStyle LayerBadgeStyle
        {
            get
            {
                if (_layerBadgeStyle == null)
                {
                    _layerBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                    };
                    _layerBadgeStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                }

                return _layerBadgeStyle;
            }
        }

        bool CanUseBoardTools() => _orders.IsFinalized || _orders.HasOrderSnapshot;

        bool IsEditOrdersMode => !_orders.IsFinalized && CanUseBoardTools();

        bool CanInteractWithBoard() => CanUseBoardTools() && !IsEditOrdersMode;

        void MaintainOrderColumnIndices()
        {
            var remap = _orders.CompactMutableOrderColumns();
            if (remap.Count > 0)
                _board.RemapOrderColumnIndices(remap, _orders.SnapshotOrderCount);
        }

        string[] _resourceLevelPaths = System.Array.Empty<string>();
        string[] _resourceLevelLabels = System.Array.Empty<string>();
        int _selectedResourceLevel = -1;

        [MenuItem("Window/Tile Level Editor")]
        static void Open() => GetWindow<TileLevelEditorWindow>("Tile Level Editor");

        void OnEnable()
        {
            _board.EnsureGrid();
            RefreshResourceLevels();
        }

        void RefreshResourceLevels()
        {
            var guids = AssetDatabase.FindAssets("", new[] { "Assets/Resources/Levels" });
            var paths = new List<string>();
            var labels = new List<string>();
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)) continue;
                paths.Add(path);
                labels.Add(Path.GetFileNameWithoutExtension(path));
            }

            paths.Sort();
            labels.Sort();
            _resourceLevelPaths = paths.ToArray();
            _resourceLevelLabels = labels.ToArray();
            _selectedResourceLevel = Mathf.Clamp(_selectedResourceLevel, -1, _resourceLevelPaths.Length - 1);
        }

        void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);
            DrawPaletteSection();
            EditorGUILayout.Space(6f);
            DrawOrdersSection();
            EditorGUILayout.Space(6f);
            DrawBoardSection();
            EditorGUILayout.Space(4f);
            DrawFooter();
        }

        void DrawToolbar()
        {
            EditorGUILayout.LabelField("Board size (major cells)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            var mw = EditorGUILayout.IntField("Width", _board.MajorWidth);
            var mh = EditorGUILayout.IntField("Height", _board.MajorHeight);
            var md = EditorGUILayout.IntField("Depth", _board.Depth);
            if (mw != _board.MajorWidth || mh != _board.MajorHeight || md != _board.Depth)
            {
                _board.MajorWidth = mw;
                _board.MajorHeight = mh;
                _board.Depth = md;
                _board.EnsureGrid();
            }

            if (GUILayout.Button("Apply size", GUILayout.Width(90f)))
                _board.EnsureGrid();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Add amount:", GUILayout.Width(80f));
            var addIndex = _addAmount == TileAddAmount.One ? 0 : 1;
            var nextAddIndex = GUILayout.Toolbar(addIndex, new[] { "x1", "x3" }, GUILayout.Width(90f));
            if (nextAddIndex != addIndex)
                _addAmount = nextAddIndex == 0 ? TileAddAmount.One : TileAddAmount.Three;

            GUILayout.FlexibleSpace();

            RefreshResourceLevels();
            if (_resourceLevelLabels.Length > 0)
            {
                var next = EditorGUILayout.Popup("Load level", Mathf.Max(0, _selectedResourceLevel), _resourceLevelLabels, GUILayout.MinWidth(160f));
                if (next != _selectedResourceLevel)
                {
                    _selectedResourceLevel = next;
                    LoadLevelFromAssetPath(_resourceLevelPaths[_selectedResourceLevel]);
                }
            }

            if (GUILayout.Button("Import…", GUILayout.Width(72f)))
                ImportJson();
            if (GUILayout.Button("Export…", GUILayout.Width(72f)))
                ExportJson();
            if (GUILayout.Button("New", GUILayout.Width(52f)))
                NewLevel();
            EditorGUILayout.EndHorizontal();
        }

        void DrawPaletteSection()
        {
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            if (_orders.IsFinalized)
            {
                EditorGUILayout.HelpBox("Click Edit orders to change the palette and order columns.", MessageType.None);
                return;
            }

            if (!_orders.CanAcceptPaletteTiles())
            {
                EditorGUILayout.HelpBox("Click Add order first, then use the palette to fill the new order column.", MessageType.Info);
                return;
            }

            var kindCount = EditorTileIcons.ResolvedKindCount;
            var perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / (TileButtonSize + 4f)));
            for (var i = 0; i < kindCount; i++)
            {
                if (i % perRow == 0)
                    EditorGUILayout.BeginHorizontal();
                var kind = (TileKind)i;
                var rect = GUILayoutUtility.GetRect(TileButtonSize, TileButtonSize, GUILayout.Width(TileButtonSize), GUILayout.Height(TileButtonSize));
                if (EditorTileIcons.TileButton(rect, kind))
                {
                    _orders.AddFromPalette(kind, (int)_addAmount);
                    MaintainOrderColumnIndices();
                }
                if (i % perRow == perRow - 1 || i == kindCount - 1)
                    EditorGUILayout.EndHorizontal();
            }
        }

        void DrawOrdersSection()
        {
            EditorGUILayout.LabelField("Orders", EditorStyles.boldLabel);

            var isEditMode = !_orders.IsFinalized;

            if (!isEditMode && _orders.HasOrderSnapshot && _orders.AreAllColumnsEmpty())
            {
                var nextOrder = _orders.NextAppendOrderNumber;
                EditorGUILayout.HelpBox(
                    $"All tiles are on the board. Click Edit orders, then Add order or use the palette to append order #{nextOrder}.",
                    MessageType.Info);
            }
            else if (isEditMode && _orders.HasOrderSnapshot && _orders.AreAllColumnsEmpty() &&
                     !_orders.CanAcceptPaletteTiles())
            {
                var nextOrder = _orders.NextAppendOrderNumber;
                EditorGUILayout.HelpBox(
                    $"Click Add order to open order #{nextOrder}, then use the palette. Existing orders stay on the board until you remove tiles.",
                    MessageType.Info);
            }
            else
            {
                DrawOrderColumns(isEditMode);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(_orders.IsFinalized);
            if (GUILayout.Button("Add order"))
                _orders.AddOrderColumn();
            if (GUILayout.Button("Clear active column"))
            {
                _orders.ClearActiveColumn();
                MaintainOrderColumnIndices();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(_orders.IsFinalized);
            if (GUILayout.Button("Finalize orders", GUILayout.Width(120f)))
                FinalizeOrders();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!_orders.IsFinalized);
            if (GUILayout.Button("Edit orders", GUILayout.Width(120f)))
                EditOrders();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (_phase == EditorPhase.PlacingTiles || _orders.HasOrderSnapshot)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Hand:", GUILayout.Width(40f));
                if (_handKind != TileKind.None)
                {
                    var handRect = GUILayoutUtility.GetRect(TileButtonSize, TileButtonSize, GUILayout.Width(TileButtonSize), GUILayout.Height(TileButtonSize));
                    EditorTileIcons.DrawTile(handRect, _handKind, new Color(1f, 1f, 0.75f, 1f));
                }
                else
                {
                    GUILayout.Label("(empty — pick from orders/rack, or use Remove)", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawOrderColumns(bool editMode)
        {
            _orderScroll = EditorGUILayout.BeginScrollView(_orderScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(200f));

            _orders.RecomputeActiveColumnIndices(out var activeCols);
            var columns = _orders.Columns;
            var activeColumnIndex = columns.Count - 1;
            var displayOrderNumber = 0;
            EditorGUILayout.BeginHorizontal();
            for (var ci = 0; ci < columns.Count; ci++)
            {
                var col = columns[ci];
                if (!ShouldShowOrderColumn(ci, col.Count, editMode, columns.Count, _orders.HasOrderSnapshot, _orders.SnapshotOrderCount))
                    continue;

                displayOrderNumber++;
                EditorGUILayout.BeginVertical(GUILayout.Width(TileButtonSize + 8f));
                var isActiveColumn = editMode && ci == activeColumnIndex;
                var headerStyle = isActiveColumn ? EditorStyles.boldLabel : EditorStyles.centeredGreyMiniLabel;
                EditorGUILayout.LabelField($"#{displayOrderNumber}", headerStyle, GUILayout.Width(TileButtonSize + 4f));

                if (col.Count == 0 && editMode)
                {
                    var rect = GUILayoutUtility.GetRect(TileButtonSize, TileButtonSize, GUILayout.Width(TileButtonSize), GUILayout.Height(TileButtonSize));
                    var slotColor = isActiveColumn
                        ? new Color(0.22f, 0.30f, 0.38f, 1f)
                        : new Color(0.16f, 0.16f, 0.18f, 1f);
                    EditorTileIcons.DrawEmptySlot(rect, slotColor);
                    if (isActiveColumn)
                        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), new Color(0.45f, 0.75f, 1f, 1f));
                }
                else
                {
                    for (var ti = 0; ti < col.Count; ti++)
                    {
                        var kind = col[ti];
                        var rect = GUILayoutUtility.GetRect(TileButtonSize, TileButtonSize, GUILayout.Width(TileButtonSize), GUILayout.Height(TileButtonSize));
                        var isActiveOrder = !editMode && _placeMode == PlaceMode.Place && CanUseBoardTools() &&
                                            activeCols.Contains(ci);
                        var tileTint = isActiveOrder ? new Color(0.72f, 1f, 0.78f, 1f) : Color.white;
                        if (EditorTileIcons.TileButton(rect, kind, selected: isActiveOrder, tint: tileTint))
                            HandleOrderTileClick(ci, ti);
                        DrawOrderTileActiveIndicator(rect, isActiveOrder, showInactiveBar: !editMode && _orders.IsFinalized);
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        static void DrawOrderTileActiveIndicator(Rect rect, bool isActive, bool showInactiveBar)
        {
            if (isActive)
            {
                var green = new Color(0.1f, 0.88f, 0.32f, 1f);
                const float bar = 6f;
                const float side = 3f;
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, bar), green);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - bar, rect.width, bar), green);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, side, rect.height), green);
                EditorGUI.DrawRect(new Rect(rect.xMax - side, rect.y, side, rect.height), green);
                return;
            }

            if (!showInactiveBar) return;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(0.5f, 0.5f, 0.54f, 1f));
        }

        static bool ShouldShowOrderColumn(
            int columnIndex,
            int tileCount,
            bool editMode,
            int columnCount,
            bool hasOrderSnapshot,
            int snapshotOrderCount)
        {
            if (tileCount > 0) return true;
            if (!editMode) return false;
            if (!hasOrderSnapshot) return true;
            return columnIndex >= snapshotOrderCount && columnIndex == columnCount - 1;
        }

        void DrawBoardSection()
        {
            EditorGUILayout.LabelField("Board", EditorStyles.boldLabel);
            if (_board.Cells == null)
                _board.EnsureGrid();

            string toolLabel;
            if (!CanUseBoardTools())
                toolLabel = "Build orders, then Finalize to place tiles on the board.";
            else if (IsEditOrdersMode)
                toolLabel = "Edit orders: use the palette and order columns. Place, Remove, and Remove all are disabled until you finalize again.";
            else if (_placeMode == PlaceMode.Remove)
                toolLabel = "Remove tool: left-click a clickable top board tile or a rack tile — it returns to its original order column.";
            else
                toolLabel = "Place tool: left-click places the hand tile from orders/rack onto the board.";
            EditorGUILayout.HelpBox(
                $"Placement lattice {_board.PlacementWidth}×{_board.PlacementHeight} (major {_board.MajorWidth}×{_board.MajorHeight}). " +
                "Each tile is centered on a grid intersection and covers the 4 cells around it. " +
                toolLabel,
                MessageType.None);

            _boardScroll = EditorGUILayout.BeginScrollView(_boardScroll);
            EditorGUILayout.BeginHorizontal();

            var boardW = (_board.PlacementWidth + 1) * FineCellSize + 2f * BoardMargin;
            var boardH = (_board.PlacementHeight + 1) * FineCellSize + 2f * BoardMargin;
            var boardRect = GUILayoutUtility.GetRect(boardW, boardH, GUILayout.Width(boardW), GUILayout.Height(boardH));
            DrawBoardLattice(boardRect);

            GUILayout.Space(12f);
            DrawRackColumn();

            GUILayout.Space(12f);
            DrawBoardToolsColumn();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        void DrawBoardToolsColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(150f));
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            // Board tools are disabled while editing order columns.
            var toolsEnabled = CanInteractWithBoard();
            EditorGUI.BeginDisabledGroup(!toolsEnabled);
            var toolIndex = _placeMode == PlaceMode.Place ? 0 : 1;
            var nextToolIndex = GUILayout.Toolbar(toolIndex, new[] { "Place", "Remove" }, GUILayout.Width(140f));
            if (toolsEnabled && nextToolIndex != toolIndex)
                SetPlaceMode(nextToolIndex == 0 ? PlaceMode.Place : PlaceMode.Remove);

            GUILayout.Space(8f);
            if (GUILayout.Button("Remove all", GUILayout.Width(140f), GUILayout.Height(28f)))
                RemoveAllTilesToOrders();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                !CanUseBoardTools()
                    ? "Finalize orders to enable Place / Remove."
                    : IsEditOrdersMode
                        ? "Board tools are off during Edit orders. Click order tiles to remove them, or Finalize orders when done."
                        : _placeMode == PlaceMode.Remove
                            ? "Remove: click a board or rack tile → order."
                            : "Place: put hand / rack tiles on the board.",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        void DrawBoardLattice(Rect area)
        {
            var evt = Event.current;
            var pw = _board.PlacementWidth;
            var ph = _board.PlacementHeight;
            var ox = area.x + BoardMargin;
            var oy = area.y + BoardMargin;
            // The grid has one extra cell on each axis so a tile centered on any interior intersection fits.
            var gridW = (pw + 1) * FineCellSize;
            var gridH = (ph + 1) * FineCellSize;

            EditorGUI.DrawRect(area, new Color(0.11f, 0.11f, 0.13f, 1f));

            UpdateBoardHover(evt.mousePosition, ox, oy, pw, ph);

            // Fine placement grid (half-cell lattice, like the scene editor's green lines).
            var lineColor = new Color(0.3f, 0.85f, 0.4f, 0.28f);
            for (var i = 0; i <= pw + 1; i++)
                EditorGUI.DrawRect(new Rect(ox + i * FineCellSize, oy, 1f, gridH), lineColor);
            for (var j = 0; j <= ph + 1; j++)
                EditorGUI.DrawRect(new Rect(ox, oy + j * FineCellSize, gridW, 1f), lineColor);

            // Intersection dots (tile centers) so every drop position is visible.
            var dotColor = new Color(1f, 1f, 1f, 0.18f);
            for (var py = 0; py < ph; py++)
            for (var px = 0; px < pw; px++)
            {
                var c = CellCenter(px, py, ox, oy, ph);
                EditorGUI.DrawRect(new Rect(c.x - 1.5f, c.y - 1.5f, 3f, 3f), dotColor);
            }

            // Hover highlight over the 2x tile footprint: green to place, red to remove.
            if (_hoverValid && _hoverPx >= 0 && _hoverPy >= 0)
            {
                var hoverColor = _placeMode == PlaceMode.Remove
                    ? new Color(0.9f, 0.35f, 0.3f, 0.45f)
                    : new Color(0.35f, 0.85f, 0.45f, 0.4f);
                EditorGUI.DrawRect(TileRect(_hoverPx, _hoverPy, ox, oy, ph), hoverColor);
            }

            // Tiles: drawn at 2x cell size centered on the placement point. Draw order = layer ascending
            // (then bottom rows last within a layer) so higher-layer tiles render on top of lower ones.
            _drawOrderScratch.Clear();
            for (var py = 0; py < ph; py++)
            for (var px = 0; px < pw; px++)
            {
                if (_board.TryGetTopTileAt(px, py, out var layer, out var kind))
                    _drawOrderScratch.Add((layer, px, py, kind));
            }

            _drawOrderScratch.Sort((a, b) =>
            {
                if (a.layer != b.layer) return a.layer.CompareTo(b.layer);
                return b.py.CompareTo(a.py);
            });

            _maxVisibleLayer = 0;
            for (var i = 0; i < _drawOrderScratch.Count; i++)
                _maxVisibleLayer = Mathf.Max(_maxVisibleLayer, _drawOrderScratch[i].layer);

            for (var i = 0; i < _drawOrderScratch.Count; i++)
            {
                var (layer, px, py, kind) = _drawOrderScratch[i];
                var rect = TileRect(px, py, ox, oy, ph);
                var clickable = TileClickability.IsClickable(_board.Cells, px, py, layer);
                var tint = clickable ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
                EditorTileIcons.DrawTile(rect, kind, tint, TileBackgroundForLayer(layer), null, 2.5f);
                var badge = new Rect(rect.xMax - 16f, rect.y + 2f, 14f, 14f);
                EditorGUI.DrawRect(badge, new Color(0.08f, 0.08f, 0.1f, 1f));
                GUI.Label(badge, layer.ToString(), LayerBadgeStyle);
            }

            if (evt.type == EventType.MouseDown && area.Contains(evt.mousePosition))
            {
                if (_placeMode == PlaceMode.Remove && evt.button == 0)
                {
                    if (TryPointerToTopTile(evt.mousePosition, ox, oy, ph, out var hitPx, out var hitPy))
                        HandleBoardLeftClick(hitPx, hitPy);
                }
                else if (TryPointerToPlacement(evt.mousePosition, ox, oy, pw, ph, out var cpx, out var cpy))
                {
                    if (evt.button == 0)
                        HandleBoardLeftClick(cpx, cpy);
                    else if (evt.button == 1)
                        HandleBoardRightClick(cpx, cpy);
                }
                evt.Use();
                Repaint();
            }

            if (evt.type == EventType.MouseMove || evt.type == EventType.MouseDrag)
                Repaint();
        }

        /// <summary>Tile background that lightens with depth so higher layers stand out; distinct from the board bg.</summary>
        Color TileBackgroundForLayer(int layer)
        {
            var maxLayer = Mathf.Max(1, _maxVisibleLayer);
            var t = Mathf.Clamp01(layer / (float)maxLayer);
            return Color.Lerp(new Color(0.14f, 0.15f, 0.19f, 1f), new Color(0.9f, 0.94f, 1f, 1f), t);
        }

        /// <summary>Grid-intersection pixel where placement (px, py) is centered; row 0 is the bottom row.</summary>
        static Vector2 CellCenter(int px, int py, float ox, float oy, int placementHeight)
        {
            var cx = ox + (px + 1) * FineCellSize;
            var cy = oy + (placementHeight - py) * FineCellSize;
            return new Vector2(cx, cy);
        }

        /// <summary>2x-cell tile footprint centered on the placement point (overlaps neighbouring cells).</summary>
        static Rect TileRect(int px, int py, float ox, float oy, int placementHeight)
        {
            var c = CellCenter(px, py, ox, oy, placementHeight);
            return new Rect(c.x - TileDrawSize * 0.5f, c.y - TileDrawSize * 0.5f, TileDrawSize, TileDrawSize);
        }

        bool TryPointerToTopTile(Vector2 pointer, float ox, float oy, int placementHeight, out int px, out int py)
        {
            px = -1;
            py = -1;

            for (var i = _drawOrderScratch.Count - 1; i >= 0; i--)
            {
                var item = _drawOrderScratch[i];
                if (!TileRect(item.px, item.py, ox, oy, placementHeight).Contains(pointer))
                    continue;

                px = item.px;
                py = item.py;
                return true;
            }

            return false;
        }

        void UpdateBoardHover(Vector2 pointer, float ox, float oy, int pw, int ph)
        {
            _hoverPx = -1;
            _hoverPy = -1;
            _hoverValid = false;
            if (!TryPointerToPlacement(pointer, ox, oy, pw, ph, out var px, out var py))
                return;

            _hoverPx = px;
            _hoverPy = py;
            if (!CanInteractWithBoard())
                _hoverValid = false;
            else if (_placeMode == PlaceMode.Remove)
                _hoverValid = IsRemovableTop(px, py);
            else
                _hoverValid = _handKind != TileKind.None && _board.CanPlaceAt(px, py);
        }

        static bool TryPointerToPlacement(Vector2 pointer, float ox, float oy, int pw, int ph, out int px, out int py)
        {
            px = -1;
            py = -1;
            // Snap to the nearest grid intersection: line k -> placement index k-1 (tile fills the 4 cells around it).
            var k = Mathf.RoundToInt((pointer.x - ox) / FineCellSize);
            var m = Mathf.RoundToInt((pointer.y - oy) / FineCellSize);
            var cpx = k - 1;
            var cpy = ph - m;
            if (cpx < 0 || cpx >= pw || cpy < 0 || cpy >= ph) return false;
            px = cpx;
            py = cpy;
            return true;
        }

        void DrawRackColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RackCellSize + 16f));
            EditorGUILayout.LabelField("Rack", EditorStyles.boldLabel);
            var rack = _board.RackSlots;
            if (rack == null) return;

            for (var i = 0; i < GameConstants.RackCapacity; i++)
            {
                var rect = GUILayoutUtility.GetRect(RackCellSize, RackCellSize, GUILayout.Width(RackCellSize), GUILayout.Height(RackCellSize));
                EditorTileIcons.DrawEmptySlot(rect, new Color(0.14f, 0.14f, 0.16f, 1f));
                if (rack[i].HasValue)
                    EditorTileIcons.DrawTile(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), rack[i].Value);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    HandleRackClick(i);
                    Event.current.Use();
                    Repaint();
                }
            }

            EditorGUILayout.EndVertical();
        }

        void DrawFooter()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            if (!string.IsNullOrEmpty(_validationMessage))
            {
                var type = _validationMessage == "Valid." ? MessageType.Info : MessageType.Warning;
                EditorGUILayout.HelpBox(_validationMessage, type);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate layout"))
                ValidateLayout();
            EditorGUILayout.EndHorizontal();
        }

        void HandleOrderTileClick(int columnIndex, int tileIndex)
        {
            if (IsEditOrdersMode || !_orders.IsFinalized)
            {
                _orders.RemoveTileFromColumn(columnIndex, tileIndex);
                MaintainOrderColumnIndices();
                return;
            }

            if (_phase != EditorPhase.PlacingTiles) return;
            if (_placeMode == PlaceMode.Remove) return;
            PickHandFromOrder(columnIndex, tileIndex, requireActiveOnly: true);
        }

        void PickHandFromOrder(int columnIndex, int tileIndex, bool requireActiveOnly)
        {
            var prevHand = _handKind;
            var prevFromRack = _handFromRack;
            var sourceOrderCol = _handOrderColumnIndex;
            var sourceSnapIcon = _handOrderSnapIcon;
            var sourceRackSlot = _handRackSlotIndex;

            var takenOk = requireActiveOnly
                ? _orders.TryTakeActiveOrderTileToHand(columnIndex, tileIndex, out var taken, out var snapIcon)
                : _orders.TryTakeOrderTileToHand(columnIndex, tileIndex, out taken, out snapIcon);
            if (!takenOk) return;

            ReturnHandToSource(prevHand, prevFromRack, sourceOrderCol, sourceSnapIcon, sourceRackSlot);
            _handKind = taken;
            _handFromRack = false;
            _handOrderColumnIndex = columnIndex;
            _handOrderSnapIcon = snapIcon;
            _handRackSlotIndex = -1;
            _placeMode = PlaceMode.Place;
            _phase = EditorPhase.PlacingTiles;
        }

        void HandleBoardLeftClick(int px, int py)
        {
            if (!CanInteractWithBoard()) return;

            if (_placeMode == PlaceMode.Remove)
            {
                RemoveTopTileGameLike(px, py);
                return;
            }

            if (_handKind == TileKind.None) return;
            if (!_board.CanPlaceAt(px, py)) return;
            if (!_board.TryPlaceAt(px, py, _handKind, _handOrderColumnIndex, _handOrderSnapIcon)) return;
            AdvanceHandAfterPlacement();
        }

        void HandleBoardRightClick(int px, int py)
        {
            if (!CanInteractWithBoard()) return;
            if (_placeMode == PlaceMode.Remove) return;
            if (!_board.TryRemoveTopAt(px, py)) return;
            _validationMessage = "";
        }

        /// <summary>Only the topmost tile at a cell that isn't covered by an overlapping neighbour can be removed.</summary>
        bool IsRemovableTop(int px, int py)
        {
            // Treat any top tile at this placement as removable for the Remove tool.
            // The board model already guarantees we only ever take the visible top tile.
            return _board.TryGetTopTileAt(px, py, out _, out _);
        }

        /// <summary>
        /// Pull a clickable top tile off the board and restore it into its exact original order
        /// column + icon slot (e.g. 4th order, 2nd icon).
        /// </summary>
        void RemoveTopTileGameLike(int px, int py)
        {
            if (!IsRemovableTop(px, py))
            {
                _statusMessage = "That tile is covered — only clickable top tiles can be removed.";
                return;
            }

            if (!_board.TryRemoveTopAt(px, py, out var kind, out _, out var orderCol, out var orderIcon)) return;

            if (_orders.TryReturnTileToOrders(kind, orderCol, orderIcon))
            {
                _statusMessage = orderCol >= 0 && orderIcon >= 0
                    ? $"Removed {(int)kind} ({kind}) → order #{orderCol + 1}, icon #{orderIcon + 1}."
                    : $"Removed {(int)kind} ({kind}) → back to its order.";
                _validationMessage = "";
                return;
            }

            _board.TryPlaceAt(px, py, kind, orderCol, orderIcon);
            _statusMessage = "Could not return tile to orders — removal cancelled.";
        }

        void HandleRackClick(int slot)
        {
            if (!CanInteractWithBoard()) return;

            if (_placeMode == PlaceMode.Remove)
            {
                RemoveRackTileToOrders(slot);
                return;
            }

            _phase = EditorPhase.PlacingTiles;
            var rack = _board.RackSlots;
            if (rack == null) return;

            if (rack[slot].HasValue)
            {
                PickHandFromOccupiedRackSlot(slot);
                return;
            }

            if (_handKind == TileKind.None) return;
            if (!_board.TryPlaceInRack(slot, _handKind, _handOrderColumnIndex, _handOrderSnapIcon)) return;
            AdvanceHandAfterPlacement();
        }

        /// <summary>
        /// Pull a rack tile off and restore it into its exact original order column + icon slot.
        /// </summary>
        void RemoveRackTileToOrders(int slot)
        {
            var rack = _board.RackSlots;
            if (rack == null || !rack[slot].HasValue) return;

            if (!_board.TryTakeFromRack(slot, out var kind, out var orderCol, out var orderIcon)) return;

            if (_orders.TryReturnTileToOrders(kind, orderCol, orderIcon))
            {
                _statusMessage = orderCol >= 0 && orderIcon >= 0
                    ? $"Removed {(int)kind} ({kind}) from rack → order #{orderCol + 1}, icon #{orderIcon + 1}."
                    : $"Removed {(int)kind} ({kind}) from rack → back to its order.";
                _validationMessage = "";
                return;
            }

            _board.TryPlaceInRack(slot, kind, orderCol, orderIcon);
            _statusMessage = "Could not return rack tile to orders — removal cancelled.";
        }

        void PickHandFromOccupiedRackSlot(int slot)
        {
            if (!_board.TryTakeFromRack(slot, out var taken, out var takenCol, out var takenIcon)) return;
            var previousHand = _handKind;
            var previousFromRack = _handFromRack;
            var sourceOrderCol = _handOrderColumnIndex;
            var sourceSnapIcon = _handOrderSnapIcon;
            var sourceRackSlot = _handRackSlotIndex;

            if (!previousFromRack && previousHand != TileKind.None && sourceOrderCol >= 0)
                _orders.AppendTileToColumn(sourceOrderCol, previousHand, sourceSnapIcon);
            else if (previousFromRack && previousHand != TileKind.None)
                _board.TryPlaceInRack(slot, previousHand, sourceOrderCol, sourceSnapIcon);

            _handKind = taken;
            _handFromRack = true;
            _handOrderColumnIndex = takenCol;
            _handOrderSnapIcon = takenIcon;
            _handRackSlotIndex = slot;
        }

        void ReturnHandToSource(TileKind hand, bool fromRack, int orderCol, int snapIcon, int rackSlot)
        {
            if (hand == TileKind.None) return;
            if (!fromRack && orderCol >= 0)
                _orders.AppendTileToColumn(orderCol, hand, snapIcon);
            else if (fromRack && rackSlot >= 0)
                _board.TryPlaceInRack(rackSlot, hand, orderCol, snapIcon);
        }

        void AdvanceHandAfterPlacement()
        {
            _handFromRack = false;
            _handRackSlotIndex = -1;
            if (!_orders.TryConsumeLastTileFromOrdersForPlacement(out var removed, out var sourceCol, out var sourceIcon) ||
                removed == TileKind.None)
            {
                if (_board.RackHasAnyTile())
                {
                    _handKind = TileKind.None;
                    _handOrderColumnIndex = -1;
                    _handOrderSnapIcon = -1;
                    _statusMessage = "Orders empty — pick a tile from the rack to keep placing.";
                    return;
                }

                _handKind = TileKind.None;
                _handOrderColumnIndex = -1;
                _handOrderSnapIcon = -1;
                if (!_orders.HasOrderSnapshot)
                    _phase = EditorPhase.OrderAuthoring;
                _statusMessage = "Placement complete. Validate and export when ready.";
                return;
            }

            _handKind = removed;
            _handOrderColumnIndex = sourceCol;
            _handOrderSnapIcon = sourceIcon;
            _statusMessage = $"Place tile {(int)_handKind} ({_handKind}).";
        }

        void FinalizeOrders()
        {
            if (_orders.IsFinalized) return;

            ReturnHandToSource(_handKind, _handFromRack, _handOrderColumnIndex, _handOrderSnapIcon, _handRackSlotIndex);
            _handKind = TileKind.None;
            _handFromRack = false;
            _handOrderColumnIndex = -1;
            _handOrderSnapIcon = -1;
            _handRackSlotIndex = -1;

            var cols = _orders.Columns;
            var hasLiveTiles = false;
            for (var i = 0; i < cols.Count; i++)
            {
                if (cols[i].Count > 0) { hasLiveTiles = true; break; }
            }

            // Empty live strips are OK when we already have a snapshot (everything may be on the board already).
            if (!hasLiveTiles && !_orders.HasOrderSnapshot)
            {
                _statusMessage = "Add at least one tile to orders before finalize.";
                return;
            }

            _pendingOrderTilesScratch.Clear();
            if (_orders.HasOrderSnapshot)
                _board.CollectTilesOriginatingFromOrderColumnAtLeast(_orders.SnapshotOrderCount, _pendingOrderTilesScratch);

            _orders.Finalize(_pendingOrderTilesScratch);
            MaintainOrderColumnIndices();
            if (_board.CollectBoardKindInts().Count == 0)
                _board.ClearBoardAndRack();
            _phase = EditorPhase.PlacingTiles;
            _placeMode = PlaceMode.Place;
            _handFromRack = false;
            _handRackSlotIndex = -1;

            if (!_orders.TryConsumeLastTileFromOrdersForPlacement(out var hand, out var sourceCol, out var sourceIcon) || hand == TileKind.None)
            {
                _handKind = TileKind.None;
                _handOrderSnapIcon = -1;
                _statusMessage = hasLiveTiles
                    ? "Orders finalized. Board is ready — load tiles from rack if needed."
                    : "Orders finalized. All tiles are already on the board — use Place/Remove to edit.";
                return;
            }

            _handKind = hand;
            _handOrderColumnIndex = sourceCol;
            _handOrderSnapIcon = sourceIcon;
            _statusMessage = $"Placement started. Hand: {(int)hand}. Click a green cell or use the rack.";
        }

        void SetPlaceMode(PlaceMode mode)
        {
            if (!CanInteractWithBoard()) return;
            _placeMode = mode;
            _phase = EditorPhase.PlacingTiles;

            if (mode == PlaceMode.Remove)
            {
                // Return whatever is in hand to its source so removal starts from a clean state.
                ReturnHandToSource(_handKind, _handFromRack, _handOrderColumnIndex, _handOrderSnapIcon, _handRackSlotIndex);
                _handKind = TileKind.None;
                _handFromRack = false;
                _handOrderColumnIndex = -1;
                _handOrderSnapIcon = -1;
                _handRackSlotIndex = -1;
                _orders.SyncReverseCursorFromLiveColumns();
                _statusMessage = "Remove tool: click a top board tile or rack tile — it goes back to its original order slot.";
                return;
            }

            // Entering Place: grab the next queued order tile into hand if idle so you can drop it.
            if (_handKind == TileKind.None &&
                _orders.TryConsumeLastTileFromOrdersForPlacement(out var hand, out var sourceCol, out var sourceIcon) &&
                hand != TileKind.None)
            {
                _handKind = hand;
                _handFromRack = false;
                _handOrderColumnIndex = sourceCol;
                _handOrderSnapIcon = sourceIcon;
                _handRackSlotIndex = -1;
                _statusMessage = $"Place tool: hand {(int)hand}. Click a green cell, or pick a tile from an order/rack.";
            }
            else
            {
                _statusMessage = "Place tool: pick a tile from an order or the rack, then click a green cell.";
            }
        }

        void EditOrders()
        {
            ReturnHandToSource(_handKind, _handFromRack, _handOrderColumnIndex, _handOrderSnapIcon, _handRackSlotIndex);
            _orders.UnlockPreserveLiveState();
            MaintainOrderColumnIndices();
            var existingPendingColumns = _orders.HasOrderSnapshot
                ? _orders.ComputeMinPendingColumnsToKeep(_board.GetMaxOriginOrderColumn())
                : 0;
            // Preserve pending orders that may currently exist only on the board, plus one extra draft
            // column so palette edits always target a real "new order" column instead of a snapshot order.
            _orders.EnsurePendingColumnCount(existingPendingColumns + 1);
            // Keep PlacingTiles phase; board interaction is gated by CanInteractWithBoard().
            _phase = EditorPhase.PlacingTiles;
            _placeMode = PlaceMode.Place;
            _handKind = TileKind.None;
            _handFromRack = false;
            _handOrderColumnIndex = -1;
            _handOrderSnapIcon = -1;
            _handRackSlotIndex = -1;
            _statusMessage = "Editing orders. Use the palette and order columns. Click order tiles to remove them. Finalize when done.";
        }

        void RemoveAllTilesToOrders()
        {
            if (!CanInteractWithBoard()) return;

            ReturnHandToSource(_handKind, _handFromRack, _handOrderColumnIndex, _handOrderSnapIcon, _handRackSlotIndex);

            // Sync live pending columns with board provenance before capture.
            MaintainOrderColumnIndices();

            _pendingOrderTilesScratch.Clear();
            if (_orders.HasOrderSnapshot)
                _board.CollectTilesOriginatingFromOrderColumnAtLeast(_orders.SnapshotOrderCount, _pendingOrderTilesScratch);

            var minPendingColumns = _orders.HasOrderSnapshot
                ? _orders.ComputeMinPendingColumnsToKeep(_board.GetMaxOriginOrderColumn(), _pendingOrderTilesScratch)
                : 0;

            _board.ClearBoardAndRack();

            _orders.RestoreLiveOrdersAfterRemoveAll(_pendingOrderTilesScratch, minPendingColumns);

            _phase = EditorPhase.PlacingTiles;
            _placeMode = PlaceMode.Place;
            _handKind = TileKind.None;
            _handFromRack = false;
            _handOrderColumnIndex = -1;
            _handOrderSnapIcon = -1;
            _handRackSlotIndex = -1;
            _validationMessage = "";

            if (!IsEditOrdersMode)
                SetPlaceMode(PlaceMode.Place);
            else
                _placeMode = PlaceMode.Place;

            _statusMessage = "Removed all board tiles → orders full again.";
        }

        void NewLevel()
        {
            _orders.ClearAll();
            _board.MajorWidth = 4;
            _board.MajorHeight = 4;
            _board.Depth = 16;
            _board.EnsureGrid();
            _board.ClearBoardAndRack();
            _phase = EditorPhase.OrderAuthoring;
            _placeMode = PlaceMode.Place;
            _handKind = TileKind.None;
            _handOrderColumnIndex = -1;
            _handOrderSnapIcon = -1;
            _validationMessage = "";
            _statusMessage = "New level.";
        }

        void ValidateLayout()
        {
            if (!TryBuildSpec(out var spec, out var err))
            {
                _validationMessage = err;
                return;
            }

            _validationMessage = LevelLayoutRules.Validate(spec, out var layoutErr) ? "Valid." : layoutErr;
        }

        bool TryBuildSpec(out LevelBoardSpec spec, out string error)
        {
            var dto = _board.BuildDto();
            dto.Orders = _orders.BuildLiveOrdersDto();
            return LevelGridParser.TryBuildSpec(dto, out spec, out error);
        }

        void ExportJson()
        {
            if (_handKind != TileKind.None)
            {
                EditorUtility.DisplayDialog("Export blocked", "Place the hand tile before export.", "OK");
                return;
            }

            if (_board.RackHasAnyTile())
            {
                EditorUtility.DisplayDialog("Export blocked", "Place all rack tiles on the board before export.", "OK");
                return;
            }

            if (!_orders.TryGetSnapshotOrdersForExport(out var ordersDto, out var ordersErr))
            {
                EditorUtility.DisplayDialog("Export failed", ordersErr, "OK");
                return;
            }

            if (_orders.HasOrderSnapshot && !_orders.IsFinalized)
            {
                _pendingOrderTilesScratch.Clear();
                _board.CollectTilesOriginatingFromOrderColumnAtLeast(_orders.SnapshotOrderCount, _pendingOrderTilesScratch);
                var hasPendingLive = false;
                var cols = _orders.Columns;
                for (var c = _orders.SnapshotOrderCount; c < cols.Count; c++)
                {
                    if (cols[c].Count > 0) { hasPendingLive = true; break; }
                }

                if (_pendingOrderTilesScratch.Count > 0 || hasPendingLive)
                {
                    EditorUtility.DisplayDialog(
                        "Export blocked",
                        "You have new orders that are not finalized. Click Finalize orders before export.",
                        "OK");
                    return;
                }
            }

            var boardKinds = _board.CollectBoardKindInts();
            var orderFlat = LevelEditorBoardModel.FlattenOrders(ordersDto);
            if (orderFlat.Count == 0)
            {
                EditorUtility.DisplayDialog("Export failed", "Orders export produced no icons.", "OK");
                return;
            }

            if (!LevelEditorBoardModel.MultisetsEqualSorted(boardKinds, orderFlat))
            {
                _pendingOrderTilesScratch.Clear();
                if (_orders.HasOrderSnapshot)
                    _board.CollectTilesOriginatingFromOrderColumnAtLeast(_orders.SnapshotOrderCount, _pendingOrderTilesScratch);

                var message = _pendingOrderTilesScratch.Count > 0
                    ? "Board tiles do not match the export snapshot. New order tiles are on the board but were not captured — click Finalize orders, then export again."
                    : "Board tiles do not match the orders. Place every order/rack tile back on the board (or remove extras) so the counts match.";

                EditorUtility.DisplayDialog("Export failed", message, "OK");
                return;
            }

            var dto = _board.BuildDto();
            dto.Orders = ordersDto;
            if (!LevelGridParser.TryBuildLevelDefinition(dto, out _, out var err))
            {
                if (!EditorUtility.DisplayDialog("Layout warning", err + "\n\nExport JSON anyway?", "Export", "Cancel"))
                    return;
            }

            var path = EditorUtility.SaveFilePanel("Export level JSON", Application.dataPath, "level", "json");
            if (string.IsNullOrEmpty(path)) return;

            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(path);
            _statusMessage = $"Exported to {path}";
        }

        void ImportJson()
        {
            var path = EditorUtility.OpenFilePanel("Import level JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            var json = File.ReadAllText(path);
            if (!LevelGridParser.TryParseJson(json, out LevelDefinition definition, out var err))
            {
                EditorUtility.DisplayDialog("Import failed", err, "OK");
                return;
            }

            ApplyDefinition(definition, Path.GetFileName(path));
        }

        void LoadLevelFromAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (ta == null)
            {
                EditorUtility.DisplayDialog("Load failed", $"Could not load TextAsset at {assetPath}", "OK");
                return;
            }

            if (!LevelGridParser.TryParseJson(ta.text, out LevelDefinition definition, out var err))
            {
                EditorUtility.DisplayDialog("Load failed", err, "OK");
                return;
            }

            ApplyDefinition(definition, Path.GetFileNameWithoutExtension(assetPath));
        }

        void ApplyDefinition(LevelDefinition definition, string label)
        {
            _board.LoadFromSpec(definition.Board);
            _board.ClearRack();
            // The board arrives fully built, so orders are "finalized, everything already placed": the snapshot
            // holds the real orders (for export) while the live order columns start empty. Start in Remove mode
            // so the level is immediately editable — pull top tiles off (they return to orders), then Place
            // them back like when creating a new level.
            _orders.LoadFinalizedAllPlaced(definition.Orders);
            _board.AssignOriginsFromSnapshot(_orders.SnapshotColumns);
            _phase = EditorPhase.PlacingTiles;
            _placeMode = PlaceMode.Remove;
            _handKind = TileKind.None;
            _handFromRack = false;
            _handOrderColumnIndex = -1;
            _handOrderSnapIcon = -1;
            _handRackSlotIndex = -1;
            _validationMessage = "Loaded (parsed + layout rules passed).";
            _statusMessage = $"Editing {label}. Orders start empty (everything is on the board). Remove a tile to send it back to its original order slot, then Place to put it elsewhere.";
            Repaint();
        }
    }
}
#endif
