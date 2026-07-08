using DG.Tweening;
using Gameplay;
using UnityEngine;

namespace Presentation.Hud
{
    /// <summary>
    /// Presentation for one active customer order. Maps <see cref="IObjectiveHudState"/> rows onto
    /// <see cref="OrderView"/> commands and plays the advance animation when the customer changes.
    /// </summary>
    public sealed class OrderPresenter
    {
        readonly int _orderIndex;
        readonly OrderView _view;
        readonly TileKindSpriteResolver _sprites;
        readonly bool _scaleAnimationEnabled;
        readonly float _scaleDownSec;
        readonly Ease _scaleDownEase;
        readonly float _scaleUpSec;
        readonly Ease _scaleUpEase;

        bool _skipRefreshDuringRoll;
        Vector3 _containerBaseScale = Vector3.one;

        public OrderPresenter(
            int orderIndex,
            OrderView view,
            TileKindSpriteResolver sprites,
            bool scaleAnimationEnabled,
            float scaleDownSec,
            Ease scaleDownEase,
            float scaleUpSec,
            Ease scaleUpEase)
        {
            _orderIndex = orderIndex;
            _view = view;
            _sprites = sprites;
            _scaleAnimationEnabled = scaleAnimationEnabled;
            _scaleDownSec = scaleDownSec;
            _scaleDownEase = scaleDownEase;
            _scaleUpSec = scaleUpSec;
            _scaleUpEase = scaleUpEase;
            _containerBaseScale = Container != null ? Container.localScale : Vector3.one;
        }

        RectTransform Container => _view != null ? _view.Container : null;

        public void CacheContainerBaseScale()
        {
            if (Container != null)
                _containerBaseScale = Container.localScale;
        }

        public void KillTweens() => Container?.DOKill(false);

        public void Refresh(IObjectiveHudState state, int stride)
        {
            if (state == null || _view == null || _skipRefreshDuringRoll) return;

            state.TryGetOrderRow(_orderIndex, out var row);

            for (var i = 0; i < stride; i++)
            {
                if (!row.IsActive || i >= row.IconCount)
                {
                    _view.ClearSlot(i);
                    continue;
                }

                _view.SetSlotIcon(i, _sprites.Resolve(row.GetIcon(i)), row.IsFulfilled(i));
            }
        }

        public void PlayAdvanceAnimation(IObjectiveHudState state, int stride)
        {
            var container = Container;
            if (!_scaleAnimationEnabled || state == null || container == null) return;

            container.DOKill(false);
            _skipRefreshDuringRoll = true;
            container.localScale = _containerBaseScale;

            // The model already advanced to the next customer, so flash the just-finished order
            // fully ticked (including the final match) while it scales away.
            _view.CompleteAllVisible();

            var down = Mathf.Max(0.02f, _scaleDownSec);
            var up = Mathf.Max(0.02f, _scaleUpSec);
            container
                .DOScale(Vector3.zero, down)
                .SetEase(_scaleDownEase)
                .OnComplete(() =>
                {
                    // Clear the guard first: Refresh() early-returns while it is set, so the new
                    // customer's icons would otherwise never populate until the next collect.
                    _skipRefreshDuringRoll = false;
                    Refresh(state, stride);
                    container.DOScale(_containerBaseScale, up).SetEase(_scaleUpEase);
                });
        }

        public bool TryGetIconRectTransform(int iconIdx, out RectTransform rect)
        {
            rect = null;
            return _view != null && _view.TryGetSlotRect(iconIdx, out rect);
        }
    }
}
