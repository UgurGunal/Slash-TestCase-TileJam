using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Tints the logical cell under the pointer. Cell size matches <see cref="LevelEditorGridLinesView"/> exactly
    /// by parenting to <see cref="LevelEditorGridLinesView.HighlightLayerRect"/> (same local space as line geometry).
    /// If you assign <see cref="highlightRoot"/>, positions are mapped from line space into that rect.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LevelEditorGridHoverHighlight : MonoBehaviour
    {
        [SerializeField] LevelEditorGridLinesView grid;
        [Tooltip("Optional parent for overlays. Leave empty to use the grid’s built-in hover layer (same space as lines, exact cell size).")]
        [SerializeField] RectTransform highlightRoot;
        [SerializeField] Color highlightColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] Canvas canvasOverride;

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

            var w = grid.GridWidthCells;
            var h = grid.GridHeightCells;
            var cw = grid.GridCellWidth;
            var ch = grid.GridCellHeight;
            if (w < 1 || h < 1)
            {
                HideHighlight();
                return;
            }

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

            var gridW = w * cw;
            var gridH = h * ch;

            var mx = (localInLines.x + gridW * 0.5f) / cw;
            var my = (localInLines.y + gridH * 0.5f) / ch;
            const float eps = 1e-4f;
            if (mx < -eps || my < -eps || mx > w + eps || my > h + eps)
            {
                HideHighlight();
                return;
            }

            mx = Mathf.Clamp(mx, 0f, w);
            my = Mathf.Clamp(my, 0f, h);

            var cx = Mathf.Clamp(Mathf.FloorToInt(mx), 0, w - 1);
            var cy = Mathf.Clamp(Mathf.FloorToInt(my), 0, h - 1);

            var useLinesLocalAnchors = highlightRoot == null;
            PlaceCell(linesRt, placeRt, cw, ch, gridW, gridH, cx, cy, useLinesLocalAnchors);
        }

        RectTransform HighlightParent =>
            highlightRoot != null ? highlightRoot : grid != null ? grid.HighlightLayerRect : null;

        static Vector2 CellCenterInLinesSpace(int cx, int cy, float gridW, float gridH, float cw, float ch) =>
            new Vector2(-gridW * 0.5f + (cx + 0.5f) * cw, -gridH * 0.5f + (cy + 0.5f) * ch);

        static Vector2 LinesLocalToParentLocal(RectTransform linesRt, RectTransform targetRt, Vector2 localInLines)
        {
            var world = linesRt.TransformPoint(new Vector3(localInLines.x, localInLines.y, 0f));
            var inTarget = targetRt.InverseTransformPoint(world);
            return new Vector2(inTarget.x, inTarget.y);
        }

        void PlaceCell(
            RectTransform linesRt,
            RectTransform placeRt,
            float cw,
            float ch,
            float gridW,
            float gridH,
            int cx,
            int cy,
            bool useLinesLocalAnchors)
        {
            var img = _highlight;
            var rt = (RectTransform)img.transform;
            if (rt.parent != placeRt)
                rt.SetParent(placeRt, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cw, ch);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            var centerLines = CellCenterInLinesSpace(cx, cy, gridW, gridH, cw, ch);
            rt.anchoredPosition = useLinesLocalAnchors
                ? centerLines
                : LinesLocalToParentLocal(linesRt, placeRt, centerLines);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = highlightColor;
            img.enabled = true;
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
