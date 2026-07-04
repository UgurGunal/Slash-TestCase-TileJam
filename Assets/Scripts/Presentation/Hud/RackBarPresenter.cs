using Core;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Rack slot image refresh for collected non-matching tiles.</summary>
    public sealed class RackBarPresenter
    {
        readonly Image[] _rackSlotImages;
        readonly TileIconLibrary _iconLibrary;

        public RackBarPresenter(Image[] rackSlotImages, TileIconLibrary iconLibrary)
        {
            _rackSlotImages = rackSlotImages;
            _iconLibrary = iconLibrary;
        }

        public void Refresh(LevelObjectiveSession session)
        {
            if (session == null) return;

            for (var i = 0; i < GameConstants.RackCapacity; i++)
            {
                var img = i < _rackSlotImages?.Length ? _rackSlotImages[i] : null;
                if (img == null) continue;

                var slot = session.GetRackSlot(i);
                if (!slot.HasValue)
                {
                    img.enabled = false;
                    continue;
                }

                img.enabled = true;
                ApplySprite(img, slot.Value);
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

        void ApplySprite(Image img, TileKind kind)
        {
            Sprite sprite = null;
            if (_iconLibrary != null && _iconLibrary.TryGetSprite(kind, out var fromLib))
                sprite = fromLib;
            if (sprite == null)
                sprite = Resources.Load<Sprite>($"{BoardTileView.TileIconsResourcesFolder}/{kind}");

            img.sprite = sprite;
            img.enabled = sprite != null;
            img.color = Color.white;
        }
    }
}
