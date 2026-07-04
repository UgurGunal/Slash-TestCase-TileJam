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

    /// <summary>One board cell: optional tile kind plus zero or more modifiers.</summary>
    public readonly struct BoardCell
    {
        static readonly IReadOnlyList<CellModifier> EmptyModifiers = Array.Empty<CellModifier>();

        public TileKind? Kind { get; }
        public IReadOnlyList<CellModifier> Modifiers { get; }

        public bool HasTile => Kind.HasValue;

        BoardCell(TileKind? kind, IReadOnlyList<CellModifier> modifiers)
        {
            Kind = kind;
            Modifiers = modifiers ?? EmptyModifiers;
        }

        public static BoardCell Empty => new BoardCell(null, EmptyModifiers);

        public static BoardCell FromKind(TileKind kind) => new BoardCell(kind, EmptyModifiers);

        public static BoardCell FromNullable(TileKind? kind) =>
            kind.HasValue ? FromKind(kind.Value) : Empty;
    }
}
