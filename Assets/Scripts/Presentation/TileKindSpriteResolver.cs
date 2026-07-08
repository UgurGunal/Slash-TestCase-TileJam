using Core;
using UnityEngine;

namespace Presentation
{
    /// <summary>Resolves <see cref="TileKind"/> sprites for HUD and board views.</summary>
    public sealed class TileKindSpriteResolver
    {
        readonly TileIconLibrary _iconLibrary;

        public TileKindSpriteResolver(TileIconLibrary iconLibrary) => _iconLibrary = iconLibrary;

        public Sprite Resolve(TileKind kind)
        {
            if (_iconLibrary != null && _iconLibrary.TryGetSprite(kind, out var fromLib) && fromLib != null)
                return fromLib;

            return Resources.Load<Sprite>($"{BoardTileView.TileIconsResourcesFolder}/{kind}");
        }
    }
}
