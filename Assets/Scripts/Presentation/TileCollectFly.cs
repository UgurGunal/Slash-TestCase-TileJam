using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// DOTween feedback: on collect, hide the tile background, fly the icon to a HUD target, then run <paramref name="onArrived"/>.
    /// Add alongside <see cref="LevelBoardLoader"/>; assign references in the Inspector.
    /// </summary>
    public sealed class TileCollectFly : MonoBehaviour
    {
        [Tooltip("Fly the tile icon to the order cell or rack slot; session should apply in onArrived.")]
        [SerializeField] bool useAnimation = true;
        [Tooltip("Parent for the icon during flight. If unset, uses the Canvas that contains boardRoot passed into Play.")]
        [SerializeField] RectTransform iconFlyOverlayParent;
        [SerializeField] float duration = 0.38f;
        [Tooltip("e.g. OutQuad = fast start, slow end")]
        [SerializeField] Ease ease = Ease.OutQuad;

        public bool UseAnimation => useAnimation;

        /// <summary>Call before mutating board state; ensures overlay and icon exist so <see cref="Play"/> will succeed.</summary>
        public bool WillAnimate(BoardTileView view, RectTransform flyTarget, RectTransform boardRoot)
        {
            if (!useAnimation || view == null || flyTarget == null || boardRoot == null)
                return false;
            if (view.IconRectTransform == null)
                return false;
            return ResolveOverlayParent(boardRoot) != null;
        }

        /// <summary>For rack→order: duplicate a HUD <see cref="RectTransform"/> (e.g. rack slot icon) and fly the copy.</summary>
        public bool WillAnimateUiRect(RectTransform movingRoot, RectTransform flyTarget, RectTransform boardRoot)
        {
            if (!useAnimation || movingRoot == null || flyTarget == null || boardRoot == null)
                return false;
            return ResolveOverlayParent(boardRoot) != null;
        }

        /// <summary>Duplicates <paramref name="source"/> (world-aligned), tweens the copy to <paramref name="flyTarget"/>, destroys the copy, then invokes <paramref name="onArrived"/>.</summary>
        public void PlayUiDuplicate(RectTransform source, RectTransform flyTarget, RectTransform boardRoot, Action onArrived)
        {
            if (onArrived == null) throw new ArgumentNullException(nameof(onArrived));
            if (!WillAnimateUiRect(source, flyTarget, boardRoot))
            {
                Debug.LogError("[TileCollectFly] PlayUiDuplicate called without a successful WillAnimateUiRect check.", this);
                return;
            }

            var overlay = ResolveOverlayParent(boardRoot);
            var clone = Instantiate(source.gameObject, source.transform.parent);
            var cloneRt = clone.GetComponent<RectTransform>();
            var worldPos = source.position;
            var worldRot = source.rotation;
            cloneRt.SetParent(overlay, true);
            cloneRt.SetPositionAndRotation(worldPos, worldRot);
            cloneRt.localScale = Vector3.one;
            cloneRt.SetAsLastSibling();
            clone.SetActive(true);
            foreach (var cg in clone.GetComponentsInChildren<CanvasGroup>(true))
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = false;
            }

            foreach (var g in clone.GetComponentsInChildren<Graphic>(true))
                g.enabled = true;

            var targetWorld = RectTransformWorldCenter(flyTarget);
            var endLocal = overlay.InverseTransformPoint(targetWorld);
            var len = Mathf.Max(0.02f, duration);

            cloneRt.DOKill(false);
            cloneRt
                .DOLocalMove(endLocal, len)
                .SetEase(ease)
                .OnComplete(() =>
                {
                    try
                    {
                        onArrived.Invoke();
                    }
                    finally
                    {
                        if (clone != null)
                        {
                            clone.transform.DOKill(false);
                            Destroy(clone);
                        }
                    }
                });
        }

        /// <summary>
        /// Caller must clear the board cell first. After the tween, <paramref name="onArrived"/> runs, then the flying icon and tile shell are destroyed.
        /// </summary>
        public void Play(BoardTileView view, RectTransform flyTarget, RectTransform boardRoot, Action onArrived)
        {
            if (onArrived == null) throw new ArgumentNullException(nameof(onArrived));
            if (!WillAnimate(view, flyTarget, boardRoot))
            {
                Debug.LogError("[TileCollectFly] Play called without a successful WillAnimate check.", this);
                return;
            }

            var iconRt = view.IconRectTransform;
            var overlay = ResolveOverlayParent(boardRoot);
            var shellView = view;
            var flyingGo = iconRt.gameObject;

            shellView.DestroyBackgroundImmediate();
            shellView.SetClickHandler(null);

            iconRt.SetParent(overlay, true);
            iconRt.SetAsLastSibling();

            var targetWorld = RectTransformWorldCenter(flyTarget);
            var endLocal = overlay.InverseTransformPoint(targetWorld);
            var t = Mathf.Max(0.02f, duration);

            iconRt.DOKill(false);
            iconRt
                .DOLocalMove(endLocal, t)
                .SetEase(ease)
                .OnComplete(() =>
                {
                    try
                    {
                        onArrived.Invoke();
                    }
                    finally
                    {
                        if (flyingGo != null)
                        {
                            flyingGo.transform.DOKill(false);
                            Destroy(flyingGo);
                        }

                        if (shellView != null)
                            Destroy(shellView.gameObject);
                    }
                });
        }

        RectTransform ResolveOverlayParent(RectTransform boardRoot)
        {
            if (iconFlyOverlayParent != null)
                return iconFlyOverlayParent;
            var canvas = boardRoot.GetComponentInParent<Canvas>();
            return canvas != null ? (RectTransform)canvas.transform : boardRoot;
        }

        static Vector3 RectTransformWorldCenter(RectTransform rt)
        {
            if (rt == null) return Vector3.zero;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
        }
    }
}
