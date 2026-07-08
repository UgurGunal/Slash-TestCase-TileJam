using Core;
using LevelData.Board;

namespace LevelData
{
    /// <summary>Validated board layout: optional tile kind + behavior id per (x, y, layer).</summary>
    public sealed class LevelBoardSpec
    {
        readonly TileKind?[] _cells;
        readonly string[] _behaviorIds;

        public LevelBoardSpec(int width, int height, int depth, TileKind?[] cells, string[] behaviorIds = null)
        {
            Width = width;
            Height = height;
            Depth = depth;
            _cells = cells;
            _behaviorIds = behaviorIds;
        }

        /// <summary>All cells empty (<c>null</c>).</summary>
        public static LevelBoardSpec CreateEmpty(int width, int height, int depth)
        {
            var cells = new TileKind?[width * height * depth];
            return new LevelBoardSpec(width, height, depth, cells);
        }

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        public int TotalCells => Width * Height * Depth;

        public bool TryGet(int x, int y, int layer, out TileKind kind)
        {
            kind = TileKind.None;
            var v = GetNullable(x, y, layer);
            if (!v.HasValue) return false;
            kind = v.Value;
            return true;
        }

        public TileKind? GetNullable(int x, int y, int layer)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)layer >= (uint)Depth)
                return null;
            return _cells[Index(x, y, layer)];
        }

        /// <summary>Behavior id at the cell, or <see cref="BoardCell.StandardBehaviorId"/> when unset.</summary>
        public string GetBehaviorId(int x, int y, int layer)
        {
            if (_behaviorIds == null) return BoardCell.StandardBehaviorId;
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)layer >= (uint)Depth)
                return BoardCell.StandardBehaviorId;
            var id = _behaviorIds[Index(x, y, layer)];
            return string.IsNullOrWhiteSpace(id) ? BoardCell.StandardBehaviorId : id;
        }

        /// <summary>Full cell (kind + behavior) at the position, or <see cref="BoardCell.Empty"/> when no tile.</summary>
        public BoardCell GetBoardCell(int x, int y, int layer) =>
            BoardCell.FromNullable(GetNullable(x, y, layer), GetBehaviorId(x, y, layer));

        int Index(int x, int y, int layer) =>
            layer * (Width * Height) + y * Width + x;
    }
}
