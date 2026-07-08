using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Maps domain <see cref="TileCollectDestination"/> values to HUD <see cref="RectTransform"/> targets.</summary>
    public sealed class HudDestinationLayout : IHudDestinationLayout
    {
        readonly IObjectiveHudState _hudState;
        readonly OrderPresenter[] _orders;
        readonly RackPresenter _rack;

        public HudDestinationLayout(
            IObjectiveHudState hudState,
            OrderPresenter[] orders,
            RackPresenter rack)
        {
            _hudState = hudState;
            _orders = orders;
            _rack = rack;
        }

        public bool TryResolve(TileCollectDestination destination, out RectTransform rect)
        {
            rect = null;
            if (destination.Kind == CollectTargetKind.RackSlot)
                return TryResolveRackSlot(destination.RackSlotIndex, out rect);

            return TryGetOrderIconRectTransform(
                destination.ActiveOrderSlotIndex,
                destination.OrderIconIndex,
                out rect);
        }

        public bool TryGetOrderIconRectTransform(int activeOrderSlot, int iconIdx, out RectTransform rect)
        {
            rect = null;
            if (_hudState == null || _orders == null) return false;
            if ((uint)activeOrderSlot >= (uint)_orders.Length) return false;
            return _orders[activeOrderSlot].TryGetIconRectTransform(iconIdx, out rect);
        }

        public bool TryGetRackSlotImage(int index, out Image img)
        {
            img = null;
            return _rack != null && _rack.TryGetRackSlotImage(index, out img);
        }

        bool TryResolveRackSlot(int idx, out RectTransform rect)
        {
            rect = null;
            if (_rack == null || !_rack.TryGetRackSlotImages(out var rackSlotImages) ||
                rackSlotImages == null || rackSlotImages.Length == 0)
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
