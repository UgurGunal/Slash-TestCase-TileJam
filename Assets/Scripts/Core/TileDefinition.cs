using Core;
using UnityEngine;

namespace Core
{
    [CreateAssetMenu(fileName = "TileDefinition", menuName = "Tile Jam/Tile Definition")]
    public sealed class TileDefinition : ScriptableObject
    {
        [Tooltip("Stable id for future JSON (e.g. type_0, wildcard).")]
        public string id;
        public TileKind legacyKind;
        public Sprite icon;
    }
}
