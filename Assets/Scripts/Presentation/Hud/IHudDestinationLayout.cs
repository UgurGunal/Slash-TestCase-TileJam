using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Rect resolution for collect fly targets (order icons and rack slots).</summary>
    public interface IHudDestinationLayout
    {
        bool TryGetOrderIconRectTransform(int activeOrderSlot, int iconIdx, out RectTransform rect);
        bool TryGetRackSlotImage(int index, out Image img);
        bool TryGetRackSlotImages(out Image[] images);
    }
}
