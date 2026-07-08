using Core;
using DG.Tweening;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>
    /// Presentation for one active customer order. Resolves sprites and issues high-level commands
    /// to the <see cref="OrderView"/> (which owns per-slot icon/tick state), plus the
    /// advance animation when the customer changes.
    /// </summary>
    public sealed class OrderPresenter
    {
        readonly int _orderIndex;
        readonly OrderView _view;
        readonly TileIconLibrary _iconLibrary;
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
            TileIconLibrary iconLibrary,
            bool scaleAnimationEnabled,
            float scaleDownSec,
            Ease scaleDownEase,
            float scaleUpSec,
            Ease scaleUpEase)
        {
            _orderIndex = orderIndex;
            _view = view;
            _iconLibrary = iconLibrary;
            _scaleAnimationEnabled = scaleAnimationEnabled;
            _scaleDownSec = scaleDownSec;
            _scaleDownEase = scaleDownEase;
            _scaleUpSec = scaleUpSec;
            _scaleUpEase = scaleUpEase;
            _containerBaseScale = Container != null ? Container.localScale : Vector3.one;
        }

        RectTransform Container => _view != null ? _view.Container : null;

        public bool SkipRefreshDuringRoll => _skipRefreshDuringRoll;

        public void CacheContainerBaseScale()
        {
            if (Container != null)
                _containerBaseScale = Container.localScale;
        }

        public void KillTweens() => Container?.DOKill(false);

        public void Refresh(LevelObjectiveSession session, int stride)
        {
            if (session == null || _view == null || _skipRefreshDuringRoll) return;

            for (var i = 0; i < stride; i++)
            {
                if (!session.GetActiveSlot(_orderIndex, out _, out var order, out var fulfilled) || i >= order.Length)
                {
                    _view.ClearSlot(i);
                    continue;
                }

                var kind = order.GetIcon(i);
                var cellDone = fulfilled != null && i < fulfilled.Length && fulfilled[i];
                _view.SetSlotIcon(i, ResolveSprite(kind), cellDone);
            }
        }

        public void PlayAdvanceAnimation(LevelObjectiveSession session, int stride)
        {
            var container = Container;
            if (!_scaleAnimationEnabled || session == null || container == null) return;

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
                    Refresh(session, stride);
                    container.DOScale(_containerBaseScale, up).SetEase(_scaleUpEase);
                });
        }

        public bool TryGetIconRectTransform(int iconIdx, out RectTransform rect)
        {
            rect = null;
            return _view != null && _view.TryGetSlotRect(iconIdx, out rect);
        }

        public Image FirstNonNullIcon() => _view != null ? _view.FirstIconImage() : null;

        Sprite ResolveSprite(TileKind kind)
        {
            if (_iconLibrary != null && _iconLibrary.TryGetSprite(kind, out var fromLib) && fromLib != null)
                return fromLib;
            return Resources.Load<Sprite>($"{BoardTileView.TileIconsResourcesFolder}/{kind}");
        }
    }
}
