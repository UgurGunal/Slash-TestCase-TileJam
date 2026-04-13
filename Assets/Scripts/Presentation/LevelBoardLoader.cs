using LevelData;
using UnityEngine;

namespace Presentation
{
    /// <summary>Loads a JSON level grid and instantiates <see cref="BoardTileView"/> under <see cref="boardRoot"/>.</summary>
    public sealed class LevelBoardLoader : MonoBehaviour
    {
        [Header("Source (first wins: assigned TextAsset, else Resources path)")]
        [SerializeField] TextAsset levelJson;
        [Tooltip("Path relative to a Resources folder, without extension. Example: Levels/level_demo for Assets/Resources/Levels/level_demo.json")]
        [SerializeField] string resourcesLevelPath = "Levels/level_demo";

        [Header("Board")]
        [SerializeField] RectTransform boardRoot;
        [SerializeField] BoardTileView tilePrefab;
        [Tooltip("Optional: 15 sprites (Type0–Type14). Empty slots use Resources/TileIcons/{name}.")]
        [SerializeField] TileIconLibrary tileIconLibrary;

        [Header("Cell size (canvas units)")]
        [Tooltip("Grid spacing and tile root width. If ≤ 0, falls back to 100.")]
        [SerializeField] float cellWidth = 100f;
        [Tooltip("Grid spacing and tile root height. If ≤ 0, falls back to 100.")]
        [SerializeField] float cellHeight = 100f;

        [SerializeField] bool clearExistingChildren = true;
        [SerializeField] bool loadOnAwake = true;

        public LevelBoardSpec LastSpec { get; private set; }

        void Awake()
        {
            if (loadOnAwake) Reload();
        }

        [ContextMenu("Reload level from JSON")]
        public void Reload() => Reload(resourcesLevelPath);

        /// <summary>Load by Resources path (e.g. <c>Levels/level_02</c> for <c>Assets/Resources/Levels/level_02.json</c>).</summary>
        public void Reload(string resourcesPath)
        {
            if (!string.IsNullOrWhiteSpace(resourcesPath))
                resourcesLevelPath = resourcesPath.Trim();

            if (boardRoot == null || tilePrefab == null)
            {
                Debug.LogError("[LevelBoardLoader] Assign boardRoot and tilePrefab.", this);
                return;
            }

            if (!TryGetJsonText(out var json, out var source))
            {
                Debug.LogError("[LevelBoardLoader] No JSON source found.", this);
                return;
            }

            if (!LevelGridParser.TryParseJson(json, out var spec, out var err))
            {
                Debug.LogError($"[LevelBoardLoader] {err}\nSource: {source}", this);
                return;
            }

            LastSpec = spec;
            Debug.Log($"[LevelBoardLoader] Loaded from {source}\n{LevelGridParser.BuildValidationReport(spec)}");

            if (clearExistingChildren)
            {
                for (var i = boardRoot.childCount - 1; i >= 0; i--)
                    Destroy(boardRoot.GetChild(i).gameObject);
            }

            Spawn(spec);
        }

        bool TryGetJsonText(out string json, out string sourceLabel)
        {
            json = null;
            sourceLabel = null;
            if (levelJson != null && !string.IsNullOrWhiteSpace(levelJson.text))
            {
                json = levelJson.text;
                sourceLabel = $"TextAsset '{levelJson.name}' (inspector)";
                return true;
            }

            if (string.IsNullOrWhiteSpace(resourcesLevelPath))
                return false;

            var asset = Resources.Load<TextAsset>(resourcesLevelPath);
            if (asset == null)
            {
                Debug.LogWarning(
                    $"[LevelBoardLoader] Resources.Load<TextAsset>(\"{resourcesLevelPath}\") returned null. " +
                    $"Expected file at Assets/Resources/{resourcesLevelPath}.json (or .txt).",
                    this);
                return false;
            }

            json = asset.text;
            sourceLabel = $"Resources/{resourcesLevelPath}";
            return !string.IsNullOrWhiteSpace(json);
        }

        void Spawn(LevelBoardSpec spec)
        {
            var cell = ResolveCellSize();
            for (var l = 0; l < spec.Depth; l++)
            // UI: later siblings draw on top — spawn bottom row (y = 0) last so it sorts above upper rows.
            for (var y = spec.Height - 1; y >= 0; y--)
            for (var x = 0; x < spec.Width; x++)
            {
                if (!spec.TryGet(x, y, l, out var kind)) continue;

                var view = Instantiate(tilePrefab, boardRoot);
                view.gameObject.name = $"Tile_L{l}_R{y}_C{x}";
                var pos = GridToAnchored(spec.Width, spec.Height, x, y, cell);
                view.Bind(kind, x, y, l, pos, cell, tileIconLibrary);
            }
        }

        Vector2 ResolveCellSize()
        {
            var w = cellWidth > 0f ? cellWidth : 100f;
            var h = cellHeight > 0f ? cellHeight : 100f;
            return new Vector2(w, h);
        }

        static Vector2 GridToAnchored(int width, int height, int x, int y, Vector2 cell)
        {
            var ox = (x + 0.5f - width * 0.5f) * cell.x;
            var oy = (y + 0.5f - height * 0.5f) * cell.y;
            return new Vector2(ox, oy);
        }
    }
}
