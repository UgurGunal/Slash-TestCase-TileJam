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
        bool _loggedLayoutMismatch;
        bool _loggedStripVisibilityHints;

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

            if (orderStrips != null)
            {
                for (var s = 0; s < GameConstants.ActiveOrderSlotsCount && s < orderStrips.Length; s++)
                {
                    var imgs = orderStrips[s].iconImages;
                    if (imgs == null || imgs.Length == 0)
                    {
                        Debug.LogWarning(
                            $"[OrderRackHud] orderStrips[{s}] has no {nameof(OrderStripUi.iconImages)}. " +
                            $"Each HUD row needs one Image reference per icon in that customer’s order (e.g. level_10 uses length 3 for every order → 3 Images on each strip).",
                            this);
                        continue;
                    }

                    for (var i = 0; i < imgs.Length; i++)
                    {
                        if (imgs[i] != null) continue;
                        Debug.LogWarning(
                            $"[OrderRackHud] orderStrips[{s}].iconImages[{i}] is unassigned. Drag the Image components for that row into the array.",
                            this);
                        break;
                    }
                }
            }

            if (rackSlotImages != null && rackSlotImages.Length > 0 &&
                rackSlotImages.Length < GameConstants.RackCapacity)
                Debug.LogWarning(
                    $"[OrderRackHud] {nameof(rackSlotImages)} has {rackSlotImages.Length} entries; assign {GameConstants.RackCapacity} so every rack tile is visible.",
                    this);
        }
#endif

        public void BindSession(LevelObjectiveSession session)
        {
            Unbind();
            _session = session;
            _loggedLayoutMismatch = false;
            _loggedStripVisibilityHints = false;
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
                    if (!_session.GetActiveSlot(s, out _, out var order, out var fulfilled))
                    {
                        var imgIdle = cells != null && i < cells.Length ? cells[i] : null;
                        if (imgIdle != null)
                            imgIdle.enabled = false;
                        continue;
                    }

                    if (i >= order.Length)
                    {
                        var imgPad = cells != null && i < cells.Length ? cells[i] : null;
                        if (imgPad != null)
                            imgPad.enabled = false;
                        continue;
                    }

                    var img = cells != null && i < cells.Length ? cells[i] : null;
                    if (img == null)
                    {
                        LogLayoutMismatchOnce(
                            $"Order strip {s} is missing an Image for icon index {i} (order length {order.Length}). " +
                            $"Assign at least {stride} entries in that strip’s {nameof(OrderStripUi.iconImages)} (level max icons = {stride}).");
                        continue;
                    }

                    img.enabled = true;
                    var kind = order.GetIcon(i);
                    var cellDone = fulfilled != null && i < fulfilled.Length && fulfilled[i];
                    ApplySprite(img, kind, cellDone);
                }
            }

            if (rackSlotImages == null || rackSlotImages.Length < GameConstants.RackCapacity)
                LogLayoutMismatchOnce(
                    $"Assign {GameConstants.RackCapacity} {nameof(rackSlotImages)} entries so every rack slot can be shown.");

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

            DiagnoseStripVisibilityOnce();
        }

        /// <summary>
        /// Warn once if an active order strip is inactive, alpha-zero, or has no drawable size — common cause of
        /// “tile vanished” when gameplay still matches <see cref="GameConstants.ActiveOrderSlotsCount"/> HUD rows.
        /// </summary>
        void DiagnoseStripVisibilityOnce()
        {
            if (_session == null || orderStrips == null || _loggedStripVisibilityHints) return;
            _loggedStripVisibilityHints = true;

            var n = GameConstants.ActiveOrderSlotsCount;
            Debug.Log(
                $"[OrderRackHud] Gameplay uses {n} order HUD rows (UI strip index 0…{n - 1}). " +
                $"Each row must be visible on the Canvas; a missing row looks like tiles vanish when they match that strip.",
                this);

            for (var s = 0; s < n; s++)
            {
                if (!_session.GetActiveSlot(s, out var levelOrderIndex, out _, out _))
                    continue;

                var cells = s < orderStrips.Length ? orderStrips[s].iconImages : null;
                var img = FirstNonNullImage(cells);
                if (img == null)
                    continue;

                if (!img.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning(
                        $"[OrderRackHud] UI strip {s} (level order index {levelOrderIndex}) has an inactive icon GameObject — " +
                        $"players cannot see this row; tiles can still match it.",
                        this);
                    continue;
                }

                for (var t = img.transform; t != null; t = t.parent)
                {
                    var cg = t.GetComponent<CanvasGroup>();
                    if (cg != null && cg.alpha <= 0.001f)
                    {
                        Debug.LogWarning(
                            $"[OrderRackHud] UI strip {s} (level order index {levelOrderIndex}) is under a CanvasGroup with alpha≈0 — " +
                            $"this row is invisible but still active in gameplay.",
                            this);
                        break;
                    }
                }

                var rect = img.rectTransform.rect;
                if (rect.width <= 0.01f || rect.height <= 0.01f)
                {
                    Debug.LogWarning(
                        $"[OrderRackHud] UI strip {s} (level order index {levelOrderIndex}) icon Rect has zero size — " +
                        $"check layout / anchors for that strip.",
                        this);
                }
            }
        }

        static Image FirstNonNullImage(Image[] cells)
        {
            if (cells == null) return null;
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null)
                    return cells[i];
            }

            return null;
        }

        void LogLayoutMismatchOnce(string message)
        {
            if (_loggedLayoutMismatch) return;
            _loggedLayoutMismatch = true;
            Debug.LogError($"[OrderRackHud] {message}\nGameplay still uses the full order from level data; tiles can match icons you do not see until this is fixed.", this);
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
