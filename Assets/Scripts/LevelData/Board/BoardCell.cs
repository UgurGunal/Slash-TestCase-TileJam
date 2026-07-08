using Core;

namespace LevelData.Board
{
    /// <summary>One board cell: optional tile kind and behavior id.</summary>
    public readonly struct BoardCell
    {
        public const string StandardBehaviorId = "standard";

        public TileKind? Kind { get; }
        public string BehaviorId { get; }

        public bool HasTile => Kind.HasValue;

        BoardCell(TileKind? kind, string behaviorId)
        {
            Kind = kind;
            BehaviorId = string.IsNullOrWhiteSpace(behaviorId) ? StandardBehaviorId : behaviorId;
        }

        public static BoardCell Empty => new BoardCell(null, StandardBehaviorId);

        public static BoardCell FromKind(TileKind kind, string behaviorId = StandardBehaviorId) =>
            new BoardCell(kind, behaviorId);

        public static BoardCell FromNullable(TileKind? kind, string behaviorId = StandardBehaviorId) =>
            kind.HasValue ? FromKind(kind.Value, behaviorId) : Empty;
    }
}
