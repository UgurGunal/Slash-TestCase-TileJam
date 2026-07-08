using Core;
using LevelData;

namespace Gameplay
{
    /// <summary>Read-only projection of one active order row for HUD display.</summary>
    public readonly struct ObjectiveHudRowView
    {
        readonly OrderSpec _order;
        readonly bool[] _fulfilled;
        readonly int _levelOrderIndex;

        internal ObjectiveHudRowView(int levelOrderIndex, OrderSpec order, bool[] fulfilled)
        {
            _levelOrderIndex = levelOrderIndex;
            _order = order;
            _fulfilled = fulfilled;
        }

        public int LevelOrderIndex => _levelOrderIndex;
        public int IconCount => _order?.Length ?? 0;
        public bool IsActive => _order != null;

        public TileKind GetIcon(int index) => _order.GetIcon(index);

        public bool IsFulfilled(int index) =>
            _fulfilled != null && (uint)index < (uint)_fulfilled.Length && _fulfilled[index];
    }
}
