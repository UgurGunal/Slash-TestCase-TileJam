using System.Collections.Generic;
using Gameplay;
using LevelData;
using UnityEngine;

namespace Presentation
{
    /// <summary>Loads a JSON level grid and instantiates <see cref="BoardTileView"/> under <see cref="boardRoot"/>.</summary>
    public sealed class LevelBoardLoader : MonoBehaviour
    {
        [Header("Source (first wins: assigned TextAsset, else Resources path)")]
        [SerializeField] TextAsset levelJson;
        [Tooltip("Path relative to a Resources folder, without extension. Example: Levels/level_1 for Assets/Resources/Levels/level_1.json")]
        [SerializeField] string resourcesLevelPath = "Levels/level_1";

        [Header("Board")]
        [SerializeField] RectTransform boardRoot;
        [SerializeField] BoardTileView tilePrefab;
        [Tooltip("Optional: 15 sprites (Type0–Type14). Empty slots use Resources/TileIcons/{name}.")]
        [SerializeField] TileIconLibrary tileIconLibrary;

        [Header("Visual layout")]
        [Tooltip("Cell sizes and tile padding. If unset, loads Resources/" + LevelBoardVisualLayoutSettings.ResourcesLoadName + " when present, else built-in fallbacks.")]
        [SerializeField] LevelBoardVisualLayoutSettings visualLayout;

        [SerializeField] bool clearExistingChildren = true;
        [SerializeField] bool loadOnAwake = true;
        [SerializeField] OrderRackHud orderRackHud;
        [Tooltip("Log each tile click: order match vs rack, strip indices, and automatic rack→order matches.")]
        [SerializeField] bool logTileCollectFlow;

        public LevelBoardSpec LastSpec { get; private set; }
        public LevelDefinition LastDefinition { get; private set; }
        public LevelObjectiveSession Session => _session;

        PlayableBoardState _playState;
        LevelObjectiveSession _session;
        readonly Dictionary<(int x, int y, int layer), BoardTileView> _tiles = new Dictionary<(int x, int y, int layer), BoardTileView>();
        bool _tileCollectInFlight;

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

            if (!LevelGridParser.TryParseJson(json, out LevelDefinition definition, out var err))
            {
                Debug.LogError($"[LevelBoardLoader] {err}\nSource: {source}", this);
                return;
            }

            LastDefinition = definition;
            LastSpec = definition.Board;
            _session = new LevelObjectiveSession(definition.Orders) { LogCollectFlow = logTileCollectFlow };
            orderRackHud?.BindSession(_session);
            Debug.Log($"[LevelBoardLoader] Loaded from {source}\n{LevelGridParser.BuildValidationReport(definition.Board)}");

            if (clearExistingChildren)
            {
                for (var i = boardRoot.childCount - 1; i >= 0; i--)
                    Destroy(boardRoot.GetChild(i).gameObject);
            }

            Spawn(definition.Board);
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
            _tiles.Clear();
            _playState = new PlayableBoardState(spec);

            var layout = ResolveVisualLayout();
            var cell = LevelBoardVisualLayoutSettings.ResolveCellSize(layout);
            var tileScale = LevelBoardVisualLayoutSettings.ResolveTileSizeInCellScale(layout);
            for (var l = 0; l < spec.Depth; l++)
            // UI: later siblings draw on top — spawn bottom row (y = 0) last so it sorts above upper rows.
            for (var y = spec.Height - 1; y >= 0; y--)
            for (var x = 0; x < spec.Width; x++)
            {
                if (!spec.TryGet(x, y, l, out var kind)) continue;

                var view = Instantiate(tilePrefab, boardRoot);
                view.gameObject.name = $"Tile_L{l}_R{y}_C{x}";
                var pos = GridToAnchored(spec.Width, spec.Height, x, y, cell);
                view.Bind(kind, x, y, l, pos, cell, tileScale, tileIconLibrary);
                view.SetClickHandler(OnTileClicked);
                _tiles[(x, y, l)] = view;
            }

            RefreshTileClickabilityVisuals();
        }

        void OnTileClicked(BoardTileView view)
        {
            if (_tileCollectInFlight) return;
            if (_playState == null || view == null || _session == null) return;
            if (_session.HasFailed || _session.HasWon) return;

            var x = view.GridX;
            var y = view.GridY;
            var l = view.LayerIndex;
            if (!TileClickability.IsClickable(_playState, x, y, l)) return;

            _tileCollectInFlight = true;
            try
            {
                var result = _session.TryCollectTile(view.Kind);
                if (result == TileCollectResult.SessionInactive)
                {
                    RefreshTileClickabilityVisuals();
                    return;
                }

                if (result == TileCollectResult.FailedRackFull)
                {
                    Debug.LogWarning("[LevelBoardLoader] Rack full — level failed.");
                    RefreshTileClickabilityVisuals();
                    return;
                }

                if (result == TileCollectResult.LevelWon)
                    Debug.Log("[LevelBoardLoader] All orders completed — level won.");

                _playState.Clear(x, y, l);
                _tiles.Remove((x, y, l));
                Destroy(view.gameObject);
                RefreshTileClickabilityVisuals();
            }
            finally
            {
                _tileCollectInFlight = false;
            }
        }

        void RefreshTileClickabilityVisuals()
        {
            if (_playState == null) return;
            foreach (var kv in _tiles)
            {
                var (x, y, l) = kv.Key;
                var clickable = TileClickability.IsClickable(_playState, x, y, l);
                kv.Value.SetClickableVisual(clickable);
            }
        }

        LevelBoardVisualLayoutSettings ResolveVisualLayout() =>
            visualLayout != null
                ? visualLayout
                : Resources.Load<LevelBoardVisualLayoutSettings>(LevelBoardVisualLayoutSettings.ResourcesLoadName);

        static Vector2 GridToAnchored(int width, int height, int x, int y, Vector2 cell)
        {
            var ox = (x + 0.5f - width * 0.5f) * cell.x;
            var oy = (y + 0.5f - height * 0.5f) * cell.y;
            return new Vector2(ox, oy);
        }
    }
}
