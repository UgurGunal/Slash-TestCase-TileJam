using Core;
using Gameplay.Collect;
using LevelData.Board;

namespace Gameplay
{
    /// <summary>Injectable gameplay rule pipelines (clickability + collect).</summary>
    public sealed class GameplayRulesContext
    {
        public GameplayRulesContext(
            ClickabilityPipeline clickability,
            CollectPipeline collect,
            TileBehaviorRegistry behaviorRegistry = null)
        {
            Clickability = clickability;
            Collect = collect;
            BehaviorRegistry = behaviorRegistry;
        }

        public ClickabilityPipeline Clickability { get; }
        public CollectPipeline Collect { get; }
        public TileBehaviorRegistry BehaviorRegistry { get; }

        public static GameplayRulesContext CreateDefault(TileBehaviorRegistry behaviorRegistry = null) =>
            new GameplayRulesContext(
                ClickabilityPipeline.CreateDefault(),
                CollectPipeline.CreateDefault(),
                behaviorRegistry);

        public bool CanRemoveFromBoard(BoardCell cell)
        {
            if (!cell.HasTile)
                return false;

            var policies = TileBehaviorContributorProvider.GetRemovalPolicies(cell.BehaviorId);
            for (var i = 0; i < policies.Count; i++)
            {
                var policy = policies[i];
                if (policy != null && !policy.CanRemoveFromBoard(cell))
                    return false;
            }

            return true;
        }
    }
}
