using System.Collections.Generic;
using Core;
using UnityEngine;

namespace LevelData.Board
{
    [CreateAssetMenu(fileName = "TileBehaviorRegistry", menuName = "Tile Jam/Tile Behavior Registry")]
    public sealed class TileBehaviorRegistry : ScriptableObject
    {
        [SerializeField] List<TileBehaviorDefinition> definitions = new List<TileBehaviorDefinition>();

        public IReadOnlyList<TileBehaviorDefinition> Definitions => definitions;

        public bool TryGet(string behaviorId, out TileBehaviorDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(behaviorId) || definitions == null) return false;
            for (var i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (d != null && d.id == behaviorId)
                {
                    definition = d;
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<ITileClickabilityContributor> GetClickabilityContributors(string behaviorId) =>
            Empty<ITileClickabilityContributor>.List;

        public IReadOnlyList<ITileRemovalPolicy> GetRemovalPolicies(string behaviorId) =>
            Empty<ITileRemovalPolicy>.List;

        static class Empty<T>
        {
            public static readonly IReadOnlyList<T> List = new T[0];
        }
    }
}
