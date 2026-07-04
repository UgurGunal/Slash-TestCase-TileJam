using System;
using System.Collections.Generic;
using Core;

namespace LevelData.Board
{
    /// <summary>Future blocker/modifier identity (e.g. ice, chain). Empty list means no modifiers.</summary>
    public readonly struct CellModifier
    {
        public string Id { get; }

        public CellModifier(string id) => Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    /// <summary>One board cell: optional tile kind, behavior id, and zero or more modifiers.</summary>
    public readonly struct BoardCell
    {
        public const string StandardBehaviorId = "standard";

        static readonly IReadOnlyList<CellModifier> EmptyModifiers = Array.Empty<CellModifier>();

        public TileKind? Kind { get; }
        public string BehaviorId { get; }
        public IReadOnlyList<CellModifier> Modifiers { get; }

        public bool HasTile => Kind.HasValue;

        BoardCell(TileKind? kind, string behaviorId, IReadOnlyList<CellModifier> modifiers)
        {
            Kind = kind;
            BehaviorId = string.IsNullOrWhiteSpace(behaviorId) ? StandardBehaviorId : behaviorId;
            Modifiers = modifiers ?? EmptyModifiers;
        }

        public static BoardCell Empty => new BoardCell(null, StandardBehaviorId, EmptyModifiers);

        public static BoardCell FromKind(TileKind kind, string behaviorId = StandardBehaviorId) =>
            new BoardCell(kind, behaviorId, EmptyModifiers);

        public static BoardCell FromNullable(TileKind? kind, string behaviorId = StandardBehaviorId) =>
            kind.HasValue ? FromKind(kind.Value, behaviorId) : Empty;
    }
}
