using System;
using System.Collections;
using DG.Tweening;
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

        [Header("Board timing")]
        [Tooltip(
            "Wait this long after level data is ready before spawning tiles (session/HUD bind immediately). " +
            "If > 0, any previous tiles are removed right away so clicks cannot use a stale board with the new session.")]
        [SerializeField] float boardInitializationDelaySec = 0.2f;

        [Header("Tile spawn intro")]
        [Tooltip("On load, scale tiles from 0 → 1 with DOTween; same layer starts together, next layer delayed by the stagger.")]
        [SerializeField] bool tileSpawnScaleIn = true;
        [SerializeField] float tileSpawnLayerStaggerSec = 0.2f;
        [SerializeField] float tileSpawnScaleDurationSec = 0.28f;
        [SerializeField] Ease tileSpawnScaleEase = Ease.OutBack;

        public LevelBoardSpec LastSpec { get; private set; }
        public LevelDefinition LastDefinition { get; private set; }
        public LevelObjectiveSession Session => _session;

        /// <summary>Invoked after each successful <see cref="Reload"/> when a new <see cref="LevelObjectiveSession"/> is created and bound.</summary>
        public event Action<LevelObjectiveSession> SessionAssigned;

        LevelBoardGrid _grid;
        BoardTileCollectCoordinator _collect;
        LevelObjectiveSession _session;
        int _boardBuildGeneration;

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

            _collect.CancelInFlightCollect();

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
            SessionAssigned?.Invoke(_session);
            Debug.Log($"[LevelBoardLoader] Loaded from {source}\n{LevelGridParser.BuildValidationReport(definition.Board)}");

            _boardBuildGeneration++;
            var buildId = _boardBuildGeneration;
            if (boardInitializationDelaySec > 0f)
            {
                _grid.TearDownTiles();
                if (clearExistingChildren && boardRoot != null)
                {
                    for (var i = boardRoot.childCount - 1; i >= 0; i--)
                        Destroy(boardRoot.GetChild(i).gameObject);
                }

                StartCoroutine(RunBoardBuildAfterDelay(definition.Board, buildId));
            }
            else
            {
                RunBoardBuild(definition.Board);
            }
        }

        IEnumerator RunBoardBuildAfterDelay(LevelBoardSpec boardSpec, int scheduledGeneration)
        {
            yield return new WaitForSeconds(boardInitializationDelaySec);
            if (scheduledGeneration != _boardBuildGeneration)
                yield break;
            RunBoardBuild(boardSpec);
        }

        void RunBoardBuild(LevelBoardSpec boardSpec)
        {
            if (clearExistingChildren && boardRoot != null)
            {
                for (var i = boardRoot.childCount - 1; i >= 0; i--)
                    Destroy(boardRoot.GetChild(i).gameObject);
            }

            var spawnIntro = tileSpawnScaleIn
                ? new TileSpawnIntroConfig
                {
                    Enabled = true,
                    LayerStaggerSeconds = tileSpawnLayerStaggerSec,
                    ScaleDurationSeconds = tileSpawnScaleDurationSec,
                    ScaleEase = tileSpawnScaleEase
                }
                : TileSpawnIntroConfig.Disabled;

            _grid.BuildFromSpec(boardSpec, tilePrefab, visualLayout, tileIconLibrary, OnTileClicked, spawnIntro);
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
