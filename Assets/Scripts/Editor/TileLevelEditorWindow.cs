#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
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
    string _lastValidation = "";

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
        _currentLayer = EditorGUILayout.IntSlider("Edit layer", _currentLayer, 0, Mathf.Max(0, _depth - 1));
        EditorGUILayout.HelpBox(
            "Rules: (1) Same layer — no two tiles with |Δcol|≤1 and |Δrow|≤1. (2) No tile stacked in same (col,row) on consecutive layers. (3) Layer >0 — each tile needs a tile on the layer below in |Δcol|≤1 and |Δrow|≤1. Click cycles: empty → 0…14 → empty.",
            MessageType.Info);

        if (_cells == null) EnsureGrid();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        var cell = GUILayout.Width(36);
        for (var y = _height - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"r{y}", GUILayout.Width(28));
            for (var x = 0; x < _width; x++)
            {
                var v = _cells[x, y, _currentLayer];
                var label = v < 0 ? "·" : v.ToString();
                if (GUILayout.Button(label, cell))
                {
                    _cells[x, y, _currentLayer] = Cycle(v);
                    _lastValidation = "";
                }
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
        };
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
        if (!LevelGridParser.TryParseJson(json, out var spec, out var err))
        {
            EditorUtility.DisplayDialog("Import failed", err, "OK");
            return;
        }

        ApplySpecToGrid(spec);
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

        if (!LevelGridParser.TryParseJson(ta.text, out var spec, out var err))
        {
            EditorUtility.DisplayDialog("Load failed", err, "OK");
            return;
        }

        ApplySpecToGrid(spec);
        _lastValidation = $"Loaded Resources/{resourcesPathWithoutExtension} (layout rules passed).";
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
