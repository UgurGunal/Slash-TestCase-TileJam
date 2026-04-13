using Core;
using UnityEngine;

namespace Presentation
{
    [CreateAssetMenu(fileName = "TileIconLibrary", menuName = "Game/Tile Icon Library")]
    public sealed class TileIconLibrary : ScriptableObject
    {
        [Tooltip("Index 0 = Type0 … index 14 = Type14. Leave empty to use Resources fallback for that type.")]
        [SerializeField] Sprite[] iconsByType = new Sprite[GameConstants.PlayableTileKindCount];

        public bool TryGetSprite(TileKind kind, out Sprite sprite)
        {
            sprite = null;
            if (kind == TileKind.None) return false;
            var i = (int)kind;
            if (iconsByType == null || i < 0 || i >= iconsByType.Length) return false;
            sprite = iconsByType[i];
            return sprite != null;
        }
    }
}
