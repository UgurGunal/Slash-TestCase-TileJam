using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Maps <see cref="IObjectiveHudState"/> rack slots onto rack slot images.</summary>
    public sealed class RackPresenter
    {
        readonly Image[] _rackSlotImages;
        readonly TileKindSpriteResolver _sprites;

        public RackPresenter(Image[] rackSlotImages, TileKindSpriteResolver sprites)
        {
            _rackSlotImages = rackSlotImages;
            _sprites = sprites;
        }

        public void Refresh(IObjectiveHudState state)
        {
            if (state == null) return;

            var count = _rackSlotImages?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                var img = _rackSlotImages[i];
                if (img == null) continue;

                var slot = state.GetRackSlot(i);
                if (!slot.HasValue)
                {
                    img.enabled = false;
                    continue;
                }

                var sprite = _sprites.Resolve(slot.Value);
                img.sprite = sprite;
                img.enabled = sprite != null;
                img.color = Color.white;
            }
        }

        public bool TryGetRackSlotImage(int index, out Image img)
        {
            img = null;
            if (_rackSlotImages == null || index < 0 || index >= _rackSlotImages.Length) return false;
            img = _rackSlotImages[index];
            return img != null;
        }

        public bool TryGetRackSlotImages(out Image[] images)
        {
            images = _rackSlotImages;
            return _rackSlotImages != null && _rackSlotImages.Length > 0;
        }
    }
}
