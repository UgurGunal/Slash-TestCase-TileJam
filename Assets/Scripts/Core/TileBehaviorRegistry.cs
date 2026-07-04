using System.Collections.Generic;
using UnityEngine;

namespace Core
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
    }
}
