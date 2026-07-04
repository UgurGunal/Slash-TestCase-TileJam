using LevelData;

namespace LevelData.Board
{
    /// <summary>Extra clickability rules contributed by a tile behavior (e.g. locked until unlocked).</summary>
    public interface ITileClickabilityContributor
    {
        bool IsClickable(PlayableBoardState board, int x, int y, int layer, BoardCell cell);
    }

    /// <summary>Whether a collected tile may be removed from the board grid.</summary>
    public interface ITileRemovalPolicy
    {
        bool CanRemoveFromBoard(BoardCell cell);
    }
}
