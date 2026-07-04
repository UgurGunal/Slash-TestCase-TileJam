using System;
using System.Collections.Generic;
using Core;

namespace Gameplay
{
    public interface IGameplayEvent
    {
    }

    public readonly struct TileMatchedOrderEvent : IGameplayEvent
    {
        public TileKind Kind { get; }

        public TileMatchedOrderEvent(TileKind kind) => Kind = kind;
    }

    public readonly struct TileSentToRackEvent : IGameplayEvent
    {
        public TileKind Kind { get; }
        public int RackCountAfter { get; }

        public TileSentToRackEvent(TileKind kind, int rackCountAfter)
        {
            Kind = kind;
            RackCountAfter = rackCountAfter;
        }
    }

    public readonly struct OrderCompletedEvent : IGameplayEvent
    {
        public TileKind LastKind { get; }
        public bool FromRack { get; }

        public OrderCompletedEvent(TileKind lastKind, bool fromRack)
        {
            LastKind = lastKind;
            FromRack = fromRack;
        }
    }

    public readonly struct OrderSlotAdvancedEvent : IGameplayEvent
    {
        public int SlotIndex { get; }

        public OrderSlotAdvancedEvent(int slotIndex) => SlotIndex = slotIndex;
    }

    public readonly struct RackFullEvent : IGameplayEvent
    {
    }

    public readonly struct VictoryEvent : IGameplayEvent
    {
    }

    public interface IGameplayEventBus
    {
        void Publish<T>(T gameplayEvent) where T : IGameplayEvent;
        void Subscribe<T>(Action<T> handler) where T : IGameplayEvent;
        void Unsubscribe<T>(Action<T> handler) where T : IGameplayEvent;
    }

    public sealed class GameplayEventBus : IGameplayEventBus
    {
        readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Publish<T>(T gameplayEvent) where T : IGameplayEvent
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;
            for (var i = list.Count - 1; i >= 0; i--)
                ((Action<T>)list[i])?.Invoke(gameplayEvent);
        }

        public void Subscribe<T>(Action<T> handler) where T : IGameplayEvent
        {
            if (handler == null) return;
            var t = typeof(T);
            if (!_handlers.TryGetValue(t, out var list))
            {
                list = new List<Delegate>();
                _handlers[t] = list;
            }

            if (!list.Contains(handler))
                list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameplayEvent
        {
            if (handler == null) return;
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;
            list.Remove(handler);
        }
    }

    public sealed class NullGameplayEventBus : IGameplayEventBus
    {
        public static readonly NullGameplayEventBus Instance = new NullGameplayEventBus();

        public void Publish<T>(T gameplayEvent) where T : IGameplayEvent { }

        public void Subscribe<T>(Action<T> handler) where T : IGameplayEvent { }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameplayEvent { }
    }
}
