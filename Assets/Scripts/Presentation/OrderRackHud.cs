using Core;
using DG.Tweening;
using Gameplay;
using Presentation.Hud;
using UnityEngine;
using UnityEngine.Serialization;

namespace Presentation
{
    /// <summary>Thin facade: session binding and access to the live destination layout for collect fly targets.</summary>
    public sealed class OrderRackHud : MonoBehaviour
    {
        [SerializeField] TileIconLibrary iconLibrary;

        [FormerlySerializedAs("orderStripCompleteScaleAnimation")]
        [SerializeField] bool orderCompleteScaleAnimation = true;
        [FormerlySerializedAs("orderStripCompleteScaleDownSec")]
        [SerializeField] float orderCompleteScaleDownSec = 0.22f;
        [FormerlySerializedAs("orderStripCompleteScaleDownEase")]
        [SerializeField] Ease orderCompleteScaleDownEase = Ease.InQuad;
        [FormerlySerializedAs("orderStripCompleteScaleUpSec")]
        [SerializeField] float orderCompleteScaleUpSec = 0.28f;
        [FormerlySerializedAs("orderStripCompleteScaleUpEase")]
        [SerializeField] Ease orderCompleteScaleUpEase = Ease.OutBack;

        OrderRackHudBinder _controller;

        /// <summary>Live rect resolver for order icons and rack slots; rebuilt when the session or views change.</summary>
        public IHudDestinationLayout DestinationLayout
        {
            get
            {
                EnsureController();
                return _controller?.DestinationLayout;
            }
        }

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

        bool EnsureController()
        {
            if (_controller != null) return true;

            _controller = new OrderRackHudBinder(
                iconLibrary,
                orderCompleteScaleAnimation,
                orderCompleteScaleDownSec,
                orderCompleteScaleDownEase,
                orderCompleteScaleUpSec,
                orderCompleteScaleUpEase);
            return true;
        }
    }
}
