using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Maps domain <see cref="TileCollectDestination"/> values to HUD <see cref="RectTransform"/> targets.</summary>
    public sealed class HudDestinationLayout : IHudDestinationLayout
    {
        readonly LevelObjectiveSession _session;
        readonly OrderStripPresenter[] _strips;
        readonly RackBarPresenter _rack;

        public HudDestinationLayout(
            LevelObjectiveSession session,
            OrderStripPresenter[] strips,
            RackBarPresenter rack)
        {
            _session = session;
            _strips = strips;
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
            if (_session == null || _strips == null) return false;
            if ((uint)activeOrderSlot >= (uint)_strips.Length) return false;
            return _strips[activeOrderSlot].TryGetIconRectTransform(iconIdx, out rect);
        }

        public bool TryGetRackSlotImage(int index, out Image img)
        {
            img = null;
            return _rack != null && _rack.TryGetRackSlotImage(index, out img);
        }

        public bool TryGetRackSlotImages(out Image[] images)
        {
            images = null;
            return _rack != null && _rack.TryGetRackSlotImages(out images);
        }

        bool TryResolveRackSlot(int idx, out RectTransform rect)
        {
            rect = null;
            if (!TryGetRackSlotImages(out var rackSlotImages) || rackSlotImages == null || rackSlotImages.Length == 0)
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
