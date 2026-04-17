using UnityEngine;

namespace Presentation
{
    /// <summary>Editable layout for the level grid: assign on <see cref="LevelBoardLoader"/> or place at <c>Resources/{ResourcesLoadName}</c>.</summary>
    [CreateAssetMenu(menuName = "Level Board Visual Layout", fileName = "LevelBoardVisualLayout", order = 0)]
    public sealed class LevelBoardVisualLayoutSettings : ScriptableObject
    {
        /// <summary>Load via <c>Resources.Load&lt;LevelBoardVisualLayoutSettings&gt;("{ResourcesLoadName}")</c> (no extension).</summary>
        public const string ResourcesLoadName = "LevelBoardVisualLayout";

        /// <summary>Used when no asset is available at runtime.</summary>
        public static class Fallback
        {
            public const float DefaultCellWidth = 80f;
            public const float DefaultCellHeight = 100f;
            public const float TileSizeInCellScale = 0.92f;
        }

        [Header("Canvas")]
        [Tooltip("Grid spacing and tile stride width. If ≤ 0, " + nameof(Fallback.DefaultCellWidth) + " is used.")]
        [SerializeField] float cellWidth = Fallback.DefaultCellWidth;

        [Tooltip("Grid spacing and tile stride height. If ≤ 0, " + nameof(Fallback.DefaultCellHeight) + " is used.")]
        [SerializeField] float cellHeight = Fallback.DefaultCellHeight;

        [Tooltip("Tile RectTransform size as a fraction of the cell (padding between neighbours).")]
        [SerializeField] [Range(0.01f, 1f)] float tileSizeInCellScale = Fallback.TileSizeInCellScale;

        public Vector2 EffectiveCellSize => new Vector2(
            cellWidth > 0f ? cellWidth : Fallback.DefaultCellWidth,
            cellHeight > 0f ? cellHeight : Fallback.DefaultCellHeight);

        public float EffectiveTileSizeInCellScale => Mathf.Clamp(tileSizeInCellScale, 0.01f, 1f);

        public static Vector2 ResolveCellSize(LevelBoardVisualLayoutSettings asset) =>
            asset != null ? asset.EffectiveCellSize : new Vector2(Fallback.DefaultCellWidth, Fallback.DefaultCellHeight);

        public static float ResolveTileSizeInCellScale(LevelBoardVisualLayoutSettings asset) =>
            asset != null ? asset.EffectiveTileSizeInCellScale : Fallback.TileSizeInCellScale;

        void OnValidate() =>
            tileSizeInCellScale = Mathf.Clamp(tileSizeInCellScale, 0.01f, 1f);
    }
}
