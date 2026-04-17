#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core;
using LevelData;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>Author 3D matrix levels with layout rules; export/import JSON matching <see cref="Matrix3DLevelJsonDto"/>.</summary>
public sealed class TileLevelEditorWindow : EditorWindow
{
    int _width = 3;
    int _height = 3;
    int _depth = 2;
    int[,,] _cells;
    int _currentLayer;
    Vector2 _scroll;
    string _ordersText = "{{0,0,0}}";
    string _lastValidation = "";
    readonly List<int> _reverseBuildQueue = new List<int>();
    readonly List<int> _plannedRackTiles = new List<int>();
    int _reverseBuildIndex;
    int _selectedRackTileIndex = -1;
    bool _reverseBuildActive;

    [MenuItem("Window/Tile Level Editor")]
    static void Open() => GetWindow<TileLevelEditorWindow>("Tile Level Editor");

    void OnEnable() => EnsureGrid();

    void EnsureGrid()
    {
        _width = Mathf.Max(1, _width);
        _height = Mathf.Max(1, _height);
        _depth = Mathf.Max(1, _depth);
        var next = new int[_width, _height, _depth];
        if (_cells != null)
        {
            var cw = Mathf.Min(_width, _cells.GetLength(0));
            var ch = Mathf.Min(_height, _cells.GetLength(1));
            var cd = Mathf.Min(_depth, _cells.GetLength(2));
            for (var x = 0; x < cw; x++)
            for (var y = 0; y < ch; y++)
            for (var l = 0; l < cd; l++)
                next[x, y, l] = _cells[x, y, l];
        }

        for (var x = 0; x < _width; x++)
        for (var y = 0; y < _height; y++)
        for (var l = 0; l < _depth; l++)
        {
            if (_cells == null || x >= _cells.GetLength(0) || y >= _cells.GetLength(1) || l >= _cells.GetLength(2))
                next[x, y, l] = -1;
        }

        _cells = next;
        _currentLayer = Mathf.Clamp(_currentLayer, 0, _depth - 1);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Grid size", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _width = EditorGUILayout.IntField("Width", _width);
        _height = EditorGUILayout.IntField("Height", _height);
        _depth = EditorGUILayout.IntField("Depth", _depth);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Apply size (preserves overlapping region)"))
            EnsureGrid();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Orders", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Enter orders as list of lists, e.g. {{3,3,3},{4,4,4},{2,2,2,2},{3,5,7}}. Order lengths may differ.",
            MessageType.None);
        _ordersText = EditorGUILayout.TextArea(_ordersText, GUILayout.MinHeight(52));
        if (TryParseOrdersText(_ordersText, out var parsedOrders, out var ordersErr))
            EditorGUILayout.HelpBox($"Parsed {parsedOrders.Count} orders, total icons: {CountOrderIcons(parsedOrders)}.", MessageType.Info);
        else
            EditorGUILayout.HelpBox($"Orders parse error: {ordersErr}", MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(parsedOrders == null || parsedOrders.Count == 0);
        if (GUILayout.Button("Confirm Orders & Start Reverse Build"))
            StartReverseBuild(parsedOrders);
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!_reverseBuildActive || !TryGetCurrentReverseTile(out _) || !CanSendCurrentToRack());
        if (GUILayout.Button("Send Current Tile To Rack"))
            ConsumeCurrentReverseTile(toRack: true);
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!_reverseBuildActive);
        if (GUILayout.Button("Stop Reverse Build"))
            StopReverseBuild();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (_reverseBuildActive && TryGetCurrentReverseTile(out var currentReverseTile))
        {
            EditorGUILayout.HelpBox(
                $"Reverse build step {_reverseBuildIndex + 1}/{_reverseBuildQueue.Count}: current tile {currentReverseTile}. Click a green empty cell to place current tile, or pick a rack tile to place instead.",
                MessageType.Info);
            if (_plannedRackTiles.Count > 0)
            {
                EditorGUILayout.HelpBox($"Planned rack tiles: {BuildIntListText(_plannedRackTiles)}", MessageType.None);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Use tile from rack:", GUILayout.Width(120f));
                for (var i = 0; i < _plannedRackTiles.Count; i++)
                {
                    var value = _plannedRackTiles[i];
                    var label = _selectedRackTileIndex == i ? $"[{value}]" : value.ToString();
                    if (GUILayout.Button(label, GUILayout.Width(38f)))
                        _selectedRackTileIndex = _selectedRackTileIndex == i ? -1 : i;
                }

                if (_selectedRackTileIndex >= 0 && GUILayout.Button("Clear", GUILayout.Width(54f)))
                    _selectedRackTileIndex = -1;
                EditorGUILayout.EndHorizontal();
            }

            if (!CanSendCurrentToRack())
                EditorGUILayout.HelpBox($"Rack is full ({GameConstants.RackCapacity}). You must place current tile on board or place one from rack first.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        _currentLayer = EditorGUILayout.IntSlider("Edit layer", _currentLayer, 0, Mathf.Max(0, _depth - 1));
        EditorGUILayout.HelpBox(
            "Rules: (1) Same layer — no two tiles with |Δcol|≤1 and |Δrow|≤1. (2) No tile stacked in same (col,row) on consecutive layers. (3) Layer >0 — each tile needs a tile on the layer below in |Δcol|≤1 and |Δrow|≤1. Click cycles: empty → 0…14 → empty. Empty cells highlighted green are currently valid placement spots.",
            MessageType.Info);

        if (_cells == null) EnsureGrid();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        var cell = GUILayout.Width(36f);
        for (var y = _height - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"r{y}", GUILayout.Width(28f));
            for (var x = 0; x < _width; x++)
            {
                var v = _cells[x, y, _currentLayer];
                var label = v < 0 ? "·" : v.ToString();
                var canPlaceHere = v >= 0 || IsPlacementAllowed(x, y, _currentLayer);
                var prevColor = GUI.backgroundColor;
                if (v < 0 && canPlaceHere)
                    GUI.backgroundColor = new Color(0.45f, 0.85f, 0.45f, 1f);
                EditorGUI.BeginDisabledGroup(v < 0 && !canPlaceHere);
                if (GUILayout.Button(label, cell))
                {
                    HandleGridCellClick(x, y, _currentLayer, v, canPlaceHere);
                }
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = prevColor;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Validate layout"))
        {
            if (TryBuildSpec(out var spec, out var err))
            {
                if (LevelLayoutRules.Validate(spec, out var layoutErr))
                    _lastValidation = "Valid.";
                else
                    _lastValidation = layoutErr;
            }
            else
                _lastValidation = err;
        }

        if (!string.IsNullOrEmpty(_lastValidation))
            EditorGUILayout.HelpBox(_lastValidation, _lastValidation == "Valid." ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export JSON…"))
            ExportJson();
        if (GUILayout.Button("Import JSON…"))
            ImportJson();
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Load Levels/level_1 (Resources)"))
            LoadLevelFromResources("Levels/level_1");
    }

    static int Cycle(int v)
    {
        if (v < 0) return 0;
        if (v < 14) return v + 1;
        return -1;
    }

    void HandleGridCellClick(int x, int y, int layer, int currentValue, bool canPlaceHere)
    {
        if (_reverseBuildActive &&
            currentValue < 0 &&
            canPlaceHere)
        {
            if (TryPopSelectedRackTile(out var rackTile))
            {
                _cells[x, y, layer] = rackTile;
                _lastValidation = "";
                return;
            }

            if (TryGetCurrentReverseTile(out var reverseTile))
            {
                _cells[x, y, layer] = reverseTile;
                ConsumeCurrentReverseTile(toRack: false);
                _lastValidation = "";
                return;
            }

            _lastValidation = "";
            return;
        }

        _cells[x, y, layer] = Cycle(currentValue);
        _lastValidation = "";
    }

    void StartReverseBuild(List<List<int>> parsedOrders)
    {
        _reverseBuildQueue.Clear();
        _plannedRackTiles.Clear();
        _reverseBuildIndex = 0;
        _selectedRackTileIndex = -1;
        if (parsedOrders == null)
        {
            _reverseBuildActive = false;
            return;
        }

        // Build sequence from the end of the order list and end of each order.
        for (var orderIndex = parsedOrders.Count - 1; orderIndex >= 0; orderIndex--)
        {
            var order = parsedOrders[orderIndex];
            for (var i = order.Count - 1; i >= 0; i--)
                _reverseBuildQueue.Add(order[i]);
        }

        _reverseBuildActive = _reverseBuildQueue.Count > 0;
        if (_reverseBuildActive)
            _lastValidation = $"Reverse build started. Total steps: {_reverseBuildQueue.Count}.";
    }

    void StopReverseBuild()
    {
        _reverseBuildActive = false;
        _reverseBuildIndex = 0;
        _reverseBuildQueue.Clear();
        _plannedRackTiles.Clear();
        _selectedRackTileIndex = -1;
    }

    bool TryGetCurrentReverseTile(out int tile)
    {
        tile = -1;
        if (!_reverseBuildActive || _reverseBuildIndex >= _reverseBuildQueue.Count) return false;
        tile = _reverseBuildQueue[_reverseBuildIndex];
        return true;
    }

    void ConsumeCurrentReverseTile(bool toRack)
    {
        if (!TryGetCurrentReverseTile(out var current)) return;
        if (toRack)
        {
            if (!CanSendCurrentToRack())
            {
                _lastValidation = $"Rack is full ({GameConstants.RackCapacity}).";
                return;
            }

            _plannedRackTiles.Add(current);
        }

        _reverseBuildIndex++;
        if (_reverseBuildIndex >= _reverseBuildQueue.Count)
        {
            _reverseBuildActive = false;
            _lastValidation = "Reverse build complete.";
        }
    }

    bool CanSendCurrentToRack() => _plannedRackTiles.Count < GameConstants.RackCapacity;

    bool TryPopSelectedRackTile(out int tile)
    {
        tile = -1;
        if (_selectedRackTileIndex < 0 || _selectedRackTileIndex >= _plannedRackTiles.Count) return false;

        tile = _plannedRackTiles[_selectedRackTileIndex];
        _plannedRackTiles.RemoveAt(_selectedRackTileIndex);
        if (_selectedRackTileIndex >= _plannedRackTiles.Count)
            _selectedRackTileIndex = _plannedRackTiles.Count - 1;
        return true;
    }

    bool IsPlacementAllowed(int x, int y, int layer)
    {
        if (_cells == null || _cells[x, y, layer] >= 0) return false;
        return HasNoSameLayerNeighbour(x, y, layer) &&
               HasNoConsecutiveVerticalStack(x, y, layer) &&
               HasSupportFromBelow(x, y, layer);
    }

    bool HasNoSameLayerNeighbour(int x, int y, int layer)
    {
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var nx = x + dx;
            var ny = y + dy;
            if ((uint)nx >= (uint)_width || (uint)ny >= (uint)_height) continue;
            if (_cells[nx, ny, layer] >= 0) return false;
        }

        return true;
    }

    bool HasNoConsecutiveVerticalStack(int x, int y, int layer)
    {
        if (layer > 0 && _cells[x, y, layer - 1] >= 0) return false;
        if (layer + 1 < _depth && _cells[x, y, layer + 1] >= 0) return false;
        return true;
    }

    bool HasSupportFromBelow(int x, int y, int layer)
    {
        if (layer == 0) return true;
        var below = layer - 1;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var nx = x + dx;
            var ny = y + dy;
            if ((uint)nx >= (uint)_width || (uint)ny >= (uint)_height) continue;
            if (_cells[nx, ny, below] >= 0) return true;
        }

        return false;
    }

    bool TryBuildSpec(out LevelBoardSpec spec, out string error) =>
        LevelGridParser.TryBuildSpec(BuildDto(), out spec, out error);

    Matrix3DLevelJsonDto BuildDto()
    {
        var layers = new List<List<List<int>>>();
        for (var l = 0; l < _depth; l++)
        {
            var layer = new List<List<int>>();
            for (var y = 0; y < _height; y++)
            {
                var row = new List<int>();
                for (var x = 0; x < _width; x++)
                    row.Add(_cells[x, y, l]);
                layer.Add(row);
            }

            layers.Add(layer);
        }

        return new Matrix3DLevelJsonDto
        {
            Width = _width,
            Height = _height,
            Depth = _depth,
            Matrix3D = layers,
            Orders = BuildOrdersForDto(),
        };
    }

    List<List<int>> BuildOrdersForDto()
    {
        if (TryParseOrdersText(_ordersText, out var parsed, out _))
            return parsed;
        return new List<List<int>>();
    }

    void ExportJson()
    {
        if (!TryBuildSpec(out _, out var err))
        {
            if (!EditorUtility.DisplayDialog("Layout warning", err + "\n\nExport JSON anyway?", "Export", "Cancel"))
                return;
        }

        var path = EditorUtility.SaveFilePanel("Export level JSON", Application.dataPath, "level", "json");
        if (string.IsNullOrEmpty(path)) return;

        var json = JsonConvert.SerializeObject(BuildDto(), Formatting.Indented);
        File.WriteAllText(path, json);
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(path);
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

        ApplySpecToGrid(definition.Board);
        _ordersText = BuildOrdersText(definition.Orders);
        _lastValidation = "Imported (parsed + layout rules passed).";
    }

    /// <summary>Loads <c>Assets/Resources/{path}.json</c> as a <see cref="TextAsset"/> (no extension in argument).</summary>
    void LoadLevelFromResources(string resourcesPathWithoutExtension)
    {
        var ta = Resources.Load<TextAsset>(resourcesPathWithoutExtension);
        if (ta == null)
        {
            EditorUtility.DisplayDialog(
                "Load failed",
                $"Resources.Load<TextAsset>(\"{resourcesPathWithoutExtension}\") returned null.\n" +
                $"Add the file at Assets/Resources/{resourcesPathWithoutExtension}.json and ensure it is imported as TextAsset.",
                "OK");
            return;
        }

        if (!LevelGridParser.TryParseJson(ta.text, out LevelDefinition definition, out var err))
        {
            EditorUtility.DisplayDialog("Load failed", err, "OK");
            return;
        }

        ApplySpecToGrid(definition.Board);
        _ordersText = BuildOrdersText(definition.Orders);
        _lastValidation = $"Loaded Resources/{resourcesPathWithoutExtension} (layout rules passed).";
    }

    static string BuildOrdersText(LevelOrdersSpec orders)
    {
        if (orders == null || orders.OrderCount == 0) return "{}";
        var sb = new StringBuilder();
        sb.Append('{');
        for (var oi = 0; oi < orders.OrderCount; oi++)
        {
            if (oi > 0) sb.Append(',');
            var order = orders.Orders[oi];
            sb.Append('{');
            for (var i = 0; i < order.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append((int)order.GetIcon(i));
            }

            sb.Append('}');
        }

        sb.Append('}');
        return sb.ToString();
    }

    static bool TryParseOrdersText(string input, out List<List<int>> orders, out string error)
    {
        orders = null;
        error = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Orders text is empty.";
            return false;
        }

        var normalized = input.Replace('{', '[').Replace('}', ']').Trim();
        try
        {
            orders = JsonConvert.DeserializeObject<List<List<int>>>(normalized);
        }
        catch (JsonException e)
        {
            error = e.Message;
            return false;
        }

        if (orders == null || orders.Count == 0)
        {
            error = "No orders found.";
            return false;
        }

        for (var oi = 0; oi < orders.Count; oi++)
        {
            var row = orders[oi];
            if (row == null || row.Count == 0)
            {
                error = $"Order {oi} is empty.";
                return false;
            }

            for (var i = 0; i < row.Count; i++)
            {
                var v = row[i];
                if (v < 0 || v >= GameConstants.PlayableTileKindCount)
                {
                    error = $"orders[{oi}][{i}] must be 0..{GameConstants.PlayableTileKindCount - 1}, got {v}.";
                    return false;
                }
            }
        }

        return true;
    }

    static int CountOrderIcons(List<List<int>> orders)
    {
        var n = 0;
        for (var i = 0; i < orders.Count; i++)
            n += orders[i].Count;
        return n;
    }

    static string BuildIntListText(List<int> values)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(values[i]);
        }

        sb.Append('}');
        return sb.ToString();
    }

    void ApplySpecToGrid(LevelBoardSpec spec)
    {
        _width = spec.Width;
        _height = spec.Height;
        _depth = spec.Depth;
        _cells = new int[_width, _height, _depth];
        for (var l = 0; l < _depth; l++)
        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var v = spec.GetNullable(x, y, l);
            _cells[x, y, l] = v.HasValue ? (int)v.Value : -1;
        }

        _currentLayer = 0;
    }
}
#endif
