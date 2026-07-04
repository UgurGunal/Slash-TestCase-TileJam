using Core;
using UnityEngine;

namespace Presentation
{
    [CreateAssetMenu(fileName = "TileIconLibrary", menuName = "Tile Icon Library")]
    public sealed class TileIconLibrary : ScriptableObject
    {
        [Tooltip("Optional registry — when set, icons are resolved from catalog entries first.")]
        [SerializeField] TileCatalog tileCatalog;
        [Tooltip("Index 0 = Type0 … index 14 = Type14. Leave empty to use Resources fallback for that type.")]
        [SerializeField] Sprite[] iconsByType = new Sprite[GameConstants.PlayableTileKindCount];

        public TileCatalog Catalog => tileCatalog;

        public bool TryGetSprite(TileKind kind, out Sprite sprite)
        {
            sprite = null;
            if (kind == TileKind.None) return false;

            if (tileCatalog != null && tileCatalog.TryGetSprite(kind, out sprite))
                return true;

            var i = (int)kind;
            if (iconsByType == null || i < 0 || i >= iconsByType.Length) return false;
            sprite = iconsByType[i];
            return sprite != null;
        }

        public int ResolvedDefinitionCount =>
            tileCatalog != null && tileCatalog.Count > 0 ? tileCatalog.Count : GameConstants.PlayableTileKindCount;
    }
}
