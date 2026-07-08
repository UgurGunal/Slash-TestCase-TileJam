using System.Collections.Generic;

namespace LevelData.Board
{
    /// <summary>
    /// Injectable registry of <see cref="ITileBehavior"/> keyed by id, with a standard fallback.
    /// This replaces the old static provider: behaviors are registered explicitly (composition root
    /// or <see cref="CreateDefault"/>), so adding a tile type does not touch the rule pipelines.
    /// </summary>
    public sealed class TileBehaviorCatalog
    {
        readonly Dictionary<string, ITileBehavior> _byId = new Dictionary<string, ITileBehavior>();
        readonly ITileBehavior _fallback;

        public TileBehaviorCatalog(IEnumerable<ITileBehavior> behaviors = null, ITileBehavior fallback = null)
        {
            _fallback = fallback ?? StandardTileBehavior.Instance;
            Register(_fallback);
            if (behaviors != null)
            {
                foreach (var behavior in behaviors)
                    Register(behavior);
            }
        }

        /// <summary>Standard tile only. Register extra behaviors via the ctor or <see cref="Register"/>.</summary>
        public static TileBehaviorCatalog CreateDefault() => new TileBehaviorCatalog();

        public void Register(ITileBehavior behavior)
        {
            if (behavior == null || string.IsNullOrWhiteSpace(behavior.Id)) return;
            _byId[behavior.Id] = behavior;
        }

        /// <summary>Behavior for <paramref name="behaviorId"/>, or the standard fallback when unknown.</summary>
        public ITileBehavior Resolve(string behaviorId) =>
            behaviorId != null && _byId.TryGetValue(behaviorId, out var behavior) ? behavior : _fallback;

        public IReadOnlyCollection<ITileBehavior> Behaviors => _byId.Values;
    }
}
