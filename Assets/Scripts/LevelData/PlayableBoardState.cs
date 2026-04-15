using Core;

namespace LevelData
{
    /// <summary>Mutable occupancy grid for play; cloned from a validated <see cref="LevelBoardSpec"/>.</summary>
    public sealed class PlayableBoardState
    {
        readonly TileKind?[] _cells;

        public PlayableBoardState(LevelBoardSpec spec)
        {
            Width = spec.Width;
            Height = spec.Height;
            Depth = spec.Depth;
            _cells = new TileKind?[Width * Height * Depth];
            for (var l = 0; l < Depth; l++)
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                _cells[Index(x, y, l)] = spec.GetNullable(x, y, l);
        }

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        public bool HasTile(int x, int y, int layer) =>
            (uint)x < (uint)Width && (uint)y < (uint)Height && (uint)layer < (uint)Depth &&
            _cells[Index(x, y, layer)].HasValue;

        public void Clear(int x, int y, int layer)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)layer >= (uint)Depth) return;
            _cells[Index(x, y, layer)] = null;
        }

        int Index(int x, int y, int layer) =>
            layer * (Width * Height) + y * Width + x;
    }
}
