using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>Maps domain <see cref="TileCollectDestination"/> values to HUD <see cref="RectTransform"/> targets.</summary>
    public sealed class CollectDestinationResolver
    {
        readonly OrderRackHud _hud;

        public CollectDestinationResolver(OrderRackHud hud) =>
            _hud = hud;

        public bool TryResolve(TileCollectDestination destination, out RectTransform rect)
        {
            rect = null;
            if (_hud == null) return false;

            if (destination.Kind == CollectTargetKind.RackSlot)
                return TryResolveRackSlot(destination.RackSlotIndex, out rect);

            return _hud.TryGetOrderIconRectTransform(
                destination.ActiveOrderSlotIndex,
                destination.OrderIconIndex,
                out rect);
        }

        bool TryResolveRackSlot(int idx, out RectTransform rect)
        {
            rect = null;
            if (idx < 0 || !_hud.TryGetRackSlotImages(out var rackSlotImages) || rackSlotImages == null || rackSlotImages.Length == 0)
                return false;

            if ((uint)idx < (uint)rackSlotImages.Length && rackSlotImages[idx] != null)
            {
                rect = rackSlotImages[idx].rectTransform;
                return true;
            }

            for (var i = idx; i < rackSlotImages.Length; i++)
            {
                if (rackSlotImages[i] != null)
                {
                    rect = rackSlotImages[i].rectTransform;
                    return true;
                }
            }

            for (var i = 0; i < idx && i < rackSlotImages.Length; i++)
            {
                if (rackSlotImages[i] != null)
                {
                    rect = rackSlotImages[i].rectTransform;
                    return true;
                }
            }

            return false;
        }
    }
}
