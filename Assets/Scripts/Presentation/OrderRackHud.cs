using Core;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>Inspector binding: one row of icon <see cref="Image"/>s for a single active order strip.</summary>
    [System.Serializable]
    public sealed class OrderStripUi
    {
        [Tooltip("One Image per icon in this order, left → right (same order as the JSON row for this customer).")]
        public Image[] iconImages;
    }

    /// <summary>
    /// Shows each active order’s required icons (left → right) in <see cref="orderStrips"/>; collected icons are dimmed (any order within that strip).
    /// If a tile matches any still-needed icon on an active order it goes to that order; otherwise it goes to <see cref="rackSlotImages"/>.
    /// </summary>
    public sealed class OrderRackHud : MonoBehaviour
    {
        [SerializeField] TileIconLibrary iconLibrary;
        [Tooltip("One strip per active order slot. Each strip lists the full order sequence; the HUD updates as the player fills it.")]
        [SerializeField] OrderStripUi[] orderStrips;
        [Tooltip("Six Images: rack slot 0 = first wrong tile collected, then 1…5. Empty slots stay hidden.")]
        [SerializeField] Image[] rackSlotImages;

        LevelObjectiveSession _session;

        void OnEnable()
        {
            if (_session != null)
                Refresh();
        }

        void OnDestroy() => Unbind();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (orderStrips != null && orderStrips.Length != GameConstants.ActiveOrderSlotsCount)
                Debug.LogWarning(
                    $"[OrderRackHud] Expected {GameConstants.ActiveOrderSlotsCount} entries in order strips (GameConstants.ActiveOrderSlotsCount).",
                    this);
        }
#endif

        public void BindSession(LevelObjectiveSession session)
        {
            Unbind();
            _session = session;
            if (_session != null)
                _session.StateChanged += OnStateChanged;
            Refresh();
        }

        void Unbind()
        {
            if (_session != null)
                _session.StateChanged -= OnStateChanged;
            _session = null;
        }

        void OnStateChanged() => Refresh();

        void Refresh()
        {
            if (_session == null) return;

            var stride = _session.MaxOrderIconsOnLevel;
            var slotCount = GameConstants.ActiveOrderSlotsCount;
            for (var s = 0; s < slotCount; s++)
            {
                var cells = orderStrips != null && s < orderStrips.Length ? orderStrips[s].iconImages : null;
                for (var i = 0; i < stride; i++)
                {
                    var img = cells != null && i < cells.Length ? cells[i] : null;
                    if (img == null) continue;

                    if (!_session.GetActiveSlot(s, out _, out var order, out var fulfilled))
                    {
                        img.enabled = false;
                        continue;
                    }

                    if (i >= order.Length)
                    {
                        img.enabled = false;
                        continue;
                    }

                    img.enabled = true;
                    var kind = order.GetIcon(i);
                    var cellDone = fulfilled != null && i < fulfilled.Length && fulfilled[i];
                    ApplySprite(img, kind, cellDone);
                }
            }

            for (var i = 0; i < GameConstants.RackCapacity; i++)
            {
                var img = rackSlotImages != null && i < rackSlotImages.Length ? rackSlotImages[i] : null;
                if (img == null) continue;

                var slot = _session.GetRackSlot(i);
                if (!slot.HasValue)
                {
                    img.enabled = false;
                    continue;
                }

                img.enabled = true;
                ApplySprite(img, slot.Value, false);
            }
        }

        void ApplySprite(Image img, TileKind kind, bool fulfilledDim)
        {
            Sprite sprite = null;
            if (iconLibrary != null && iconLibrary.TryGetSprite(kind, out var fromLib))
                sprite = fromLib;
            if (sprite == null)
                sprite = Resources.Load<Sprite>($"{BoardTileView.TileIconsResourcesFolder}/{kind}");

            img.sprite = sprite;
            img.enabled = sprite != null;
            img.color = fulfilledDim ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
        }
    }
}
