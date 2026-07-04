using LevelData.Board;

namespace Gameplay.Collect
{
    /// <summary>Optional collect-side behavior hook (wildcard, double-slot, etc.).</summary>
    public interface ITileCollectContributor
    {
        bool TryHandleCollect(BoardCell cell, CollectSessionContext context, out TileCollectResult result);
    }
}
