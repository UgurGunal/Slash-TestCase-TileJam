using Core;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Editor-only layout validation for order/rack HUD wiring.</summary>
    public static class OrderRackLayoutDiagnostics
    {
        public static void ValidateOnEditor(
            Object context,
            OrderStripUi[] orderStrips,
            Image[] rackSlotImages,
            GameObject matchedOrderTickPrefab)
        {
#if UNITY_EDITOR
            if (orderStrips != null && orderStrips.Length != GameConstants.ActiveOrderSlotsCount)
                Debug.LogWarning(
                    $"[OrderRackHud] Expected {GameConstants.ActiveOrderSlotsCount} entries in order strips (GameConstants.ActiveOrderSlotsCount).",
                    context);

            if (orderStrips != null)
            {
                for (var s = 0; s < GameConstants.ActiveOrderSlotsCount && s < orderStrips.Length; s++)
                {
                    var imgs = orderStrips[s].iconImages;
                    if (imgs == null || imgs.Length == 0)
                    {
                        Debug.LogWarning(
                            $"[OrderRackHud] orderStrips[{s}] has no {nameof(OrderStripUi.iconImages)}.",
                            context);
                        continue;
                    }

                    for (var i = 0; i < imgs.Length; i++)
                    {
                        if (imgs[i] != null) continue;
                        Debug.LogWarning(
                            $"[OrderRackHud] orderStrips[{s}].iconImages[{i}] is unassigned.",
                            context);
                        break;
                    }
                }
            }

            if (rackSlotImages != null && rackSlotImages.Length > 0 &&
                rackSlotImages.Length < GameConstants.RackCapacity)
                Debug.LogWarning(
                    $"[OrderRackHud] rackSlotImages has {rackSlotImages.Length} entries; assign {GameConstants.RackCapacity}.",
                    context);

            if (matchedOrderTickPrefab != null && matchedOrderTickPrefab.GetComponent<RectTransform>() == null)
                Debug.LogWarning(
                    $"[OrderRackHud] matchedOrderTickPrefab root must have a RectTransform.",
                    context);
#endif
        }

        public static void DiagnoseStripVisibilityOnce(
            Object context,
            LevelObjectiveSession session,
            OrderStripPresenter[] strips,
            ref bool logged)
        {
            if (session == null || strips == null || logged) return;
            logged = true;

            var n = GameConstants.ActiveOrderSlotsCount;
            Debug.Log(
                $"[OrderRackHud] Gameplay uses {n} order HUD rows (UI strip index 0…{n - 1}).",
                context);

            for (var s = 0; s < n && s < strips.Length; s++)
            {
                if (!session.GetActiveSlot(s, out var levelOrderIndex, out _, out _))
                    continue;

                var img = strips[s].FirstNonNullIcon();
                if (img == null) continue;

                if (!img.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning(
                        $"[OrderRackHud] UI strip {s} (level order index {levelOrderIndex}) has an inactive icon GameObject.",
                        context);
                    continue;
                }

                for (var t = img.transform; t != null; t = t.parent)
                {
                    var cg = t.GetComponent<CanvasGroup>();
                    if (cg != null && cg.alpha <= 0.001f)
                    {
                        Debug.LogWarning(
                            $"[OrderRackHud] UI strip {s} (level order index {levelOrderIndex}) is under a CanvasGroup with alpha≈0.",
                            context);
                        break;
                    }
                }

                var rect = img.rectTransform.rect;
                if (rect.width <= 0.01f || rect.height <= 0.01f)
                {
                    Debug.LogWarning(
                        $"[OrderRackHud] UI strip {s} (level order index {levelOrderIndex}) icon Rect has zero size.",
                        context);
                }
            }
        }
    }
}
