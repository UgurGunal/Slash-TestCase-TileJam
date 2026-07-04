using System.Collections.Generic;

namespace LevelData.Board
{
    /// <summary>Resolves per-behavior clickability/removal hooks (empty until behaviors are registered).</summary>
    public static class TileBehaviorContributorProvider
    {
        public static IReadOnlyList<ITileClickabilityContributor> GetClickabilityContributors(string behaviorId) =>
            Empty<ITileClickabilityContributor>.List;

        public static IReadOnlyList<ITileRemovalPolicy> GetRemovalPolicies(string behaviorId) =>
            Empty<ITileRemovalPolicy>.List;

        static class Empty<T>
        {
            public static readonly IReadOnlyList<T> List = new T[0];
        }
    }
}
