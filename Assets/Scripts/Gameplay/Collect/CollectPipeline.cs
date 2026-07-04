using System.Collections.Generic;
using System.Linq;
using Core;
using LevelData.Board;

namespace Gameplay.Collect
{
    /// <summary>Runs registered <see cref="ICollectHandler"/> instances by ascending <see cref="ICollectHandler.Priority"/>.</summary>
    public sealed class CollectPipeline
    {
        readonly List<ICollectHandler> _handlers;
        readonly IReadOnlyList<ITileCollectContributor> _behaviorContributors;

        public CollectPipeline(
            IEnumerable<ICollectHandler> handlers,
            IEnumerable<ITileCollectContributor> behaviorContributors = null)
        {
            _handlers = handlers != null
                ? handlers.OrderBy(h => h.Priority).ToList()
                : new List<ICollectHandler>();
            _behaviorContributors = behaviorContributors != null
                ? behaviorContributors.Where(c => c != null).ToList()
                : new ITileCollectContributor[0];
        }

        public static CollectPipeline CreateDefault(IEnumerable<ITileCollectContributor> behaviorContributors = null) =>
            new CollectPipeline(new[] { MatchOrRackCollectHandler.Instance }, behaviorContributors);

        public TileCollectResult Execute(BoardCell cell, CollectSessionContext context)
        {
            if (!cell.HasTile)
                return TileCollectResult.SessionInactive;

            for (var i = 0; i < _behaviorContributors.Count; i++)
            {
                var contributor = _behaviorContributors[i];
                if (contributor.TryHandleCollect(cell, context, out var contributorResult))
                    return contributorResult;
            }

            return Execute(cell.Kind.Value, context);
        }

        public TileCollectResult Execute(TileKind kind, CollectSessionContext context)
        {
            for (var i = 0; i < _handlers.Count; i++)
            {
                var handler = _handlers[i];
                if (handler == null || !handler.CanHandle(kind, context)) continue;
                return handler.Handle(kind, context);
            }

            return TileCollectResult.SessionInactive;
        }
    }
}
