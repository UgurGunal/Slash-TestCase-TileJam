using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    [CreateAssetMenu(fileName = "TileBehaviorDefinition", menuName = "Tile Jam/Tile Behavior Definition")]
    public sealed class TileBehaviorDefinition : ScriptableObject
    {
        [Tooltip("Stable id referenced by BoardCell.BehaviorId (e.g. standard, locked, ice).")]
        public string id = "standard";
    }
}
