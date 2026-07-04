using Core;
using LevelData.Board;

namespace LevelData
{
    /// <summary>Mutable occupancy grid for play; cloned from a validated <see cref="LevelBoardSpec"/>.</summary>
    public sealed class PlayableBoardState
    {
        readonly BoardCell[] _cells;

        public PlayableBoardState(LevelBoardSpec spec)
        {
            Width = spec.Width;
            Height = spec.Height;
            Depth = spec.Depth;
            _cells = new BoardCell[Width * Height * Depth];
            for (var l = 0; l < Depth; l++)
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                _cells[Index(x, y, l)] = BoardCell.FromNullable(spec.GetNullable(x, y, l));
        }

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        public bool HasTile(int x, int y, int layer) =>
            TryGetCell(x, y, layer, out var cell) && cell.HasTile;

        public BoardCell GetCell(int x, int y, int layer) =>
            TryGetCell(x, y, layer, out var cell) ? cell : BoardCell.Empty;

        public void Clear(int x, int y, int layer)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)layer >= (uint)Depth) return;
            _cells[Index(x, y, layer)] = BoardCell.Empty;
        }

        bool TryGetCell(int x, int y, int layer, out BoardCell cell)
        {
            cell = BoardCell.Empty;
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)layer >= (uint)Depth)
                return false;
            cell = _cells[Index(x, y, layer)];
            return true;
        }

        int Index(int x, int y, int layer) =>
            layer * (Width * Height) + y * Width + x;
    }
}
