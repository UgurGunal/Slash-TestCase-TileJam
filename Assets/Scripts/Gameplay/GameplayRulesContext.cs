using System.Collections.Generic;
using Gameplay.Collect;
using LevelData.Board;

namespace Gameplay
{
    /// <summary>Injectable gameplay rule pipelines (clickability + collect) driven by a single behavior catalog.</summary>
    public sealed class GameplayRulesContext
    {
        public GameplayRulesContext(
            ClickabilityPipeline clickability,
            CollectPipeline collect,
            TileBehaviorCatalog behaviorCatalog = null)
        {
            Clickability = clickability;
            Collect = collect;
            BehaviorCatalog = behaviorCatalog ?? TileBehaviorCatalog.CreateDefault();
        }

        public ClickabilityPipeline Clickability { get; }
        public CollectPipeline Collect { get; }
        public TileBehaviorCatalog BehaviorCatalog { get; }

        public static GameplayRulesContext CreateDefault(TileBehaviorCatalog behaviorCatalog = null)
        {
            var catalog = behaviorCatalog ?? TileBehaviorCatalog.CreateDefault();
            return new GameplayRulesContext(
                ClickabilityPipeline.CreateDefault(catalog),
                CollectPipeline.CreateDefault(CollectContributorsFrom(catalog)),
                catalog);
        }

        public bool CanRemoveFromBoard(BoardCell cell) =>
            cell.HasTile && BehaviorCatalog.Resolve(cell.BehaviorId).CanRemoveFromBoard(cell);

        /// <summary>Behaviors that also opt into custom collect logic become collect contributors.</summary>
        static IEnumerable<ITileCollectContributor> CollectContributorsFrom(TileBehaviorCatalog catalog)
        {
            var contributors = new List<ITileCollectContributor>();
            foreach (var behavior in catalog.Behaviors)
            {
                if (behavior is ITileCollectContributor contributor)
                    contributors.Add(contributor);
            }

            return contributors;
        }
    }
}
