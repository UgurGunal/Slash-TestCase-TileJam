using Core;
using DG.Tweening;
using Gameplay;
using Presentation.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>Thin facade: session binding and destination layout for collect fly targets.</summary>
    public sealed class OrderRackHud : MonoBehaviour, IHudDestinationLayout
    {
        [SerializeField] TileIconLibrary iconLibrary;
        [SerializeField] bool orderStripCompleteScaleAnimation = true;
        [SerializeField] float orderStripCompleteScaleDownSec = 0.22f;
        [SerializeField] Ease orderStripCompleteScaleDownEase = Ease.InQuad;
        [SerializeField] float orderStripCompleteScaleUpSec = 0.28f;
        [SerializeField] Ease orderStripCompleteScaleUpEase = Ease.OutBack;

        OrderRackHudBinder _controller;

        void OnEnable()
        {
            EnsureController();
            _controller?.Refresh();
        }

        void OnDestroy() => _controller?.Unbind();

        public void BindSession(LevelObjectiveSession session, IGameplayEventBus eventBus)
        {
            EnsureController();
            _controller.BindSession(session, eventBus);
        }

        public void ConfigureFromPrefabViews(OrderView[] orderViews, RackView rack, OrderSlotView orderSlotPrefab)
        {
            EnsureController();
            _controller.ConfigureViews(orderViews, rack, orderSlotPrefab);
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

            _controller = new OrderRackHudBinder(
                iconLibrary,
                orderStripCompleteScaleAnimation,
                orderStripCompleteScaleDownSec,
                orderStripCompleteScaleDownEase,
                orderStripCompleteScaleUpSec,
                orderStripCompleteScaleUpEase);
            return true;
        }
    }
}
