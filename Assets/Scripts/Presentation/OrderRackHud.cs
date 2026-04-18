using Core;
using DG.Tweening;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>Inspector binding: one row of icon <see cref="Image"/>s for a single active order strip.</summary>
    [System.Serializable]
    public sealed class OrderStripUi
    {
        [Tooltip("Parent of this customer row: scales down/up when the order completes. If unset, the first icon’s parent RectTransform is used.")]
        public RectTransform stripContainer;
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
        [Tooltip("Optional: instantiated as a child of each fulfilled order icon (bottom-right). Removed when the slot is not fulfilled or the strip advances.")]
        [SerializeField] GameObject matchedOrderTickPrefab;
        [Tooltip("Anchored position inset from the icon’s bottom-right corner (x ≤ 0 moves left, y ≥ 0 moves up).")]
        [SerializeField] Vector2 matchedTickAnchorOffset = new Vector2(-6f, 6f);
        [Tooltip("RGB multiplier for fulfilled order icons (1 = no dimming).")]
        [SerializeField] [Range(0.55f, 1f)] float fulfilledOrderIconTint = 0.78f;

        [Header("Order complete feedback")]
        [Tooltip("When a strip advances to the next customer, its container scales to 0 then back to normal with the new order.")]
        [SerializeField] bool orderStripCompleteScaleAnimation = true;
        [SerializeField] float orderStripCompleteScaleDownSec = 0.22f;
        [SerializeField] Ease orderStripCompleteScaleDownEase = Ease.InQuad;
        [SerializeField] float orderStripCompleteScaleUpSec = 0.28f;
        [SerializeField] Ease orderStripCompleteScaleUpEase = Ease.OutBack;

        const string MatchedTickChildName = "OrderMatchedTick";

        LevelObjectiveSession _session;
        bool _loggedLayoutMismatch;
        bool _loggedStripVisibilityHints;
        bool[] _stripSkipRefreshDuringRoll;
        Vector3[] _stripContainerBaseScales;

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

            if (matchedOrderTickPrefab != null && matchedOrderTickPrefab.GetComponent<RectTransform>() == null)
                Debug.LogWarning(
                    $"[OrderRackHud] {nameof(matchedOrderTickPrefab)} root must have a RectTransform (UI prefab).",
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
            {
                _session.StateChanged += OnStateChanged;
                _session.ActiveOrderStripAdvanced += OnActiveOrderStripAdvanced;
            }

            EnsureAnimScratch(GameConstants.ActiveOrderSlotsCount);
            CacheStripContainerBaseScales();
            Refresh();
        }

        void Unbind()
        {
            if (_session != null)
            {
                _session.StateChanged -= OnStateChanged;
                _session.ActiveOrderStripAdvanced -= OnActiveOrderStripAdvanced;
            }

            KillAllStripContainerTweens();
            _session = null;
        }

        void OnStateChanged() => Refresh();

        void EnsureAnimScratch(int slotCount)
        {
            if (_stripSkipRefreshDuringRoll == null || _stripSkipRefreshDuringRoll.Length != slotCount)
                _stripSkipRefreshDuringRoll = new bool[slotCount];
            if (_stripContainerBaseScales == null || _stripContainerBaseScales.Length != slotCount)
                _stripContainerBaseScales = new Vector3[slotCount];
        }

        void CacheStripContainerBaseScales()
        {
            var n = GameConstants.ActiveOrderSlotsCount;
            EnsureAnimScratch(n);
            for (var s = 0; s < n; s++)
            {
                var rt = ResolveStripContainer(s);
                _stripContainerBaseScales[s] = rt != null ? rt.localScale : Vector3.one;
            }
        }

        RectTransform ResolveStripContainer(int stripIndex)
        {
            if (orderStrips == null || stripIndex < 0 || stripIndex >= orderStrips.Length)
                return null;
            var row = orderStrips[stripIndex];
            if (row.stripContainer != null)
                return row.stripContainer;
            var first = FirstNonNullImage(row.iconImages);
            if (first == null)
                return null;
            var p = first.transform.parent;
            return p != null ? p as RectTransform : first.rectTransform;
        }

        void KillAllStripContainerTweens()
        {
            var n = GameConstants.ActiveOrderSlotsCount;
            for (var s = 0; s < n; s++)
            {
                var rt = ResolveStripContainer(s);
                rt?.DOKill(false);
            }
        }

        void OnActiveOrderStripAdvanced(int slot)
        {
            if (!orderStripCompleteScaleAnimation || _session == null)
                return;

            var rt = ResolveStripContainer(slot);
            if (rt == null)
                return;

            var n = GameConstants.ActiveOrderSlotsCount;
            EnsureAnimScratch(n);
            if ((uint)slot >= (uint)n)
                return;

            rt.DOKill(false);
            _stripSkipRefreshDuringRoll[slot] = false;
            _stripSkipRefreshDuringRoll[slot] = true;
            var baseScale = slot < _stripContainerBaseScales.Length ? _stripContainerBaseScales[slot] : Vector3.one;
            rt.localScale = baseScale;

            var stride = _session.MaxOrderIconsOnLevel;
            var down = Mathf.Max(0.02f, orderStripCompleteScaleDownSec);
            var up = Mathf.Max(0.02f, orderStripCompleteScaleUpSec);
            rt
                .DOScale(Vector3.zero, down)
                .SetEase(orderStripCompleteScaleDownEase)
                .OnComplete(() =>
                {
                    RefreshOrderStripFromSession(slot, stride);
                    _stripSkipRefreshDuringRoll[slot] = false;
                    rt.DOScale(baseScale, up).SetEase(orderStripCompleteScaleUpEase);
                });
        }

        void Refresh()
        {
            if (_session == null) return;

            var stride = _session.MaxOrderIconsOnLevel;
            var slotCount = GameConstants.ActiveOrderSlotsCount;
            EnsureAnimScratch(slotCount);
            for (var s = 0; s < slotCount; s++)
            {
                if (s < _stripSkipRefreshDuringRoll.Length && _stripSkipRefreshDuringRoll[s])
                    continue;
                RefreshOrderStripFromSession(s, stride);
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

        void RefreshOrderStripFromSession(int s, int stride)
        {
            if (_session == null) return;
            var cells = orderStrips != null && s < orderStrips.Length ? orderStrips[s].iconImages : null;
            for (var i = 0; i < stride; i++)
            {
                if (!_session.GetActiveSlot(s, out _, out var order, out var fulfilled))
                {
                    var imgIdle = cells != null && i < cells.Length ? cells[i] : null;
                    if (imgIdle != null)
                    {
                        ClearOrderMatchedTick(imgIdle);
                        imgIdle.enabled = false;
                    }

                    continue;
                }

                if (i >= order.Length)
                {
                    var imgPad = cells != null && i < cells.Length ? cells[i] : null;
                    if (imgPad != null)
                    {
                        ClearOrderMatchedTick(imgPad);
                        imgPad.enabled = false;
                    }

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
                UpdateOrderMatchedTick(img, cellDone && img.enabled);
            }
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
            img.color = fulfilledDim ? new Color(fulfilledOrderIconTint, fulfilledOrderIconTint, fulfilledOrderIconTint, 1f) : Color.white;
        }

        void UpdateOrderMatchedTick(Image icon, bool showMatched)
        {
            if (icon == null) return;
            if (!showMatched || matchedOrderTickPrefab == null)
            {
                ClearOrderMatchedTick(icon);
                return;
            }

            var parent = icon.rectTransform;
            var existing = parent.Find(MatchedTickChildName);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                LayoutMatchedTick(existing as RectTransform, parent);
                existing.SetAsLastSibling();
                return;
            }

            var tickGo = Instantiate(matchedOrderTickPrefab, parent);
            tickGo.name = MatchedTickChildName;
            if (!tickGo.TryGetComponent<RectTransform>(out var tickRt))
            {
                Destroy(tickGo);
                return;
            }

            LayoutMatchedTick(tickRt, parent);
            tickRt.SetAsLastSibling();
        }

        void LayoutMatchedTick(RectTransform tick, RectTransform iconRect)
        {
            if (tick == null) return;
            tick.anchorMin = new Vector2(1f, 0f);
            tick.anchorMax = new Vector2(1f, 0f);
            tick.pivot = new Vector2(1f, 0f);
            tick.anchoredPosition = matchedTickAnchorOffset;
            tick.localRotation = Quaternion.identity;
            tick.localScale = Vector3.one;
        }

        void ClearOrderMatchedTick(Image icon)
        {
            if (icon == null) return;
            var t = icon.rectTransform.Find(MatchedTickChildName);
            if (t != null)
                Destroy(t.gameObject);
        }

        /// <summary>Rack HUD image for packed slot index <paramref name="index"/> (0 = leftmost occupied).</summary>
        public bool TryGetRackSlotImage(int index, out Image img)
        {
            img = null;
            if (rackSlotImages == null || index < 0 || index >= rackSlotImages.Length) return false;
            img = rackSlotImages[index];
            return img != null;
        }

        /// <summary>Resolves the UI rect the board icon should fly to (order cell or next rack slot).</summary>
        public bool TryGetRectTransformForFlyTarget(TileCollectFlyTarget target, out RectTransform rect)
        {
            rect = null;
            if (_session == null) return false;

            if (target.GoesToRack)
            {
                var idx = target.RackSlotIndex;
                if (idx < 0 || rackSlotImages == null || rackSlotImages.Length == 0) return false;
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

            var strip = target.OrderStripIndex;
            var iconIdx = target.OrderIconIndex;
            if (!_session.GetActiveSlot(strip, out _, out var order, out _)) return false;
            if (iconIdx < 0 || iconIdx >= order.Length) return false;
            if (orderStrips == null || strip >= orderStrips.Length) return false;
            var cells = orderStrips[strip].iconImages;
            if (cells == null) return false;
            if (iconIdx < cells.Length && cells[iconIdx] != null)
            {
                rect = cells[iconIdx].rectTransform;
                return true;
            }

            for (var i = 0; i < order.Length && i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                rect = cells[i].rectTransform;
                return true;
            }

            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                rect = cells[i].rectTransform;
                return true;
            }

            return false;
        }
    }
}
