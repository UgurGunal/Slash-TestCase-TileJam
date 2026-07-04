using System;
using System.Collections;
using System.Text.RegularExpressions;
using DG.Tweening;
using Gameplay;
using LevelData;
using UnityEngine;
using UnityEngine.Serialization;

namespace Presentation
{
    public sealed class LevelBoardLoader : MonoBehaviour
    {
        [Header("Source (first wins: assigned TextAsset, else Resources path)")]
        [SerializeField] TextAsset levelJson;
        [SerializeField] string resourcesLevelPath = "Levels/level_1";
        [SerializeField] string numberedLevelsResourcesFolder = "Levels";

        [Header("Board")]
        [SerializeField] RectTransform boardRoot;
        [SerializeField] BoardTileView tilePrefab;
        [SerializeField] TileIconLibrary tileIconLibrary;
        [SerializeField] LevelBoardVisualLayoutSettings visualLayout;

        [SerializeField] bool clearExistingChildren = true;
        [SerializeField] bool loadOnAwake = true;
        [SerializeField] OrderRackHud orderRackHud;
        [FormerlySerializedAs("collectFlyFeedback")]
        [SerializeField] TileCollectFly collectFly;
        [SerializeField] bool logTileCollectFlow;

        [Header("Board timing")]
        [SerializeField] float boardInitializationDelaySec = 0.2f;

        [Header("Tile spawn intro")]
        [SerializeField] bool tileSpawnScaleIn = true;
        [SerializeField] float tileSpawnLayerStaggerSec = 0.2f;
        [SerializeField] float tileSpawnScaleDurationSec = 0.28f;
        [SerializeField] Ease tileSpawnScaleEase = Ease.OutBack;

        public LevelBoardSpec LastSpec { get; private set; }
        public LevelDefinition LastDefinition { get; private set; }
        public LevelObjectiveSession Session => _session;
        public IGameplayEventBus GameplayEventBus => _gameplayEventBus;
        public OrderRackHud OrderRackHud => orderRackHud;
        public TileCollectFly CollectFly => collectFly;
        public int CurrentLevelNumber { get; private set; } = 1;
        public bool UsesNumberedResourcesLevels => levelJson == null;

        public event Action<LevelObjectiveSession> SessionAssigned;

        LevelBoardGrid _grid;
        BoardTileCollectCoordinator _collect;
        LevelObjectiveSession _session;
        IGameplayEventBus _gameplayEventBus;
        bool _initialized;
        int _boardBuildGeneration;

        public void Initialize(IGameplayEventBus eventBus, GameplayRulesContext rulesContext = null)
        {
            if (eventBus == null)
            {
                Debug.LogError("[LevelBoardLoader] Initialize requires a gameplay event bus.", this);
                return;
            }

            _gameplayEventBus = eventBus;
            _rulesContext = rulesContext ?? GameplayRulesContext.CreateDefault();
            _initialized = true;

            if (!ValidatePresentationReferences())
                return;

            if (boardRoot == null)
            {
                Debug.LogError("[LevelBoardLoader] Assign boardRoot.", this);
                return;
            }

            if (_grid == null || _collect == null)
            {
                _grid = new LevelBoardGrid(boardRoot);
                _collect = new BoardTileCollectCoordinator(_grid);
            }

            _grid.SetClickabilityPipeline(_rulesContext.Clickability);
            _collect.SetGameplayRules(_rulesContext);
            _collect.SetPresentationRefs(collectFly, orderRackHud);

            if (loadOnAwake)
                Reload();
        }

        GameplayRulesContext _rulesContext;

        bool ValidatePresentationReferences()
        {
            var ok = true;
            if (orderRackHud == null)
            {
                Debug.LogError("[LevelBoardLoader] Assign orderRackHud in the Inspector.", this);
                ok = false;
            }

            if (collectFly == null)
            {
                Debug.LogError("[LevelBoardLoader] Assign collectFly in the Inspector.", this);
                ok = false;
            }

            return ok;
        }

        [ContextMenu("Reload level from JSON")]
        public void Reload() => Reload(resourcesLevelPath);

        public bool TryLoadNextLevel()
        {
            if (!_initialized)
            {
                Debug.LogError("[LevelBoardLoader] Not initialized — assign GameCompositionRoot.", this);
                return false;
            }

            if (levelJson != null)
            {
                Debug.LogWarning("[LevelBoardLoader] Clear level TextAsset to advance numbered Resources levels.", this);
                return false;
            }

            var next = CurrentLevelNumber + 1;
            var path = BuildNumberedLevelResourcesPathForIndex(next);
            if (Resources.Load<TextAsset>(path) == null)
            {
                Debug.LogWarning(
                    $"[LevelBoardLoader] No next level: Resources.Load(\"{path}\") is null (add Assets/Resources/{path}.json).",
                    this);
                return false;
            }

            Reload(path);
            return true;
        }

        public string BuildNumberedLevelResourcesPathForIndex(int levelIndexOneBased)
        {
            var n = Mathf.Max(1, levelIndexOneBased);
            var folder = ResolveNumberedLevelsFolderPrefix();
            return $"{folder}/level_{n}";
        }

        string ResolveNumberedLevelsFolderPrefix()
        {
            if (TryExtractFolderFromResourcesPath(resourcesLevelPath, out var fromPath))
                return fromPath;
            var fb = string.IsNullOrWhiteSpace(numberedLevelsResourcesFolder)
                ? "Levels"
                : numberedLevelsResourcesFolder.Trim().Trim('/');
            return fb;
        }

        static bool TryExtractFolderFromResourcesPath(string path, out string folder)
        {
            folder = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            var i = path.LastIndexOf('/');
            if (i <= 0) return false;
            folder = path.Substring(0, i);
            return true;
        }

        public void Reload(string resourcesPath)
        {
            if (!_initialized)
            {
                Debug.LogError("[LevelBoardLoader] Reload before Initialize — wire GameCompositionRoot.", this);
                return;
            }

            if (!ValidatePresentationReferences())
                return;

            if (!string.IsNullOrWhiteSpace(resourcesPath))
                resourcesLevelPath = resourcesPath.Trim();

            if (boardRoot == null || tilePrefab == null)
            {
                Debug.LogError("[LevelBoardLoader] Assign boardRoot and tilePrefab.", this);
                return;
            }

            if (_grid == null || _collect == null)
            {
                _grid = new LevelBoardGrid(boardRoot);
                _collect = new BoardTileCollectCoordinator(_grid);
            }

            _grid.SetClickabilityPipeline(_rulesContext.Clickability);
            _collect.SetGameplayRules(_rulesContext);
            _collect.SetPresentationRefs(collectFly, orderRackHud);
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
            if (levelJson == null && TryParseLevelNumberFromResourcesPath(resourcesLevelPath, out var parsedLevel))
                CurrentLevelNumber = parsedLevel;

            _session = new LevelObjectiveSession(definition.Orders, _gameplayEventBus, _rulesContext.Collect)
            {
                CollectFlowLogger = new UnityCollectFlowLogger { IsEnabled = logTileCollectFlow }
            };
            orderRackHud.BindSession(_session, _gameplayEventBus);
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

        static bool TryParseLevelNumberFromResourcesPath(string path, out int level)
        {
            level = 1;
            if (string.IsNullOrWhiteSpace(path))
                return false;
            var m = Regex.Match(path, @"level_(\d+)\s*$", RegexOptions.IgnoreCase);
            if (!m.Success)
                return false;
            return int.TryParse(m.Groups[1].Value, out level);
        }
    }
}
