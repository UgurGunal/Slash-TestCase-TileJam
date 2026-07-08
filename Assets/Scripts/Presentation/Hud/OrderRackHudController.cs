using Core;
using DG.Tweening;
using Gameplay;

namespace Presentation.Hud
{
    /// <summary>Subscribes to gameplay events, owns the order/rack presenters, and drives refreshes.</summary>
    public sealed class OrderRackHudController
    {
        readonly TileKindSpriteResolver _sprites;
        readonly bool _orderCompleteScaleAnimation;
        readonly float _orderCompleteScaleDownSec;
        readonly Ease _orderCompleteScaleDownEase;
        readonly float _orderCompleteScaleUpSec;
        readonly Ease _orderCompleteScaleUpEase;

        OrderPresenter[] _orders;
        RackPresenter _rack;
        HudDestinationLayout _destinationLayout;
        IObjectiveHudState _hudState;
        IGameplayEventBus _eventBus;

        // Views spawned by OrderRackHudBuilder; order slots are spawned at bind time from the level's max order size.
        OrderView[] _orderViews;
        OrderSlotView _orderSlotPrefab;

        public OrderRackHudController(
            TileKindSpriteResolver sprites,
            bool orderCompleteScaleAnimation,
            float orderCompleteScaleDownSec,
            Ease orderCompleteScaleDownEase,
            float orderCompleteScaleUpSec,
            Ease orderCompleteScaleUpEase)
        {
            _sprites = sprites;
            _orderCompleteScaleAnimation = orderCompleteScaleAnimation;
            _orderCompleteScaleDownSec = orderCompleteScaleDownSec;
            _orderCompleteScaleDownEase = orderCompleteScaleDownEase;
            _orderCompleteScaleUpSec = orderCompleteScaleUpSec;
            _orderCompleteScaleUpEase = orderCompleteScaleUpEase;
            _orders = System.Array.Empty<OrderPresenter>();
        }

        public IHudDestinationLayout DestinationLayout => _destinationLayout;

        public void ConfigureViews(OrderView[] orderViews, RackView rack, OrderSlotView orderSlotPrefab)
        {
            _orderViews = orderViews;
            _orderSlotPrefab = orderSlotPrefab;
            _rack = new RackPresenter(rack != null ? rack.RackSlotImages : null, _sprites);

            // Execution-order independent: if the session was already bound before the builder
            // configured the views, build the orders now; otherwise BindSession will do it.
            if (_hudState != null && _orderViews != null && _orderSlotPrefab != null)
            {
                BuildOrdersFromViews(_hudState.MaxOrderIconsOnLevel);
                Refresh();
            }
            else
            {
                _orders = System.Array.Empty<OrderPresenter>();
                RebuildDestinationLayout();
            }
        }

        public void BindSession(LevelObjectiveSession session, IGameplayEventBus eventBus)
        {
            Unbind();
            _hudState = session;
            _eventBus = eventBus;

            if (_orderViews != null && _orderSlotPrefab != null)
                BuildOrdersFromViews(session != null ? session.MaxOrderIconsOnLevel : 0);
            else
                RebuildDestinationLayout();

            if (_eventBus != null)
            {
                _eventBus.Subscribe<TileMatchedOrderEvent>(OnGameplayRefresh);
                _eventBus.Subscribe<TileSentToRackEvent>(OnGameplayRefresh);
                _eventBus.Subscribe<OrderCompletedEvent>(OnGameplayRefresh);
                _eventBus.Subscribe<OrderSlotAdvancedEvent>(OnOrderSlotAdvancedFromBus);
            }

            Refresh();
        }

        public void Unbind()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<TileMatchedOrderEvent>(OnGameplayRefresh);
                _eventBus.Unsubscribe<TileSentToRackEvent>(OnGameplayRefresh);
                _eventBus.Unsubscribe<OrderCompletedEvent>(OnGameplayRefresh);
                _eventBus.Unsubscribe<OrderSlotAdvancedEvent>(OnOrderSlotAdvancedFromBus);
            }

            KillAllOrderTweens();
            _eventBus = null;
            _hudState = null;
            RebuildDestinationLayout();
        }

        public void Refresh()
        {
            if (_hudState == null) return;

            var stride = _hudState.MaxOrderIconsOnLevel;
            var orderCount = _orders?.Length ?? 0;
            for (var s = 0; s < orderCount; s++)
                _orders[s].Refresh(_hudState, stride);

            _rack?.Refresh(_hudState);
        }

        void BuildOrdersFromViews(int slotCount)
        {
            var n = _orderViews.Length;
            _orders = new OrderPresenter[n];
            for (var s = 0; s < n; s++)
            {
                var view = _orderViews[s];
                if (view != null)
                    view.BuildSlots(_orderSlotPrefab, slotCount);
                _orders[s] = CreateOrderPresenter(s, view);
            }

            RebuildDestinationLayout();
            CacheOrderBaseScales();
        }

        void OnGameplayRefresh(TileMatchedOrderEvent _) => Refresh();
        void OnGameplayRefresh(TileSentToRackEvent _) => Refresh();
        void OnGameplayRefresh(OrderCompletedEvent _) => Refresh();
        void OnOrderSlotAdvancedFromBus(OrderSlotAdvancedEvent e) => OnActiveOrderSlotAdvanced(e.SlotIndex);

        void OnActiveOrderSlotAdvanced(int slot)
        {
            if (!_orderCompleteScaleAnimation || _hudState == null || _orders == null) return;
            if ((uint)slot >= (uint)_orders.Length) return;
            _orders[slot].PlayAdvanceAnimation(_hudState, _hudState.MaxOrderIconsOnLevel);
        }

        void RebuildDestinationLayout() =>
            _destinationLayout = new HudDestinationLayout(_hudState, _orders, _rack);

        void CacheOrderBaseScales()
        {
            if (_orders == null) return;
            for (var i = 0; i < _orders.Length; i++)
                _orders[i].CacheContainerBaseScale();
        }

        void KillAllOrderTweens()
        {
            if (_orders == null) return;
            for (var i = 0; i < _orders.Length; i++)
                _orders[i].KillTweens();
        }

        OrderPresenter CreateOrderPresenter(int index, OrderView view) =>
            new OrderPresenter(
                index,
                view,
                _sprites,
                _orderCompleteScaleAnimation,
                _orderCompleteScaleDownSec,
                _orderCompleteScaleDownEase,
                _orderCompleteScaleUpSec,
                _orderCompleteScaleUpEase);
    }
}
