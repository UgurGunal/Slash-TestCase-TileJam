using TileMatch.Core;

namespace TileMatch.LevelData
{
    /// <summary>Validated board layout: optional tile per (x, y, layer).</summary>
    public sealed class LevelBoardSpec
    {
        readonly TileKind?[] _cells;

        public LevelBoardSpec(int width, int height, int depth, TileKind?[] cells)
        {
            Width = width;
            Height = height;
            Depth = depth;
            _cells = cells;
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

        int Index(int x, int y, int layer) =>
            layer * (Width * Height) + y * Width + x;
    }
}
