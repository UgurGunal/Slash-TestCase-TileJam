using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Highlights a full <b>major cell</b> (<see cref="LevelEditorGridLinesView.GridCellWidth"/> × height) centered on the
    /// nearest half-cell placement point — (2W−1)×(2H−1) centers on a W×H major grid (see <see cref="LevelEditorGridLinesView.GetPlacementSlotCounts"/>).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LevelEditorGridHoverHighlight : MonoBehaviour
    {
        [SerializeField] LevelEditorGridLinesView grid;
        [Tooltip("Optional parent for overlays. Leave empty to use the grid’s built-in hover layer (major-cell-sized highlight).")]
        [SerializeField] RectTransform highlightRoot;
        [SerializeField] Color highlightColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] Canvas canvasOverride;
        [Tooltip("Nested Canvas sort order so the highlight draws above board tiles (sibling RectTransforms that are later in the hierarchy).")]
        [SerializeField] int hoverOverlaySortingOrder = 500;

        Image _highlight;
        bool _built;

        void OnEnable() => HideHighlight();

        void OnDisable() => HideHighlight();

        void Update()
        {
            if (grid == null)
            {
                HideHighlight();
                return;
            }

            EnsureHighlight();

            var linesRt = grid.LinesContentRect;
            var placeRt = HighlightParent;
            if (linesRt == null || placeRt == null)
            {
                HideHighlight();
                return;
            }

            var majorW = grid.GridWidthCells;
            var majorH = grid.GridHeightCells;
            var cw = grid.GridCellWidth;
            var ch = grid.GridCellHeight;
            if (majorW < 1 || majorH < 1)
            {
                HideHighlight();
                return;
            }

            var gridW = majorW * cw;
            var gridH = majorH * ch;

            var canvas = canvasOverride != null ? canvasOverride : GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(linesRt, Input.mousePosition, cam, out var localInLines))
            {
                HideHighlight();
                return;
            }

            if (!linesRt.rect.Contains(localInLines))
            {
                HideHighlight();
                return;
            }

            var tx = (localInLines.x + gridW * 0.5f) / gridW;
            var ty = (localInLines.y + gridH * 0.5f) / gridH;
            const float eps = 1e-4f;
            if (tx < -eps || ty < -eps || tx > 1f + eps || ty > 1f + eps)
            {
                HideHighlight();
                return;
            }

            tx = Mathf.Clamp01(tx);
            ty = Mathf.Clamp01(ty);

            LevelEditorGridLinesView.LocalToPlacementIndices(localInLines, majorW, majorH, gridW, gridH, out var px, out var py);

            var useLinesLocalAnchors = highlightRoot == null;
            PlaceHover(linesRt, placeRt, gridW, gridH, cw, ch, majorW, majorH, px, py, useLinesLocalAnchors);
        }

        RectTransform HighlightParent =>
            highlightRoot != null ? highlightRoot : grid != null ? grid.HighlightLayerRect : null;

        static Vector2 LinesLocalToParentLocal(RectTransform linesRt, RectTransform targetRt, Vector2 localInLines)
        {
            var world = linesRt.TransformPoint(new Vector3(localInLines.x, localInLines.y, 0f));
            var inTarget = targetRt.InverseTransformPoint(world);
            return new Vector2(inTarget.x, inTarget.y);
        }

        void PlaceHover(
            RectTransform linesRt,
            RectTransform placeRt,
            float gridW,
            float gridH,
            float majorCellW,
            float majorCellH,
            int majorW,
            int majorH,
            int px,
            int py,
            bool useLinesLocalAnchors)
        {
            var img = _highlight;
            var rt = (RectTransform)img.transform;
            if (rt.parent != placeRt)
                rt.SetParent(placeRt, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(majorCellW, majorCellH);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            var centerLines = LevelEditorGridLinesView.PlacementSlotCenterLocal(px, py, gridW, gridH, majorW, majorH);
            rt.anchoredPosition = useLinesLocalAnchors
                ? centerLines
                : LinesLocalToParentLocal(linesRt, placeRt, centerLines);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = highlightColor;
            img.enabled = true;
            rt.SetAsLastSibling();
        }

        void EnsureHighlight()
        {
            if (_built || grid == null) return;
            var parent = HighlightParent;
            if (parent == null) return;
            var go = new GameObject("GridHoverCell", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = highlightColor;
            // Board tiles often live on a sibling RectTransform that sorts after the grid; without this, hover draws underneath.
            var overlayCanvas = go.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = hoverOverlaySortingOrder;
            _highlight = img;
            _built = true;
        }

        void HideHighlight()
        {
            if (_highlight != null)
                _highlight.enabled = false;
        }
    }
}
