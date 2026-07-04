using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    [CreateAssetMenu(fileName = "TileCatalog", menuName = "Tile Jam/Tile Catalog")]
    public sealed class TileCatalog : ScriptableObject
    {
        [SerializeField] List<TileDefinition> definitions = new List<TileDefinition>();

        public IReadOnlyList<TileDefinition> Definitions => definitions;

        public int Count => definitions != null ? definitions.Count : 0;

        public bool TryGetByLegacyKind(TileKind kind, out TileDefinition definition)
        {
            definition = null;
            if (definitions == null) return false;
            for (var i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (d != null && d.legacyKind == kind)
                {
                    definition = d;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetSprite(TileKind kind, out Sprite sprite)
        {
            sprite = null;
            if (!TryGetByLegacyKind(kind, out var def) || def.icon == null) return false;
            sprite = def.icon;
            return true;
        }
    }
}
