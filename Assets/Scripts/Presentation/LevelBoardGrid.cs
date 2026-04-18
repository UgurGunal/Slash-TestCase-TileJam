using System;
using System.Collections.Generic;
using Core;
using LevelData;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Board grid state (<see cref="PlayableBoardState"/>) and live <see cref="BoardTileView"/> instances.
    /// Spawning and clickability visuals only — no match/session rules.
    /// </summary>
    public sealed class LevelBoardGrid
    {
        readonly RectTransform _boardRoot;
        readonly Dictionary<(int x, int y, int layer), BoardTileView> _tiles = new Dictionary<(int x, int y, int layer), BoardTileView>();

        public LevelBoardGrid(RectTransform boardRoot) =>
            _boardRoot = boardRoot ?? throw new ArgumentNullException(nameof(boardRoot));

        public RectTransform BoardRoot => _boardRoot;
        public PlayableBoardState PlayState { get; private set; }

        public void BuildFromSpec(
            LevelBoardSpec spec,
            BoardTileView tilePrefab,
            LevelBoardVisualLayoutSettings visualLayoutOrNull,
            TileIconLibrary tileIconLibrary,
            Action<BoardTileView> onTileClicked)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (tilePrefab == null) throw new ArgumentNullException(nameof(tilePrefab));

            _tiles.Clear();
            PlayState = new PlayableBoardState(spec);

            var layout = visualLayoutOrNull != null
                ? visualLayoutOrNull
                : Resources.Load<LevelBoardVisualLayoutSettings>(LevelBoardVisualLayoutSettings.ResourcesLoadName);

            var cell = LevelBoardVisualLayoutSettings.ResolveCellSize(layout);
            var tileScale = LevelBoardVisualLayoutSettings.ResolveTileSizeInCellScale(layout);
            for (var l = 0; l < spec.Depth; l++)
            for (var y = spec.Height - 1; y >= 0; y--)
            for (var x = 0; x < spec.Width; x++)
            {
                if (!spec.TryGet(x, y, l, out var kind)) continue;

                var view = UnityEngine.Object.Instantiate(tilePrefab, _boardRoot);
                view.gameObject.name = $"Tile_L{l}_R{y}_C{x}";
                var pos = GridToAnchored(spec.Width, spec.Height, x, y, cell);
                view.Bind(kind, x, y, l, pos, cell, tileScale, tileIconLibrary);
                view.SetClickHandler(onTileClicked);
                _tiles[(x, y, l)] = view;
            }

            RefreshClickabilityVisuals();
        }

        /// <summary>Clears logical cell and removes the view from the map; does not destroy the view (board fly handles that).</summary>
        public void DetachTileForAnimatedCollect(BoardTileView view, int x, int y, int l)
        {
            PlayState?.Clear(x, y, l);
            _tiles.Remove((x, y, l));
            RefreshClickabilityVisuals();
        }

        /// <summary>Clears cell, removes mapping, destroys the tile GameObject, refreshes visuals.</summary>
        public void RemoveAndDestroyTile(BoardTileView view, int x, int y, int l)
        {
            PlayState?.Clear(x, y, l);
            _tiles.Remove((x, y, l));
            if (view != null)
                UnityEngine.Object.Destroy(view.gameObject);
            RefreshClickabilityVisuals();
        }

        public void RefreshClickabilityVisuals()
        {
            if (PlayState == null) return;
            foreach (var kv in _tiles)
            {
                var (x, y, l) = kv.Key;
                var clickable = TileClickability.IsClickable(PlayState, x, y, l);
                kv.Value.SetClickableVisual(clickable);
            }
        }

        static Vector2 GridToAnchored(int width, int height, int x, int y, Vector2 cell)
        {
            var ox = (x + 0.5f - width * 0.5f) * cell.x;
            var oy = (y + 0.5f - height * 0.5f) * cell.y;
            return new Vector2(ox, oy);
        }
    }
}
