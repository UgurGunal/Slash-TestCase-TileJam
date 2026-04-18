using Gameplay;
using LevelData;
using UnityEngine;
using UnityEngine.Serialization;

namespace Presentation
{
    /// <summary>
    /// Level bootstrap: JSON → <see cref="LevelDefinition"/>, session + HUD binding, grid spawn.
    /// Tile clicks and fly/rack orchestration live in <see cref="BoardTileCollectCoordinator"/>.
    /// </summary>
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
        [Tooltip("Optional: DOTween fly-to-HUD feedback. If unassigned or disabled, collects instantly.")]
        [FormerlySerializedAs("collectFlyFeedback")]
        [SerializeField] TileCollectFly collectFly;
        [Tooltip("Log each tile click: order match vs rack, strip indices, and automatic rack→order matches.")]
        [SerializeField] bool logTileCollectFlow;

        public LevelBoardSpec LastSpec { get; private set; }
        public LevelDefinition LastDefinition { get; private set; }
        public LevelObjectiveSession Session => _session;

        LevelBoardGrid _grid;
        BoardTileCollectCoordinator _collect;
        LevelObjectiveSession _session;

        void Awake()
        {
            ResolveOptionalReferences();
            if (boardRoot == null)
            {
                Debug.LogError("[LevelBoardLoader] Assign boardRoot.", this);
                return;
            }

            _grid = new LevelBoardGrid(boardRoot);
            _collect = new BoardTileCollectCoordinator(_grid);
            _collect.SetPresentationRefs(collectFly, orderRackHud);

            if (loadOnAwake) Reload();
        }

        /// <summary>Wires <see cref="collectFly"/> / <see cref="orderRackHud"/> when left unassigned (same GameObject or under the board Canvas).</summary>
        void ResolveOptionalReferences()
        {
            if (collectFly == null)
                collectFly = GetComponent<TileCollectFly>();

            if (orderRackHud == null && boardRoot != null)
            {
                orderRackHud = boardRoot.GetComponentInParent<OrderRackHud>();
                if (orderRackHud == null)
                {
                    var canvas = boardRoot.GetComponentInParent<Canvas>();
                    if (canvas != null)
                        orderRackHud = canvas.GetComponentInChildren<OrderRackHud>(true);
                }
            }
        }

        [ContextMenu("Reload level from JSON")]
        public void Reload() => Reload(resourcesLevelPath);

        /// <summary>Load by Resources path (e.g. <c>Levels/level_02</c> for <c>Assets/Resources/Levels/level_02.json</c>).</summary>
        public void Reload(string resourcesPath)
        {
            if (!string.IsNullOrWhiteSpace(resourcesPath))
                resourcesLevelPath = resourcesPath.Trim();

            ResolveOptionalReferences();
            _collect?.SetPresentationRefs(collectFly, orderRackHud);

            if (boardRoot == null || tilePrefab == null)
            {
                Debug.LogError("[LevelBoardLoader] Assign boardRoot and tilePrefab.", this);
                return;
            }

            if (_grid == null || _collect == null)
            {
                _grid = new LevelBoardGrid(boardRoot);
                _collect = new BoardTileCollectCoordinator(_grid);
                _collect.SetPresentationRefs(collectFly, orderRackHud);
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
            _collect.BindSession(_session);
            Debug.Log($"[LevelBoardLoader] Loaded from {source}\n{LevelGridParser.BuildValidationReport(definition.Board)}");

            if (clearExistingChildren)
            {
                for (var i = boardRoot.childCount - 1; i >= 0; i--)
                    Destroy(boardRoot.GetChild(i).gameObject);
            }

            _grid.BuildFromSpec(definition.Board, tilePrefab, visualLayout, tileIconLibrary, OnTileClicked);
        }

        void OnTileClicked(BoardTileView view) => _collect?.HandleTileClicked(view);

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
    }
}
