namespace LevelData.Board
{
    /// <summary>
    /// A single tile behavior (standard, locked, ice, bomb, …). One class per behavior.
    /// Covers the engine-free rule seams: clickability and board removal.
    /// A behavior that also needs custom collect logic additionally implements
    /// <c>Gameplay.Collect.ITileCollectContributor</c> (resolved by the same catalog).
    /// </summary>
    public interface ITileBehavior
    {
        /// <summary>Stable id matched against <see cref="BoardCell.BehaviorId"/> (e.g. "standard", "locked").</summary>
        string Id { get; }

        /// <summary>Extra clickability gate on top of the base rules. Return false to block clicking this cell.</summary>
        bool IsClickable(PlayableBoardState board, int x, int y, int layer, BoardCell cell);

        /// <summary>Whether a collected tile of this behavior may be removed from the board grid.</summary>
        bool CanRemoveFromBoard(BoardCell cell);
    }

    /// <summary>
    /// Default behavior: always clickable (base rules still apply) and removable.
    /// New behaviors inherit this and override only the seam they care about.
    /// </summary>
    public class StandardTileBehavior : ITileBehavior
    {
        public static readonly StandardTileBehavior Instance = new StandardTileBehavior();

        public virtual string Id => BoardCell.StandardBehaviorId;

        public virtual bool IsClickable(PlayableBoardState board, int x, int y, int layer, BoardCell cell) => true;

        public virtual bool CanRemoveFromBoard(BoardCell cell) => true;
    }
}
