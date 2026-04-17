using System.Collections.Generic;
using System.Text;
using Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Level editor scene UI: builds 15 palette entries from <see cref="BoardTileView"/> prefab (Type0…Type14),
    /// and appends clicked kinds to a draft order row. Wire optional UnityEvents for custom tooling.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LevelEditorOrderAuthoringPanel : MonoBehaviour
    {
        [Header("Palette (15 kinds)")]
        [SerializeField] BoardTileView tilePrefab;
        [SerializeField] RectTransform paletteRow;
        [SerializeField] TileIconLibrary iconLibrary;
        [Tooltip("Logical cell size passed to Bind (stride between tile centers ≈ this + layout spacing).")]
        [SerializeField] Vector2 paletteCellSize = new Vector2(88f, 100f);
        [Tooltip("Uniform scale on each palette tile root after layout (visual size in slot). Slot size is unchanged.")]
        [SerializeField] [Range(0.05f, 1.5f)] float paletteTileScale = 0.92f;
        [SerializeField] float paletteLayoutSpacing = 6f;
        [Tooltip("Palette wraps into horizontal lines under this parent; each line has at most this many tiles.")]
        [SerializeField] [Range(1, 15)] int paletteMaxTilesPerRow = 8;
        [Tooltip("Vertical gap between palette lines.")]
        [SerializeField] float paletteRowSpacing = 6f;
        [Tooltip("Nudge all palette tiles from the parent’s upper-left: +X = right, +Y = down (pixels).")]
        [SerializeField] Vector2 paletteTilesPosition;

        [Header("Draft order strip")]
        [SerializeField] RectTransform draftRow;
        [SerializeField] Vector2 draftCellSize = new Vector2(72f, 90f);
        [Tooltip("Uniform scale on each draft tile root (visual size in slot). Slot size is unchanged.")]
        [SerializeField] [Range(0.05f, 1.5f)] float draftTileScale = 0.92f;
        [SerializeField] float draftLayoutSpacing = 4f;
        [Tooltip("Draft icons wrap into lines of at most this many tiles.")]
        [SerializeField] [Range(1, 32)] int draftMaxTilesPerRow = 8;
        [Tooltip("Vertical gap between draft lines.")]
        [SerializeField] float draftRowSpacing = 4f;
        [Tooltip("Nudge all draft tiles from the parent’s upper-left: +X = right, +Y = down (pixels).")]
        [SerializeField] Vector2 draftTilesPosition;

        [Header("Events")]
        [SerializeField] UnityEvent<int> onPaletteTileKindClicked;
        [SerializeField] UnityEvent onDraftChanged;

        readonly List<TileKind> _draft = new List<TileKind>();

        Object _lastPrefab;
        RectTransform _lastPaletteRow;
        RectTransform _lastDraftRow;
        Vector2 _lastPaletteCell;
        Vector2 _lastDraftCell;
        float _lastPaletteTileInCell;
        float _lastDraftTileInCell;
        Vector2 _lastPaletteTilesPosition;
        Vector2 _lastDraftTilesPosition;
        float _lastPaletteSpacing;
        float _lastDraftSpacing;
        int _lastPaletteMaxPerRow;
        int _lastDraftMaxPerRow;
        float _lastPaletteRowSpacing;
        float _lastDraftRowSpacing;
        bool _rebuildQueued = true;

        public IReadOnlyList<TileKind> DraftOrder => _draft;

        void OnEnable() => _rebuildQueued = true;

        void OnValidate()
        {
            paletteCellSize = new Vector2(Mathf.Max(1f, paletteCellSize.x), Mathf.Max(1f, paletteCellSize.y));
            draftCellSize = new Vector2(Mathf.Max(1f, draftCellSize.x), Mathf.Max(1f, draftCellSize.y));
            paletteLayoutSpacing = Mathf.Max(0f, paletteLayoutSpacing);
            draftLayoutSpacing = Mathf.Max(0f, draftLayoutSpacing);
            paletteRowSpacing = Mathf.Max(0f, paletteRowSpacing);
            draftRowSpacing = Mathf.Max(0f, draftRowSpacing);
            paletteMaxTilesPerRow = Mathf.Clamp(paletteMaxTilesPerRow, 1, GameConstants.PlayableTileKindCount);
            draftMaxTilesPerRow = Mathf.Clamp(draftMaxTilesPerRow, 1, 32);
            _rebuildQueued = true;
        }

        void Update()
        {
            if (_rebuildQueued)
            {
                _rebuildQueued = false;
                RebuildAll(force: true);
                return;
            }

            RebuildAll(force: false);
        }

        void RebuildAll(bool force)
        {
            if (tilePrefab == null || paletteRow == null || draftRow == null)
                return;

            if (!force &&
                _lastPrefab == tilePrefab &&
                _lastPaletteRow == paletteRow &&
                _lastDraftRow == draftRow &&
                _lastPaletteCell == paletteCellSize &&
                _lastDraftCell == draftCellSize &&
                Mathf.Approximately(_lastPaletteTileInCell, paletteTileScale) &&
                Mathf.Approximately(_lastDraftTileInCell, draftTileScale) &&
                _lastPaletteTilesPosition == paletteTilesPosition &&
                _lastDraftTilesPosition == draftTilesPosition &&
                Mathf.Approximately(_lastPaletteSpacing, paletteLayoutSpacing) &&
                Mathf.Approximately(_lastDraftSpacing, draftLayoutSpacing) &&
                _lastPaletteMaxPerRow == paletteMaxTilesPerRow &&
                _lastDraftMaxPerRow == draftMaxTilesPerRow &&
                Mathf.Approximately(_lastPaletteRowSpacing, paletteRowSpacing) &&
                Mathf.Approximately(_lastDraftRowSpacing, draftRowSpacing))
                return;

            EnsureVerticalStackLayout(paletteRow, paletteRowSpacing, paletteTilesPosition);

            ClearChildren(paletteRow);
            var paletteEffCell = paletteCellSize;
            RectTransform paletteLineRt = null;
            for (var i = 0; i < GameConstants.PlayableTileKindCount; i++)
            {
                if (i % paletteMaxTilesPerRow == 0)
                    paletteLineRt = CreateHorizontalLayoutRow(
                        paletteRow,
                        $"PaletteLine_{i / paletteMaxTilesPerRow}",
                        paletteLayoutSpacing,
                        paletteEffCell.y);

                var kind = (TileKind)i;
                var slot = CreateFixedLayoutSlot(paletteLineRt, $"PaletteSlot_{kind}", paletteEffCell);
                var tile = Instantiate(tilePrefab, slot, false);
                tile.gameObject.name = $"Palette_{kind}";
                var tileRt = (RectTransform)tile.transform;
                PrepareTileRectInSlot(tileRt);
                StripLayoutElementFrom(tile.gameObject);
                tile.Bind(kind, i, 0, 0, Vector2.zero, paletteEffCell, 1f, iconLibrary);
                ApplyUniformVisualScale(tileRt, paletteTileScale);
                tile.SetClickableVisual(true);
                tile.SetClickHandler(OnPaletteTileClicked);
            }

            RebuildDraftVisuals();

            _lastPrefab = tilePrefab;
            _lastPaletteRow = paletteRow;
            _lastDraftRow = draftRow;
            _lastPaletteCell = paletteCellSize;
            _lastDraftCell = draftCellSize;
            _lastPaletteTileInCell = paletteTileScale;
            _lastDraftTileInCell = draftTileScale;
            _lastPaletteTilesPosition = paletteTilesPosition;
            _lastDraftTilesPosition = draftTilesPosition;
            _lastPaletteSpacing = paletteLayoutSpacing;
            _lastDraftSpacing = draftLayoutSpacing;
            _lastPaletteMaxPerRow = paletteMaxTilesPerRow;
            _lastDraftMaxPerRow = draftMaxTilesPerRow;
            _lastPaletteRowSpacing = paletteRowSpacing;
            _lastDraftRowSpacing = draftRowSpacing;
        }

        void OnPaletteTileClicked(BoardTileView view)
        {
            var kind = view.Kind;
            if (kind == TileKind.None) return;
            _draft.Add(kind);
            onPaletteTileKindClicked?.Invoke((int)kind);
            RebuildDraftVisuals();
            onDraftChanged?.Invoke();
        }

        public void ClearDraft()
        {
            _draft.Clear();
            RebuildDraftVisuals();
            onDraftChanged?.Invoke();
        }

        public void RemoveLastFromDraft()
        {
            if (_draft.Count == 0) return;
            _draft.RemoveAt(_draft.Count - 1);
            RebuildDraftVisuals();
            onDraftChanged?.Invoke();
        }

        /// <summary>Single order in the same shape as the Tile Level Editor window orders text, e.g. <c>{3,3,3}</c>.</summary>
        public string GetDraftAsOrderTextEntry()
        {
            if (_draft.Count == 0) return "{}";
            var sb = new StringBuilder();
            sb.Append('{');
            for (var i = 0; i < _draft.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append((int)_draft[i]);
            }

            sb.Append('}');
            return sb.ToString();
        }

        void RebuildDraftVisuals()
        {
            if (draftRow == null || tilePrefab == null) return;
            ClearChildren(draftRow);
            EnsureVerticalStackLayout(draftRow, draftRowSpacing, draftTilesPosition);
            var draftEffCell = draftCellSize;
            RectTransform draftLineRt = null;
            for (var i = 0; i < _draft.Count; i++)
            {
                if (i % draftMaxTilesPerRow == 0)
                    draftLineRt = CreateHorizontalLayoutRow(
                        draftRow,
                        $"DraftLine_{i / draftMaxTilesPerRow}",
                        draftLayoutSpacing,
                        draftEffCell.y);

                var kind = _draft[i];
                var idx = i;
                var slot = CreateFixedLayoutSlot(draftLineRt, $"DraftSlot_{i}_{kind}", draftEffCell);
                var tile = Instantiate(tilePrefab, slot, false);
                tile.gameObject.name = $"Draft_{i}_{kind}";
                var tileRt = (RectTransform)tile.transform;
                PrepareTileRectInSlot(tileRt);
                StripLayoutElementFrom(tile.gameObject);
                tile.Bind(kind, i, 0, 0, Vector2.zero, draftEffCell, 1f, iconLibrary);
                ApplyUniformVisualScale(tileRt, draftTileScale);
                tile.SetClickableVisual(true);
                tile.SetClickHandler(_ => RemoveDraftAt(idx));
            }
        }

        void RemoveDraftAt(int index)
        {
            if ((uint)index >= (uint)_draft.Count) return;
            _draft.RemoveAt(index);
            RebuildDraftVisuals();
            onDraftChanged?.Invoke();
        }

        /// <summary>Direct child of a <see cref="HorizontalLayoutGroup"/>: fixed footprint so inner tile can be smaller.</summary>
        static RectTransform CreateFixedLayoutSlot(RectTransform row, string name, Vector2 slotSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row, false);
            rt.localScale = Vector3.one;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = slotSize.x;
            le.preferredHeight = slotSize.y;
            le.minWidth = slotSize.x;
            le.minHeight = slotSize.y;
            return rt;
        }

        static void PrepareTileRectInSlot(RectTransform tileRt)
        {
            tileRt.anchorMin = tileRt.anchorMax = tileRt.pivot = new Vector2(0.5f, 0.5f);
            tileRt.anchoredPosition = Vector2.zero;
            tileRt.localScale = Vector3.one;
        }

        /// <summary>
        /// Prefabs often use stretch anchors or nested layout where <see cref="BoardTileView.Bind"/> <c>sizeDelta</c> alone
        /// does not shrink the hierarchy; scaling the root matches the tile-scale slider to the actual on-screen size.
        /// </summary>
        static void ApplyUniformVisualScale(RectTransform tileRt, float uniformScale)
        {
            var s = Mathf.Max(0.01f, uniformScale);
            tileRt.localScale = new Vector3(s, s, 1f);
        }

        static void StripLayoutElementFrom(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) return;
            if (Application.isPlaying)
                Object.Destroy(le);
            else
                Object.DestroyImmediate(le);
        }

        /// <summary>Parent holds stacked horizontal lines; strips any <see cref="HorizontalLayoutGroup"/> on the same object.</summary>
        /// <summary><paramref name="contentOffsetPixels"/>: +X → right, +Y → down via <see cref="VerticalLayoutGroup.padding"/>.</summary>
        static void EnsureVerticalStackLayout(RectTransform column, float rowSpacing, Vector2 contentOffsetPixels)
        {
            var oldH = column.GetComponent<HorizontalLayoutGroup>();
            if (oldH != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(oldH);
                else
                    Object.DestroyImmediate(oldH);
            }

            var v = column.GetComponent<VerticalLayoutGroup>();
            if (v == null)
                v = column.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = rowSpacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            v.padding.left = Mathf.RoundToInt(contentOffsetPixels.x);
            v.padding.top = Mathf.RoundToInt(contentOffsetPixels.y);
            v.padding.right = 0;
            v.padding.bottom = 0;
        }

        static RectTransform CreateHorizontalLayoutRow(RectTransform column, string name, float spacing, float rowHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(column, false);
            rt.localScale = Vector3.one;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = rowHeight;
            le.minHeight = rowHeight;
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return rt;
        }

        static void ClearChildren(RectTransform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}
