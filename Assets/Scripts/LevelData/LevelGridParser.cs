using System;
using System.Collections.Generic;
using System.Text;
using Core;
using Newtonsoft.Json;

namespace LevelData
{
    public static class LevelGridParser
    {
        public static bool TryParseJson(string json, out LevelDefinition definition, out string error)
        {
            definition = null;
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

            return TryBuildLevelDefinition(dto, out definition, out error);
        }

        public static bool TryParseJson(string json, out LevelBoardSpec spec, out string error)
        {
            if (!TryParseJson(json, out LevelDefinition definition, out error))
            {
                spec = null;
                return false;
            }

            spec = definition.Board;
            return true;
        }

        public static bool TryBuildSpec(Matrix3DLevelJsonDto dto, out LevelBoardSpec spec, out string error)
        {
            if (!TryBuildLevelDefinition(dto, out var definition, out error))
            {
                spec = null;
                return false;
            }

            spec = definition.Board;
            return true;
        }

        public static bool TryBuildLevelDefinition(Matrix3DLevelJsonDto dto, out LevelDefinition definition, out string error)
        {
            definition = null;
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

            var spec = new LevelBoardSpec(width, height, depth, cells);
            if (!LevelLayoutRules.Validate(spec, out var layoutError))
            {
                error = layoutError;
                return false;
            }

            if (!TryParseOrders(dto, spec, out var ordersSpec, out error))
                return false;

            definition = new LevelDefinition(spec, ordersSpec);
            return true;
        }

        static bool TryParseOrders(Matrix3DLevelJsonDto dto, LevelBoardSpec spec, out LevelOrdersSpec ordersSpec, out string error)
        {
            ordersSpec = null;
            error = null;
            if (dto.Orders == null || dto.Orders.Count == 0)
            {
                error = "orders is missing or empty. Add \"orders\": [[a,b,…], [c,d], …] — each row is one order (≥1 tile id per row, 0..14).";
                return false;
            }

            var tileCount = CountTilesOnBoard(spec);
            var list = new List<OrderSpec>(dto.Orders.Count);
            var sumIcons = 0;

            for (var i = 0; i < dto.Orders.Count; i++)
            {
                var row = dto.Orders[i];
                if (row == null || row.Count < 1)
                {
                    error = $"orders[{i}] must be a non-empty array of tile kinds.";
                    return false;
                }

                var kinds = new TileKind[row.Count];
                for (var k = 0; k < row.Count; k++)
                {
                    if (!TryMapOrderIcon(row[k], out kinds[k], out var ek))
                    {
                        error = $"orders[{i}][{k}]: {ek}";
                        return false;
                    }
                }

                list.Add(new OrderSpec(kinds));
                sumIcons += kinds.Length;
            }

            if (tileCount != sumIcons)
            {
                error =
                    $"Board has {tileCount} tiles but orders define {sumIcons} icons total (sum of each order row length). These must match.";
                return false;
            }

            ordersSpec = new LevelOrdersSpec(list);
            return true;
        }

        static int CountTilesOnBoard(LevelBoardSpec spec)
        {
            var n = 0;
            for (var l = 0; l < spec.Depth; l++)
            for (var y = 0; y < spec.Height; y++)
            for (var x = 0; x < spec.Width; x++)
            {
                if (spec.TryGet(x, y, l, out _)) n++;
            }

            return n;
        }

        static bool TryMapOrderIcon(int value, out TileKind kind, out string err)
        {
            kind = TileKind.None;
            err = null;
            if (value >= 0 && value < GameConstants.PlayableTileKindCount)
            {
                kind = (TileKind)value;
                return true;
            }

            err = $"expected tile kind 0..{GameConstants.PlayableTileKindCount - 1}, got {value}.";
            return false;
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
