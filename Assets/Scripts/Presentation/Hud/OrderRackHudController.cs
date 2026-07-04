using Core;
using DG.Tweening;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Session/bus subscription and refresh orchestration for order strips and rack bar.</summary>
    public sealed class OrderRackHudController
    {
        readonly TileIconLibrary _iconLibrary;
        readonly GameObject _matchedTickPrefab;
        readonly Vector2 _matchedTickAnchorOffset;
        readonly float _fulfilledOrderIconTint;
        readonly bool _orderStripCompleteScaleAnimation;
        readonly float _orderStripCompleteScaleDownSec;
        readonly Ease _orderStripCompleteScaleDownEase;
        readonly float _orderStripCompleteScaleUpSec;
        readonly Ease _orderStripCompleteScaleUpEase;

        OrderStripPresenter[] _strips;
        RackBarPresenter _rack;
        HudDestinationLayout _destinationLayout;
        LevelObjectiveSession _session;
        IGameplayEventBus _eventBus;
        bool _loggedLayoutMismatch;
        bool _loggedStripVisibilityHints;

        public OrderRackHudController(
            TileIconLibrary iconLibrary,
            GameObject matchedTickPrefab,
            Vector2 matchedTickAnchorOffset,
            float fulfilledOrderIconTint,
            bool orderStripCompleteScaleAnimation,
            float orderStripCompleteScaleDownSec,
            Ease orderStripCompleteScaleDownEase,
            float orderStripCompleteScaleUpSec,
            Ease orderStripCompleteScaleUpEase)
        {
            _iconLibrary = iconLibrary;
            _matchedTickPrefab = matchedTickPrefab;
            _matchedTickAnchorOffset = matchedTickAnchorOffset;
            _fulfilledOrderIconTint = fulfilledOrderIconTint;
            _orderStripCompleteScaleAnimation = orderStripCompleteScaleAnimation;
            _orderStripCompleteScaleDownSec = orderStripCompleteScaleDownSec;
            _orderStripCompleteScaleDownEase = orderStripCompleteScaleDownEase;
            _orderStripCompleteScaleUpSec = orderStripCompleteScaleUpSec;
            _orderStripCompleteScaleUpEase = orderStripCompleteScaleUpEase;
        }

        public IHudDestinationLayout DestinationLayout => _destinationLayout;

        public void ConfigureViews(OrderStripUi[] orderStrips, Image[] rackSlotImages)
        {
            var slotCount = GameConstants.ActiveOrderSlotsCount;
            _strips = new OrderStripPresenter[slotCount];
            for (var s = 0; s < slotCount; s++)
            {
                var row = orderStrips != null && s < orderStrips.Length ? orderStrips[s] : default;
                var container = ResolveStripContainer(row);
                _strips[s] = CreateStripPresenter(s, container, row.iconImages);
            }

            _rack = new RackBarPresenter(rackSlotImages, _iconLibrary);
            RebuildDestinationLayout();
            CacheStripBaseScales();
        }

        public void ConfigureViews(OrderStripView[] stripViews, RackBarView rackBar)
        {
            var slotCount = GameConstants.ActiveOrderSlotsCount;
            _strips = new OrderStripPresenter[slotCount];
            for (var s = 0; s < slotCount; s++)
            {
                var view = stripViews != null && s < stripViews.Length ? stripViews[s] : null;
                _strips[s] = CreateStripPresenter(
                    s,
                    view != null ? view.StripContainer : null,
                    view != null ? view.IconImages : null);
            }

            _rack = new RackBarPresenter(rackBar != null ? rackBar.RackSlotImages : null, _iconLibrary);
            RebuildDestinationLayout();
            CacheStripBaseScales();
        }

        public void BindSession(LevelObjectiveSession session, IGameplayEventBus eventBus)
        {
            Unbind();
            _session = session;
            _eventBus = eventBus;
            _loggedLayoutMismatch = false;
            _loggedStripVisibilityHints = false;
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

            KillAllStripTweens();
            _eventBus = null;
            _session = null;
            RebuildDestinationLayout();
        }

        public void Refresh()
        {
            if (_session == null) return;

            var stride = _session.MaxOrderIconsOnLevel;
            var slotCount = GameConstants.ActiveOrderSlotsCount;
            for (var s = 0; s < slotCount && s < _strips?.Length; s++)
                _strips[s].Refresh(_session, stride);

            if (_rack != null)
            {
                if (_rack.TryGetRackSlotImages(out var rackImages) &&
                    (rackImages == null || rackImages.Length < GameConstants.RackCapacity))
                    LogLayoutMismatchOnce(
                        $"Assign {GameConstants.RackCapacity} rack slot images so every rack slot can be shown.");
                _rack.Refresh(_session);
            }

            OrderRackLayoutDiagnostics.DiagnoseStripVisibilityOnce(
                null,
                _session,
                _strips,
                ref _loggedStripVisibilityHints);
        }

        void OnGameplayRefresh(TileMatchedOrderEvent _) => Refresh();
        void OnGameplayRefresh(TileSentToRackEvent _) => Refresh();
        void OnGameplayRefresh(OrderCompletedEvent _) => Refresh();
        void OnOrderSlotAdvancedFromBus(OrderSlotAdvancedEvent e) => OnActiveOrderSlotAdvanced(e.SlotIndex);

        void OnActiveOrderSlotAdvanced(int slot)
        {
            if (!_orderStripCompleteScaleAnimation || _session == null || _strips == null) return;
            if ((uint)slot >= (uint)_strips.Length) return;
            _strips[slot].PlayAdvanceAnimation(_session, _session.MaxOrderIconsOnLevel);
        }

        void RebuildDestinationLayout() =>
            _destinationLayout = new HudDestinationLayout(_session, _strips, _rack);

        void CacheStripBaseScales()
        {
            if (_strips == null) return;
            for (var i = 0; i < _strips.Length; i++)
                _strips[i].CacheContainerBaseScale();
        }

        void KillAllStripTweens()
        {
            if (_strips == null) return;
            for (var i = 0; i < _strips.Length; i++)
                _strips[i].KillTweens();
        }

        OrderStripPresenter CreateStripPresenter(int index, RectTransform container, Image[] icons) =>
            new OrderStripPresenter(
                index,
                container,
                icons,
                _iconLibrary,
                _matchedTickPrefab,
                _matchedTickAnchorOffset,
                _fulfilledOrderIconTint,
                _orderStripCompleteScaleAnimation,
                _orderStripCompleteScaleDownSec,
                _orderStripCompleteScaleDownEase,
                _orderStripCompleteScaleUpSec,
                _orderStripCompleteScaleUpEase);

        static RectTransform ResolveStripContainer(OrderStripUi row)
        {
            if (row.stripContainer != null)
                return row.stripContainer;
            var first = FirstNonNullImage(row.iconImages);
            if (first == null)
                return null;
            var p = first.transform.parent;
            return p != null ? p as RectTransform : first.rectTransform;
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
            Debug.LogError($"[OrderRackHud] {message}\nGameplay still uses the full order from level data; tiles can match icons you do not see until this is fixed.");
        }
    }
}
