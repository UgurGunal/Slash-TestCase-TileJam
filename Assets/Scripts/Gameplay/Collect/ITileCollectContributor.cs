using LevelData.Board;

namespace Gameplay.Collect
{
    /// <summary>
    /// Optional collect-side behavior hook (wildcard, bomb, double-slot, …).
    /// A tile behavior opts in by also implementing this interface; it is then resolved from the
    /// same <see cref="TileBehaviorCatalog"/>. Implementations MUST guard on <c>cell.BehaviorId</c>
    /// so they only act on their own tile type, and return false to fall through to the default handler.
    /// </summary>
    public interface ITileCollectContributor
    {
        bool TryHandleCollect(BoardCell cell, CollectSessionContext context, out TileCollectResult result);
    }
}
