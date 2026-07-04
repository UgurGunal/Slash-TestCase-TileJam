using System.Collections.Generic;
using System.Linq;
using Core;

namespace Gameplay.Collect
{
    /// <summary>Runs registered <see cref="ICollectHandler"/> instances by ascending <see cref="ICollectHandler.Priority"/>.</summary>
    public sealed class CollectPipeline
    {
        readonly List<ICollectHandler> _handlers;

        public CollectPipeline(IEnumerable<ICollectHandler> handlers)
        {
            _handlers = handlers != null
                ? handlers.OrderBy(h => h.Priority).ToList()
                : new List<ICollectHandler>();
        }

        public static CollectPipeline CreateDefault() =>
            new CollectPipeline(new[] { MatchOrRackCollectHandler.Instance });

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
