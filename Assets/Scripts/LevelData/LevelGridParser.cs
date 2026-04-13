using System;
using System.Text;
using Newtonsoft.Json;
using Core;

namespace LevelData
{
    public static class LevelGridParser
    {
        public static bool TryParseJson(string json, out LevelBoardSpec spec, out string error)
        {
            spec = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON is empty.";
                return false;
            }

            Matrix3DLevelJsonDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<Matrix3DLevelJsonDto>(json);
            }
            catch (Exception e)
            {
                error = $"Parse error: {e.Message}";
                return false;
            }

            return TryBuildSpec(dto, out spec, out error);
        }

        public static bool TryBuildSpec(Matrix3DLevelJsonDto dto, out LevelBoardSpec spec, out string error)
        {
            spec = null;
            error = null;
            if (dto == null)
            {
                error = "Root object is null.";
                return false;
            }

            if (dto.Width <= 0 || dto.Height <= 0 || dto.Depth <= 0)
            {
                error = "width, height, and depth must be positive.";
                return false;
            }

            if (dto.Matrix3D == null)
            {
                error = "matrix3D is missing.";
                return false;
            }

            if (dto.Matrix3D.Count != dto.Depth)
            {
                error = $"matrix3D layer count ({dto.Matrix3D.Count}) must equal depth ({dto.Depth}).";
                return false;
            }

            var width = dto.Width;
            var height = dto.Height;
            var depth = dto.Depth;
            var cells = new TileKind?[width * height * depth];
            System.Array.Clear(cells, 0, cells.Length);

            for (var l = 0; l < depth; l++)
            {
                var layer = dto.Matrix3D[l];
                if (layer == null || layer.Count != height)
                {
                    error = $"Layer {l}: expected {height} rows (height).";
                    return false;
                }

                for (var y = 0; y < height; y++)
                {
                    var row = layer[y];
                    if (row == null || row.Count != width)
                    {
                        error = $"Layer {l}, row {y}: expected {width} columns (width).";
                        return false;
                    }

                    for (var x = 0; x < width; x++)
                    {
                        var v = row[x];
                        if (!TryMapCell(v, out var kind, out var cellError))
                        {
                            error = $"Layer {l}, cell ({x},{y}): {cellError}";
                            return false;
                        }

                        cells[Index(width, height, x, y, l)] = kind;
                    }
                }
            }

            spec = new LevelBoardSpec(width, height, depth, cells);
            if (!LevelLayoutRules.Validate(spec, out var layoutError))
            {
                spec = null;
                error = layoutError;
                return false;
            }

            return true;
        }

        static bool TryMapCell(int value, out TileKind? kind, out string cellError)
        {
            kind = null;
            cellError = null;
            if (value == -1)
                return true;

            if (value >= 0 && value < GameConstants.PlayableTileKindCount)
            {
                kind = (TileKind)value;
                return true;
            }

            cellError = $"expected -1 or 0..{GameConstants.PlayableTileKindCount - 1}, got {value}.";
            return false;
        }

        static int Index(int w, int h, int x, int y, int layer) =>
            layer * (w * h) + y * w + x;

        public static string BuildValidationReport(LevelBoardSpec spec)
        {
            if (spec == null) return "Spec is null.";
            var sb = new StringBuilder();
            sb.AppendLine($"Board {spec.Width}x{spec.Height}, depth {spec.Depth}");
            var count = 0;
            for (var l = 0; l < spec.Depth; l++)
            for (var y = 0; y < spec.Height; y++)
            for (var x = 0; x < spec.Width; x++)
            {
                if (spec.TryGet(x, y, l, out _)) count++;
            }

            sb.AppendLine($"Tiles placed: {count}");
            return sb.ToString();
        }
    }
}
