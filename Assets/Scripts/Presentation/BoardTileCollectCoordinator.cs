using System;
using Core;
using Gameplay;
using LevelData;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Input → session collect rules, board ↔ HUD fly feedback, and deferred rack→order drain.
    /// Keeps <see cref="LevelBoardGrid"/> free of animation and objective logic.
    /// </summary>
    public sealed class BoardTileCollectCoordinator
    {
        readonly LevelBoardGrid _grid;
        TileCollectFly _collectFly;
        OrderRackHud _orderRackHud;

        LevelObjectiveSession _session;
        bool _tileCollectInFlight;

        public BoardTileCollectCoordinator(LevelBoardGrid grid) =>
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));

        public void SetPresentationRefs(TileCollectFly collectFly, OrderRackHud orderRackHud)
        {
            _collectFly = collectFly;
            _orderRackHud = orderRackHud;
        }

        public void BindSession(LevelObjectiveSession session) => _session = session;

        /// <summary>Clears the “collect in progress” guard (e.g. when the level is reloaded mid-animation).</summary>
        public void CancelInFlightCollect() => _tileCollectInFlight = false;

        public void HandleTileClicked(BoardTileView view)
        {
            if (_tileCollectInFlight) return;
            if (_grid.PlayState == null || view == null || _session == null) return;
            if (_session.HasFailed || _session.HasWon) return;

            var x = view.GridX;
            var y = view.GridY;
            var l = view.LayerIndex;
            if (!TileClickability.IsClickable(_grid.PlayState, x, y, l)) return;

            if (_collectFly == null || !_collectFly.UseAnimation || _orderRackHud == null)
            {
                CollectTileInstant(view, x, y, l);
                return;
            }

            if (!_session.TryGetFlyTargetForKind(view.Kind, out var flyTarget, out _))
            {
                CollectTileInstant(view, x, y, l);
                return;
            }

            if (!_orderRackHud.TryGetRectTransformForFlyTarget(flyTarget, out var targetRt))
            {
                CollectTileInstant(view, x, y, l);
                return;
            }

            if (!_collectFly.WillAnimate(view, targetRt, _grid.BoardRoot))
            {
                CollectTileInstant(view, x, y, l);
                return;
            }

            var kind = view.Kind;
            _tileCollectInFlight = true;
            _grid.DetachTileForAnimatedCollect(view, x, y, l);

            _collectFly.Play(view, targetRt, _grid.BoardRoot, () => ApplyCollectAfterFlyAnimation(kind));
        }

        void ApplyCollectAfterFlyAnimation(TileKind kind)
        {
            if (_session == null)
            {
                EndTileCollectFlight();
                return;
            }

            var deferRackFly = _collectFly != null && _collectFly.UseAnimation && _orderRackHud != null;
            _session.DeferRackDrainAnimation = deferRackFly;
            TileCollectResult result;
            try
            {
                result = _session.TryCollectTile(kind);
            }
            finally
            {
                _session.DeferRackDrainAnimation = false;
            }

            if (result == TileCollectResult.LevelWon)
                Debug.Log("[BoardCollect] All orders completed — level won.");
            if (result == TileCollectResult.FailedRackFull)
                Debug.LogWarning("[BoardCollect] Rack full — level failed.");

            if (result == TileCollectResult.RackDrainPending)
            {
                ProcessNextAnimatedRackDrainStep();
                return;
            }

            EndTileCollectFlight();
        }

        void CollectTileInstant(BoardTileView view, int x, int y, int l)
        {
            _tileCollectInFlight = true;
            if (_session == null)
            {
                EndTileCollectFlight();
                return;
            }

            var deferRackFly = _collectFly != null && _collectFly.UseAnimation && _orderRackHud != null;
            _session.DeferRackDrainAnimation = deferRackFly;
            TileCollectResult result;
            try
            {
                result = _session.TryCollectTile(view.Kind);
            }
            finally
            {
                _session.DeferRackDrainAnimation = false;
            }

            if (result == TileCollectResult.SessionInactive)
            {
                EndTileCollectFlight();
                return;
            }

            if (result == TileCollectResult.FailedRackFull)
            {
                Debug.LogWarning("[BoardCollect] Rack full — level failed.");
                EndTileCollectFlight();
                return;
            }

            if (result == TileCollectResult.LevelWon)
                Debug.Log("[BoardCollect] All orders completed — level won.");

            _grid.RemoveAndDestroyTile(view, x, y, l);

            if (result == TileCollectResult.RackDrainPending)
            {
                ProcessNextAnimatedRackDrainStep();
                return;
            }

            EndTileCollectFlight();
        }

        void EndTileCollectFlight()
        {
            _grid.RefreshClickabilityVisuals();
            _tileCollectInFlight = false;
        }

        void FinishRackDrainSynchronously()
        {
            if (_session == null) return;
            while (_session.TryPeekRackDrainStep(out var i, out _, out _))
            {
                var r = _session.ApplyRackDrainStepAt(i);
                if (r == TileCollectResult.LevelWon)
                {
                    Debug.Log("[BoardCollect] All orders completed — level won.");
                    break;
                }
            }

            _session.NotifyStateChanged();
        }

        void ProcessNextAnimatedRackDrainStep()
        {
            if (_session == null)
            {
                EndTileCollectFlight();
                return;
            }

            var boardRoot = _grid.BoardRoot;
            if (_collectFly == null || !_collectFly.UseAnimation || _orderRackHud == null || boardRoot == null)
            {
                FinishRackDrainSynchronously();
                EndTileCollectFlight();
                return;
            }

            while (_session.TryPeekRackDrainStep(out var rackIdx, out _, out var orderTarget))
            {
                if (!_orderRackHud.TryGetRackSlotImage(rackIdx, out var rackImg))
                {
                    var r = _session.ApplyRackDrainStepAt(rackIdx);
                    _session.NotifyStateChanged();
                    if (r == TileCollectResult.LevelWon)
                    {
                        Debug.Log("[BoardCollect] All orders completed — level won.");
                        EndTileCollectFlight();
                        return;
                    }

                    continue;
                }

                if (!_orderRackHud.TryGetRectTransformForFlyTarget(orderTarget, out var targetRt))
                {
                    FinishRackDrainSynchronously();
                    EndTileCollectFlight();
                    return;
                }

                var rackRt = rackImg.rectTransform;
                if (!_collectFly.WillAnimateUiRect(rackRt, targetRt, boardRoot))
                {
                    FinishRackDrainSynchronously();
                    EndTileCollectFlight();
                    return;
                }

                _collectFly.PlayUiDuplicate(rackRt, targetRt, boardRoot, () => RackDrainStepApplyAndContinue(rackIdx));
                rackImg.enabled = false;
                return;
            }

            _session.NotifyStateChanged();
            EndTileCollectFlight();
        }

        void RackDrainStepApplyAndContinue(int rackIdx)
        {
            if (_session == null)
            {
                EndTileCollectFlight();
                return;
            }

            var applyResult = _session.ApplyRackDrainStepAt(rackIdx);
            _session.NotifyStateChanged();

            if (applyResult == TileCollectResult.LevelWon)
            {
                Debug.Log("[BoardCollect] All orders completed — level won.");
                EndTileCollectFlight();
                return;
            }

            ProcessNextAnimatedRackDrainStep();
        }
    }
}
