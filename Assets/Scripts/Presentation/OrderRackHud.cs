using Core;
using DG.Tweening;
using Gameplay;
using Presentation.Hud;
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

    /// <summary>Thin facade: session binding and destination layout for collect fly targets.</summary>
    public sealed class OrderRackHud : MonoBehaviour, IHudDestinationLayout
    {
        [SerializeField] TileIconLibrary iconLibrary;
        [SerializeField] OrderStripUi[] orderStrips;
        [SerializeField] Image[] rackSlotImages;
        [SerializeField] GameObject matchedOrderTickPrefab;
        [SerializeField] Vector2 matchedTickAnchorOffset = new Vector2(-6f, 6f);
        [SerializeField] [Range(0.55f, 1f)] float fulfilledOrderIconTint = 0.78f;
        [SerializeField] bool orderStripCompleteScaleAnimation = true;
        [SerializeField] float orderStripCompleteScaleDownSec = 0.22f;
        [SerializeField] Ease orderStripCompleteScaleDownEase = Ease.InQuad;
        [SerializeField] float orderStripCompleteScaleUpSec = 0.28f;
        [SerializeField] Ease orderStripCompleteScaleUpEase = Ease.OutBack;

        OrderRackHudController _controller;

        void Awake() => EnsureController();

        void OnEnable()
        {
            EnsureController();
            if (_controller != null)
                _controller.Refresh();
        }

        void OnDestroy() => _controller?.Unbind();

#if UNITY_EDITOR
        void OnValidate() =>
            OrderRackLayoutDiagnostics.ValidateOnEditor(this, orderStrips, rackSlotImages, matchedOrderTickPrefab);
#endif

        public void BindSession(LevelObjectiveSession session, IGameplayEventBus eventBus)
        {
            EnsureController();
            _controller.BindSession(session, eventBus);
        }

        public void ConfigureFromPrefabViews(OrderStripView[] stripViews, RackBarView rackBar)
        {
            EnsureController();
            _controller.ConfigureViews(stripViews, rackBar);
        }

        public bool TryGetRackSlotImage(int index, out Image img)
        {
            img = null;
            if (!EnsureController()) return false;
            return _controller.DestinationLayout.TryGetRackSlotImage(index, out img);
        }

        public bool TryGetRackSlotImages(out Image[] images)
        {
            images = null;
            if (!EnsureController()) return false;
            return _controller.DestinationLayout.TryGetRackSlotImages(out images);
        }

        public bool TryGetOrderIconRectTransform(int activeOrderSlot, int iconIdx, out RectTransform rect)
        {
            rect = null;
            if (!EnsureController()) return false;
            return _controller.DestinationLayout.TryGetOrderIconRectTransform(activeOrderSlot, iconIdx, out rect);
        }

        bool EnsureController()
        {
            if (_controller != null) return true;

            _controller = new OrderRackHudController(
                iconLibrary,
                matchedOrderTickPrefab,
                matchedTickAnchorOffset,
                fulfilledOrderIconTint,
                orderStripCompleteScaleAnimation,
                orderStripCompleteScaleDownSec,
                orderStripCompleteScaleDownEase,
                orderStripCompleteScaleUpSec,
                orderStripCompleteScaleUpEase);
            _controller.ConfigureViews(orderStrips, rackSlotImages);
            return true;
        }
    }
}
