using System;
using Core;
using Gameplay;

namespace Presentation
{
    /// <summary>Bridges <see cref="IGameplayEventBus"/> events to <see cref="ITileMatchObserver"/> subscribers.</summary>
    public sealed class GameplayEventObserverBridge : ITileMatchObserver
    {
        IGameplayEventBus _bus;
        GamePhase _phase = GamePhase.Idle;

        public void Bind(IGameplayEventBus bus)
        {
            Unbind();
            _bus = bus;
            if (_bus == null) return;

            _bus.Subscribe<VictoryEvent>(_ => NotifyPhase(GamePhase.Won));
            _bus.Subscribe<RackFullEvent>(_ => NotifyPhase(GamePhase.LostRackFull));
            _bus.Subscribe<TileMatchedOrderEvent>(e => OnTileMatchedOrder(e.Kind));
            _bus.Subscribe<TileSentToRackEvent>(e => OnTileSentToRack(e.Kind, e.RackCountAfter));
        }

        public void Unbind()
        {
            _bus = null;
        }

        public void SetPlaying()
        {
            _phase = GamePhase.Playing;
            OnPhaseChanged(_phase);
        }

        void NotifyPhase(GamePhase phase)
        {
            _phase = phase;
            OnPhaseChanged(_phase);
        }

        public event Action<GamePhase> PhaseChanged;
        public event Action<TileKind> TileMatched;
        public event Action<TileKind, int> TileSentToRack;

        public void OnPhaseChanged(GamePhase phase) => PhaseChanged?.Invoke(phase);

        public void OnOrderProgress(int orderIndex, int filledSlotsInOrder, bool hasNextRequirement, TileKind nextRequired) { }

        public void OnTileMatchedOrder(TileKind kind) => TileMatched?.Invoke(kind);

        public void OnTileSentToRack(TileKind kind, int rackCountAfter) => TileSentToRack?.Invoke(kind, rackCountAfter);

        public void OnRackFull() => NotifyPhase(GamePhase.LostRackFull);

        public void OnVictory() => NotifyPhase(GamePhase.Won);
    }
}
