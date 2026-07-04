using System;
using Core;
using Gameplay;
using LevelData;
using LevelData.Board;
using Presentation.Hud;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Input → session collect rules, board ↔ HUD fly feedback, and rack→order drain orchestration.
    /// Keeps <see cref="LevelBoardGrid"/> free of animation and objective logic.
    /// </summary>
    public sealed class BoardTileCollectCoordinator
    {
        readonly LevelBoardGrid _grid;
        TileCollectFly _collectFly;
        OrderRackHud _orderRackHud;
        CollectDestinationResolver _destinationResolver;

        LevelObjectiveSession _session;
        readonly RackDrainService _rackDrain = new RackDrainService();
        GameplayRulesContext _rules;
        BoardCell _pendingCollectCell;
        bool _tileCollectInFlight;

        public BoardTileCollectCoordinator(LevelBoardGrid grid) =>
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));

        public void SetPresentationRefs(TileCollectFly collectFly, OrderRackHud orderRackHud) =>
            SetPresentationRefs(collectFly, orderRackHud as IHudDestinationLayout);

        public void SetPresentationRefs(TileCollectFly collectFly, IHudDestinationLayout destinationLayout)
        {
            _collectFly = collectFly;
            _orderRackHud = destinationLayout as OrderRackHud;
            _destinationResolver = destinationLayout != null ? new CollectDestinationResolver(destinationLayout) : null;
        }

        public void SetGameplayRules(GameplayRulesContext rules) => _rules = rules;

        public void BindSession(LevelObjectiveSession session) => _session = session;

        public void CancelInFlightCollect() => _tileCollectInFlight = false;

        public void HandleTileClicked(BoardTileView view)
        {
            if (_tileCollectInFlight) return;
            if (_grid.PlayState == null || view == null || _session == null) return;
            if (_session.HasFailed || _session.HasWon) return;

            var x = view.GridX;
            var y = view.GridY;
            var l = view.LayerIndex;
            var boardCell = _grid.PlayState.GetCell(x, y, l);
            if (!IsClickable(x, y, l, boardCell)) return;

            if (_collectFly == null || !_collectFly.UseAnimation || _destinationResolver == null)
            {
                CollectTileInstant(view, x, y, l, boardCell);
                return;
            }

            if (!_session.TryPeekCollectDestination(view.Kind, out var destination, out _))
            {
                CollectTileInstant(view, x, y, l, boardCell);
                return;
            }

            if (!_destinationResolver.TryResolve(destination, out var targetRt))
            {
                CollectTileInstant(view, x, y, l, boardCell);
                return;
            }

            if (!_collectFly.WillAnimate(view, targetRt, _grid.BoardRoot))
            {
                CollectTileInstant(view, x, y, l, boardCell);
                return;
            }

            _pendingCollectCell = boardCell;
            _tileCollectInFlight = true;
            _grid.DetachTileForAnimatedCollect(view, x, y, l);

            _collectFly.Play(view, targetRt, _grid.BoardRoot, () => ApplyCollectAfterFlyAnimation());
        }

        bool IsClickable(int x, int y, int layer, BoardCell cell)
        {
            if (_rules?.Clickability != null)
                return _rules.Clickability.IsClickable(_grid.PlayState, x, y, layer, cell);
            return TileClickability.IsClickable(_grid.PlayState, x, y, layer);
        }

        void ApplyCollectAfterFlyAnimation()
        {
            if (_session == null)
            {
                EndTileCollectFlight();
                return;
            }

            var result = _session.TryCollectTile(_pendingCollectCell);
            LogCollectOutcome(result);

            if (result == TileCollectResult.OrderCompleted)
            {
                ProcessNextAnimatedRackDrainStep();
                return;
            }

            EndTileCollectFlight();
        }

        void CollectTileInstant(BoardTileView view, int x, int y, int l, BoardCell boardCell)
        {
            _tileCollectInFlight = true;
            if (_session == null)
            {
                EndTileCollectFlight();
                return;
            }

            var result = _session.TryCollectTile(boardCell);

            if (result == TileCollectResult.SessionInactive)
            {
                EndTileCollectFlight();
                return;
            }

            LogCollectOutcome(result);

            if (result == TileCollectResult.FailedRackFull)
            {
                EndTileCollectFlight();
                return;
            }

            if (ShouldRemoveFromBoard(boardCell))
                _grid.RemoveAndDestroyTile(view, x, y, l);

            if (result == TileCollectResult.OrderCompleted)
            {
                FinishRackDrainSynchronously();
                EndTileCollectFlight();
                return;
            }

            EndTileCollectFlight();
        }

        void LogCollectOutcome(TileCollectResult result)
        {
            if (result == TileCollectResult.LevelWon)
                Debug.Log("[BoardCollect] All orders completed — level won.");
            if (result == TileCollectResult.FailedRackFull)
                Debug.LogWarning("[BoardCollect] Rack full — level failed.");
        }

        void EndTileCollectFlight()
        {
            _grid.RefreshClickabilityVisuals();
            _tileCollectInFlight = false;
        }

        void FinishRackDrainSynchronously()
        {
            if (_session == null) return;
            while (_rackDrain.TryPeekStep(_session, out var i, out _, out _))
            {
                var r = _rackDrain.ApplyStepAt(_session, i);
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
            if (_collectFly == null || !_collectFly.UseAnimation || _orderRackHud == null || _destinationResolver == null || boardRoot == null)
            {
                FinishRackDrainSynchronously();
                EndTileCollectFlight();
                return;
            }

            while (_rackDrain.TryPeekStep(_session, out var rackIdx, out _, out var orderDestination))
            {
                if (!_orderRackHud.TryGetRackSlotImage(rackIdx, out var rackImg))
                {
                    var r = _rackDrain.ApplyStepAt(_session, rackIdx);
                    _session.NotifyStateChanged();
                    if (r == TileCollectResult.LevelWon)
                    {
                        Debug.Log("[BoardCollect] All orders completed — level won.");
                        EndTileCollectFlight();
                        return;
                    }

                    continue;
                }

                if (!_destinationResolver.TryResolve(orderDestination, out var targetRt))
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

            var applyResult = _rackDrain.ApplyStepAt(_session, rackIdx);
            _session.NotifyStateChanged();

            if (applyResult == TileCollectResult.LevelWon)
            {
                Debug.Log("[BoardCollect] All orders completed — level won.");
                EndTileCollectFlight();
                return;
            }

            if (applyResult == TileCollectResult.OrderCompleted)
            {
                ProcessNextAnimatedRackDrainStep();
                return;
            }

            ProcessNextAnimatedRackDrainStep();
        }

        bool ShouldRemoveFromBoard(BoardCell cell) =>
            _rules == null || _rules.CanRemoveFromBoard(cell);
    }
}
