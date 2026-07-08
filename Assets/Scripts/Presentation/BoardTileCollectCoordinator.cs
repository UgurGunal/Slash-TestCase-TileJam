using System;
using System.Collections.Generic;
using Core;
using Gameplay;
using Gameplay.Collect;
using LevelData;
using LevelData.Board;
using Presentation.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Input → session collect rules, board ↔ HUD fly feedback, and rack→order drain orchestration.
    /// Supports concurrent in-flight collects with projected reservations and a FIFO input buffer.
    /// </summary>
    public sealed class BoardTileCollectCoordinator
    {
        struct ActiveFlight
        {
            public CollectReservation Reservation;
            public BoardTileView View;
        }

        struct PendingClick
        {
            public BoardTileView View;
            public int X;
            public int Y;
            public int Layer;
        }

        readonly LevelBoardGrid _grid;
        readonly List<ActiveFlight> _activeFlights = new List<ActiveFlight>();
        readonly Queue<PendingClick> _inputBuffer = new Queue<PendingClick>();
        readonly HashSet<BoardTileView> _bufferedViews = new HashSet<BoardTileView>();
        readonly RackDrainService _rackDrain = new RackDrainService();

        TileCollectFly _collectFly;
        OrderRackHud _orderRackHud;
        LevelObjectiveSession _session;
        GameplayRulesContext _rules;
        bool _drainStepAnimating;

        public BoardTileCollectCoordinator(LevelBoardGrid grid) =>
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));

        public void SetPresentationRefs(TileCollectFly collectFly, OrderRackHud orderRackHud)
        {
            _collectFly = collectFly;
            _orderRackHud = orderRackHud;
        }

        public void SetGameplayRules(GameplayRulesContext rules) => _rules = rules;

        public void BindSession(LevelObjectiveSession session) => _session = session;

        public void CancelInFlightCollect()
        {
            for (var i = _activeFlights.Count - 1; i >= 0; i--)
            {
                var flight = _activeFlights[i];
                _session?.CancelReservation(flight.Reservation);
                if (flight.View != null)
                    UnityEngine.Object.Destroy(flight.View.gameObject);
            }

            _activeFlights.Clear();
            _drainStepAnimating = false;
            ClearBuffer();
            _session?.CancelAllReservations();
            _grid.RefreshClickabilityVisuals();
        }

        public void HandleTileClicked(BoardTileView view)
        {
            if (_grid.PlayState == null || view == null || _session == null) return;
            if (_session.HasFailed || _session.HasWon) return;
            if (IsViewInFlight(view) || _bufferedViews.Contains(view)) return;

            var x = view.GridX;
            var y = view.GridY;
            var l = view.LayerIndex;
            var boardCell = _grid.PlayState.GetCell(x, y, l);
            if (!IsClickable(x, y, l, boardCell)) return;

            if (!_session.TryReserveCollect(view.Kind, out var reservation))
            {
                BufferClick(view, x, y, l);
                return;
            }

            BeginCollect(view, x, y, l, boardCell, reservation);
        }

        void BeginCollect(BoardTileView view, int x, int y, int l, BoardCell boardCell, CollectReservation reservation)
        {
            if (TryBeginAnimatedCollect(view, x, y, l, reservation))
                return;

            CompleteCollectInstant(view, x, y, l, boardCell, reservation);
        }

        bool TryBeginAnimatedCollect(BoardTileView view, int x, int y, int l, CollectReservation reservation)
        {
            if (_collectFly == null || !_collectFly.UseAnimation || _orderRackHud == null)
                return false;

            if (!TryResolveDestination(reservation.Destination, out var targetRt))
                return false;

            if (!_collectFly.WillAnimate(view, targetRt, _grid.BoardRoot))
                return false;

            _grid.DetachTileForAnimatedCollect(view, x, y, l);
            RegisterFlight(view, reservation);

            _collectFly.Play(view, targetRt, _grid.BoardRoot, () => OnBoardFlightArrived(view, reservation));
            return true;
        }

        void OnBoardFlightArrived(BoardTileView view, CollectReservation reservation)
        {
            UnregisterFlight(view);

            if (_session == null)
                return;

            var result = _session.CommitReservation(reservation);
            HandleCollectResult(result, startDrainOnOrderComplete: true);
        }

        void CompleteCollectInstant(BoardTileView view, int x, int y, int l, BoardCell boardCell, CollectReservation reservation)
        {
            if (_session == null)
            {
                _session?.CancelReservation(reservation);
                return;
            }

            var result = _session.CommitReservation(reservation);

            if (result == TileCollectResult.SessionInactive)
            {
                HandleSessionEnded();
                return;
            }

            LogCollectOutcome(result);

            if (result == TileCollectResult.FailedRackFull)
            {
                HandleSessionEnded();
                return;
            }

            if (ShouldRemoveFromBoard(boardCell))
                _grid.RemoveAndDestroyTile(view, x, y, l);

            if (result == TileCollectResult.OrderCompleted)
                FinishRackDrainSynchronously();

            PumpBuffer();
            _grid.RefreshClickabilityVisuals();
        }

        void HandleCollectResult(TileCollectResult result, bool startDrainOnOrderComplete)
        {
            if (result == TileCollectResult.SessionInactive)
            {
                HandleSessionEnded();
                return;
            }

            LogCollectOutcome(result);

            if (result == TileCollectResult.FailedRackFull)
            {
                HandleSessionEnded();
                return;
            }

            if (result == TileCollectResult.OrderCompleted && startDrainOnOrderComplete)
                TryStartNextDrainStep();
            else
                PumpBuffer();

            _grid.RefreshClickabilityVisuals();
        }

        void BufferClick(BoardTileView view, int x, int y, int l)
        {
            if (_bufferedViews.Contains(view) || IsViewInFlight(view))
                return;

            _inputBuffer.Enqueue(new PendingClick { View = view, X = x, Y = y, Layer = l });
            _bufferedViews.Add(view);
        }

        void PumpBuffer()
        {
            if (_session == null || _session.HasFailed || _session.HasWon)
            {
                ClearBuffer();
                return;
            }

            var attempts = _inputBuffer.Count;
            for (var n = 0; n < attempts && _inputBuffer.Count > 0; n++)
            {
                var pending = _inputBuffer.Dequeue();
                _bufferedViews.Remove(pending.View);

                if (pending.View == null)
                    continue;

                if (IsViewInFlight(pending.View))
                    continue;

                var boardCell = _grid.PlayState?.GetCell(pending.X, pending.Y, pending.Layer) ?? default;
                if (!IsClickable(pending.X, pending.Y, pending.Layer, boardCell))
                    continue;

                if (!_session.TryReserveCollect(pending.View.Kind, out var reservation))
                {
                    _inputBuffer.Enqueue(pending);
                    _bufferedViews.Add(pending.View);
                    break;
                }

                BeginCollect(pending.View, pending.X, pending.Y, pending.Layer, boardCell, reservation);
            }
        }

        void ClearBuffer()
        {
            _inputBuffer.Clear();
            _bufferedViews.Clear();
        }

        void HandleSessionEnded()
        {
            ClearBuffer();
            _grid.RefreshClickabilityVisuals();
        }

        void RegisterFlight(BoardTileView view, CollectReservation reservation) =>
            _activeFlights.Add(new ActiveFlight { View = view, Reservation = reservation });

        void UnregisterFlight(BoardTileView view)
        {
            for (var i = _activeFlights.Count - 1; i >= 0; i--)
            {
                if (_activeFlights[i].View != view) continue;
                _activeFlights.RemoveAt(i);
                return;
            }
        }

        bool IsViewInFlight(BoardTileView view)
        {
            for (var i = 0; i < _activeFlights.Count; i++)
            {
                if (_activeFlights[i].View == view)
                    return true;
            }

            return false;
        }

        bool IsClickable(int x, int y, int layer, BoardCell cell)
        {
            if (_rules?.Clickability != null)
                return _rules.Clickability.IsClickable(_grid.PlayState, x, y, layer, cell);
            return TileClickability.IsClickable(_grid.PlayState, x, y, layer);
        }

        void LogCollectOutcome(TileCollectResult result)
        {
            if (result == TileCollectResult.LevelWon)
                Debug.Log("[BoardCollect] All orders completed — level won.");
            if (result == TileCollectResult.FailedRackFull)
                Debug.LogWarning("[BoardCollect] Rack full — level failed.");
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

        void TryStartNextDrainStep()
        {
            if (_session == null)
                return;

            if (_drainStepAnimating)
                return;

            var boardRoot = _grid.BoardRoot;
            var useDrainAnimation = _collectFly != null && _collectFly.UseAnimation && _orderRackHud != null && boardRoot != null;

            while (_rackDrain.TryPeekStep(_session, out var rackIdx, out var kind, out var orderDestination))
            {
                if (!useDrainAnimation)
                {
                    FinishRackDrainSynchronously();
                    PumpBuffer();
                    _grid.RefreshClickabilityVisuals();
                    return;
                }

                if (!_session.TryReserveRackDrain(kind, orderDestination, out var drainReservation))
                {
                    var syncResult = _rackDrain.ApplyStepAt(_session, rackIdx);
                    _session.NotifyStateChanged();
                    LogCollectOutcome(syncResult);
                    if (syncResult == TileCollectResult.LevelWon || syncResult == TileCollectResult.FailedRackFull)
                    {
                        HandleSessionEnded();
                        return;
                    }

                    continue;
                }

                if (!TryGetRackSlotImage(rackIdx, out var rackImg))
                {
                    _session.CancelReservation(drainReservation);
                    var syncResult = _rackDrain.ApplyStepAt(_session, rackIdx);
                    _session.NotifyStateChanged();
                    LogCollectOutcome(syncResult);
                    if (syncResult == TileCollectResult.LevelWon || syncResult == TileCollectResult.FailedRackFull)
                    {
                        HandleSessionEnded();
                        return;
                    }

                    continue;
                }

                if (!TryResolveDestination(orderDestination, out var targetRt))
                {
                    _session.CancelReservation(drainReservation);
                    FinishRackDrainSynchronously();
                    PumpBuffer();
                    _grid.RefreshClickabilityVisuals();
                    return;
                }

                var rackRt = rackImg.rectTransform;
                if (!_collectFly.WillAnimateUiRect(rackRt, targetRt, boardRoot))
                {
                    _session.CancelReservation(drainReservation);
                    FinishRackDrainSynchronously();
                    PumpBuffer();
                    _grid.RefreshClickabilityVisuals();
                    return;
                }

                _drainStepAnimating = true;
                _collectFly.PlayUiDuplicate(rackRt, targetRt, boardRoot, () => OnDrainFlightArrived(rackIdx, drainReservation));
                rackImg.enabled = false;
                return;
            }

            _session.NotifyStateChanged();
            PumpBuffer();
            _grid.RefreshClickabilityVisuals();
        }

        void OnDrainFlightArrived(int rackIdx, CollectReservation drainReservation)
        {
            _drainStepAnimating = false;

            if (_session == null)
                return;

            _session.CancelReservation(drainReservation);
            var applyResult = _rackDrain.ApplyStepAt(_session, rackIdx);
            _session.NotifyStateChanged();
            LogCollectOutcome(applyResult);

            if (applyResult == TileCollectResult.LevelWon || applyResult == TileCollectResult.FailedRackFull)
            {
                HandleSessionEnded();
                return;
            }

            TryStartNextDrainStep();
        }

        bool ShouldRemoveFromBoard(BoardCell cell) =>
            _rules == null || _rules.CanRemoveFromBoard(cell);

        bool TryResolveDestination(TileCollectDestination destination, out RectTransform rect)
        {
            rect = null;
            var layout = _orderRackHud?.DestinationLayout;
            if (layout == null) return false;
            return new CollectDestinationResolver(layout).TryResolve(destination, out rect);
        }

        bool TryGetRackSlotImage(int rackIdx, out Image rackImg)
        {
            rackImg = null;
            var layout = _orderRackHud?.DestinationLayout;
            return layout != null && layout.TryGetRackSlotImage(rackIdx, out rackImg);
        }
    }
}
