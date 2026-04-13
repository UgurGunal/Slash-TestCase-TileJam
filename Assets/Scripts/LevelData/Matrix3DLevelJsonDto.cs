using System.Collections.Generic;
using Newtonsoft.Json;

namespace LevelData
{
    /// <summary>
    /// JSON root: board size plus <c>matrix3D[layer][row][column]</c>.
    /// <c>matrix3D[0]</c> = bottom layer; last = top. Row 0 = bottom (y = 0), column 0 = left (x = 0).
    /// Cells: <c>-1</c> = empty; <c>0..14</c> = <see cref="Core.TileKind"/> (0 = Type0 … 14 = Type14).
    /// </summary>
    public sealed class Matrix3DLevelJsonDto
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("depth")]
        public int Depth { get; set; }

        [JsonProperty("matrix3D")]
        public List<List<List<int>>> Matrix3D { get; set; }
    }
}
