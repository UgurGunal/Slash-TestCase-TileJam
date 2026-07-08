using Gameplay;
using Core;
using LevelData.Board;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Scene composition root: owns the gameplay event bus and wires explicit serialized references.
    /// Runs before <see cref="LevelBoardLoader"/> via execution order.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        [SerializeField] LevelBoardLoader boardLoader;
        [SerializeField] GameFlowManager gameFlowManager;
        [SerializeField] OrderRackHud orderRackHud;
        [SerializeField] TileCollectFly collectFly;
        [Tooltip("Optional visual metadata (overlay sprite + tint) per behavior id. Logic behaviors are registered in code below.")]
        [SerializeField] TileBehaviorRegistry tileBehaviorRegistry;

        readonly GameplayEventBus _eventBus = new GameplayEventBus();
        GameplayRulesContext _rulesContext;

        public IGameplayEventBus EventBus => _eventBus;
        public GameplayRulesContext RulesContext => _rulesContext;

        void Awake()
        {
            if (!ValidateReferences())
                return;

            var behaviorCatalog = BuildBehaviorCatalog();
            _rulesContext = GameplayRulesContext.CreateDefault(behaviorCatalog);
            boardLoader.Initialize(_eventBus, _rulesContext, tileBehaviorRegistry);
        }

        /// <summary>
        /// The single place tile behaviors are registered. Only the standard tile ships today.
        /// Add a new tile type by writing one <see cref="ITileBehavior"/> class and dropping it into
        /// the array below (e.g. <c>new ITileBehavior[] { new IceTileBehavior() }</c>) — no rule
        /// pipeline or core flow changes are needed.
        /// </summary>
        static TileBehaviorCatalog BuildBehaviorCatalog() =>
            new TileBehaviorCatalog(new ITileBehavior[] { });

        bool ValidateReferences()
        {
            var ok = true;
            if (boardLoader == null)
            {
                Debug.LogError("[GameCompositionRoot] Assign boardLoader.", this);
                ok = false;
            }

            if (gameFlowManager == null)
            {
                Debug.LogError("[GameCompositionRoot] Assign gameFlowManager.", this);
                ok = false;
            }

            if (orderRackHud == null)
            {
                Debug.LogError("[GameCompositionRoot] Assign orderRackHud.", this);
                ok = false;
            }

            if (collectFly == null)
            {
                Debug.LogError("[GameCompositionRoot] Assign collectFly.", this);
                ok = false;
            }

            if (boardLoader != null && orderRackHud != null && boardLoader.OrderRackHud != orderRackHud)
            {
                Debug.LogError("[GameCompositionRoot] boardLoader.orderRackHud must match orderRackHud reference.", this);
                ok = false;
            }

            if (boardLoader != null && collectFly != null && boardLoader.CollectFly != collectFly)
            {
                Debug.LogError("[GameCompositionRoot] boardLoader.collectFly must match collectFly reference.", this);
                ok = false;
            }

            if (gameFlowManager != null && boardLoader != null && gameFlowManager.BoardLoader != boardLoader)
            {
                Debug.LogError("[GameCompositionRoot] gameFlowManager.boardLoader must match boardLoader reference.", this);
                ok = false;
            }

            return ok;
        }
    }
}
