using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>
    /// The rack: exposes the slot container and the slot images.
    /// Slots are spawned by <see cref="OrderRackHudBuilder"/>, which calls <see cref="SetSlots"/>.
    /// </summary>
    public sealed class RackView : MonoBehaviour
    {
        [SerializeField] RectTransform slotContainer;

        RackSlotView[] _slots;

        public RectTransform SlotContainer => slotContainer != null ? slotContainer : transform as RectTransform;

        public void SetSlots(RackSlotView[] slots) => _slots = slots;

        public Image[] RackSlotImages
        {
            get
            {
                if (_slots == null || _slots.Length == 0)
                    return System.Array.Empty<Image>();

                var images = new Image[_slots.Length];
                for (var i = 0; i < _slots.Length; i++)
                    images[i] = _slots[i] != null ? _slots[i].IconImage : null;
                return images;
            }
        }
    }
}
