using Core;
using DG.Tweening;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Presentation for one active order strip: icons, ticks, and advance animation.</summary>
    public sealed class OrderStripPresenter
    {
        readonly int _stripIndex;
        readonly RectTransform _stripContainer;
        readonly Image[] _iconImages;
        readonly TileIconLibrary _iconLibrary;
        readonly GameObject _matchedTickPrefab;
        readonly Vector2 _matchedTickAnchorOffset;
        readonly float _fulfilledOrderIconTint;
        readonly bool _scaleAnimationEnabled;
        readonly float _scaleDownSec;
        readonly Ease _scaleDownEase;
        readonly float _scaleUpSec;
        readonly Ease _scaleUpEase;

        bool _skipRefreshDuringRoll;
        Vector3 _containerBaseScale = Vector3.one;

        const string MatchedTickChildName = "OrderMatchedTick";

        public OrderStripPresenter(
            int stripIndex,
            RectTransform stripContainer,
            Image[] iconImages,
            TileIconLibrary iconLibrary,
            GameObject matchedTickPrefab,
            Vector2 matchedTickAnchorOffset,
            float fulfilledOrderIconTint,
            bool scaleAnimationEnabled,
            float scaleDownSec,
            Ease scaleDownEase,
            float scaleUpSec,
            Ease scaleUpEase)
        {
            _stripIndex = stripIndex;
            _stripContainer = stripContainer;
            _iconImages = iconImages;
            _iconLibrary = iconLibrary;
            _matchedTickPrefab = matchedTickPrefab;
            _matchedTickAnchorOffset = matchedTickAnchorOffset;
            _fulfilledOrderIconTint = fulfilledOrderIconTint;
            _scaleAnimationEnabled = scaleAnimationEnabled;
            _scaleDownSec = scaleDownSec;
            _scaleDownEase = scaleDownEase;
            _scaleUpSec = scaleUpSec;
            _scaleUpEase = scaleUpEase;
            _containerBaseScale = _stripContainer != null ? _stripContainer.localScale : Vector3.one;
        }

        public bool SkipRefreshDuringRoll => _skipRefreshDuringRoll;

        public void CacheContainerBaseScale()
        {
            if (_stripContainer != null)
                _containerBaseScale = _stripContainer.localScale;
        }

        public void KillTweens() => _stripContainer?.DOKill(false);

        public void Refresh(LevelObjectiveSession session, int stride)
        {
            if (session == null || _skipRefreshDuringRoll) return;

            for (var i = 0; i < stride; i++)
            {
                if (!session.GetActiveSlot(_stripIndex, out _, out var order, out var fulfilled))
                {
                    SetIconHidden(i);
                    continue;
                }

                if (i >= order.Length)
                {
                    SetIconHidden(i);
                    continue;
                }

                var img = GetIcon(i);
                if (img == null) continue;

                img.enabled = true;
                var kind = order.GetIcon(i);
                var cellDone = fulfilled != null && i < fulfilled.Length && fulfilled[i];
                ApplySprite(img, kind, cellDone);
                UpdateOrderMatchedTick(img, cellDone && img.enabled);
            }
        }

        public void PlayAdvanceAnimation(LevelObjectiveSession session, int stride)
        {
            if (!_scaleAnimationEnabled || session == null || _stripContainer == null) return;

            _stripContainer.DOKill(false);
            _skipRefreshDuringRoll = true;
            _stripContainer.localScale = _containerBaseScale;

            var down = Mathf.Max(0.02f, _scaleDownSec);
            var up = Mathf.Max(0.02f, _scaleUpSec);
            _stripContainer
                .DOScale(Vector3.zero, down)
                .SetEase(_scaleDownEase)
                .OnComplete(() =>
                {
                    Refresh(session, stride);
                    _skipRefreshDuringRoll = false;
                    _stripContainer.DOScale(_containerBaseScale, up).SetEase(_scaleUpEase);
                });
        }

        public bool TryGetIconRectTransform(int iconIdx, out RectTransform rect)
        {
            rect = null;
            var cells = _iconImages;
            if (cells == null) return false;

            if (iconIdx < cells.Length && cells[iconIdx] != null)
            {
                rect = cells[iconIdx].rectTransform;
                return true;
            }

            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                rect = cells[i].rectTransform;
                return true;
            }

            return false;
        }

        public Image FirstNonNullIcon() => FirstNonNullImage(_iconImages);

        void SetIconHidden(int index)
        {
            var img = GetIcon(index);
            if (img == null) return;
            ClearOrderMatchedTick(img);
            img.enabled = false;
        }

        Image GetIcon(int index) =>
            _iconImages != null && index < _iconImages.Length ? _iconImages[index] : null;

        void ApplySprite(Image img, TileKind kind, bool fulfilledDim)
        {
            Sprite sprite = null;
            if (_iconLibrary != null && _iconLibrary.TryGetSprite(kind, out var fromLib))
                sprite = fromLib;
            if (sprite == null)
                sprite = Resources.Load<Sprite>($"{BoardTileView.TileIconsResourcesFolder}/{kind}");

            img.sprite = sprite;
            img.enabled = sprite != null;
            img.color = fulfilledDim ? new Color(_fulfilledOrderIconTint, _fulfilledOrderIconTint, _fulfilledOrderIconTint, 1f) : Color.white;
        }

        void UpdateOrderMatchedTick(Image icon, bool showMatched)
        {
            if (icon == null) return;
            if (!showMatched || _matchedTickPrefab == null)
            {
                ClearOrderMatchedTick(icon);
                return;
            }

            var parent = icon.rectTransform;
            var existing = parent.Find(MatchedTickChildName);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                LayoutMatchedTick(existing as RectTransform, parent);
                existing.SetAsLastSibling();
                return;
            }

            var tickGo = UnityEngine.Object.Instantiate(_matchedTickPrefab, parent);
            tickGo.name = MatchedTickChildName;
            if (!tickGo.TryGetComponent<RectTransform>(out var tickRt))
            {
                UnityEngine.Object.Destroy(tickGo);
                return;
            }

            LayoutMatchedTick(tickRt, parent);
            tickRt.SetAsLastSibling();
        }

        void LayoutMatchedTick(RectTransform tick, RectTransform iconRect)
        {
            if (tick == null) return;
            tick.anchorMin = new Vector2(1f, 0f);
            tick.anchorMax = new Vector2(1f, 0f);
            tick.pivot = new Vector2(1f, 0f);
            tick.anchoredPosition = _matchedTickAnchorOffset;
            tick.localRotation = Quaternion.identity;
            tick.localScale = Vector3.one;
        }

        void ClearOrderMatchedTick(Image icon)
        {
            if (icon == null) return;
            var t = icon.rectTransform.Find(MatchedTickChildName);
            if (t != null)
                UnityEngine.Object.Destroy(t.gameObject);
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
    }
}
