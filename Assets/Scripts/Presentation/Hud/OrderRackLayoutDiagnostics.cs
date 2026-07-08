using Gameplay;
using UnityEngine;

namespace Presentation.Hud
{
    /// <summary>Editor-only layout validation for order/rack HUD wiring.</summary>
    public static class OrderRackLayoutDiagnostics
    {
        public static void DiagnoseOrderVisibilityOnce(
            Object context,
            LevelObjectiveSession session,
            OrderPresenter[] orders,
            ref bool logged)
        {
            if (session == null || orders == null || logged) return;
            logged = true;

            var n = orders.Length;
            Debug.Log(
                $"[OrderRackHud] Gameplay uses {n} order HUD rows (UI order index 0…{n - 1}).",
                context);

            for (var s = 0; s < n; s++)
            {
                if (!session.GetActiveSlot(s, out var levelOrderIndex, out _, out _))
                    continue;

                var img = orders[s].FirstNonNullIcon();
                if (img == null) continue;

                if (!img.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning(
                        $"[OrderRackHud] Order row {s} (level order index {levelOrderIndex}) has an inactive icon GameObject.",
                        context);
                    continue;
                }

                for (var t = img.transform; t != null; t = t.parent)
                {
                    var cg = t.GetComponent<CanvasGroup>();
                    if (cg != null && cg.alpha <= 0.001f)
                    {
                        Debug.LogWarning(
                            $"[OrderRackHud] Order row {s} (level order index {levelOrderIndex}) is under a CanvasGroup with alpha≈0.",
                            context);
                        break;
                    }
                }

                var rect = img.rectTransform.rect;
                if (rect.width <= 0.01f || rect.height <= 0.01f)
                {
                    Debug.LogWarning(
                        $"[OrderRackHud] Order row {s} (level order index {levelOrderIndex}) icon Rect has zero size.",
                        context);
                }
            }
        }
    }
}
