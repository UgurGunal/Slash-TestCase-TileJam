using Gameplay;
using Presentation.Hud;
using UnityEngine;

namespace Presentation
{
    /// <summary>Maps domain <see cref="TileCollectDestination"/> values to HUD <see cref="RectTransform"/> targets.</summary>
    public sealed class CollectDestinationResolver
    {
        readonly IHudDestinationLayout _layout;
        readonly HudDestinationLayout _typedLayout;

        public CollectDestinationResolver(IHudDestinationLayout layout)
        {
            _layout = layout;
            _typedLayout = layout as HudDestinationLayout;
        }

        public bool TryResolve(TileCollectDestination destination, out RectTransform rect)
        {
            if (_typedLayout != null)
                return _typedLayout.TryResolve(destination, out rect);

            rect = null;
            if (_layout == null) return false;

            if (destination.Kind == CollectTargetKind.RackSlot)
            {
                if (!_layout.TryGetRackSlotImage(destination.RackSlotIndex, out var rackImg) || rackImg == null)
                    return false;
                rect = rackImg.rectTransform;
                return true;
            }

            return _layout.TryGetOrderIconRectTransform(
                destination.ActiveOrderSlotIndex,
                destination.OrderIconIndex,
                out rect);
        }
    }
}
