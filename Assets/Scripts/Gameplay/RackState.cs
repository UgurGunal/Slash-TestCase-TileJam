using Core;

namespace Gameplay
{
    public sealed class RackState
    {
        readonly TileKind?[] _slots;
        int _count;

        public RackState()
        {
            _slots = new TileKind?[GameConstants.RackCapacity];
        }

        public int Count => _count;
        public bool IsFull => _count >= GameConstants.RackCapacity;

        public TileKind? GetSlot(int index)
        {
            if ((uint)index >= (uint)GameConstants.RackCapacity) return null;
            return index < _count ? _slots[index] : null;
        }

        public bool TryAdd(TileKind kind)
        {
            if (_count >= GameConstants.RackCapacity) return false;
            _slots[_count++] = kind;
            return true;
        }

        public TileKind RemoveAt(int index)
        {
            var kind = _slots[index].Value;
            for (var j = index; j < _count - 1; j++)
                _slots[j] = _slots[j + 1];
            _count--;
            if ((uint)_count < (uint)_slots.Length)
                _slots[_count] = null;
            return kind;
        }
    }
}
