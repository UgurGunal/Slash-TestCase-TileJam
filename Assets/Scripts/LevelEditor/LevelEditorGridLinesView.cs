using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Canvas-space grid for authoring: updates UI line images when width/height changes.
    /// Put this on a RectTransform under a Canvas.
    /// For <c>W</c>×<c>H</c> major cells, draws <c>2W+1</c> vertical and <c>2H+1</c> horizontal lines:
    /// spacing is always <c>cellWidth/2</c> horizontally and <c>cellHeight/2</c> vertically (e.g. 160×200 cells → 80 and 100 between adjacent lines).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class LevelEditorGridLinesView : MonoBehaviour
    {
        [Header("Board dimensions")]
        [SerializeField] int width = 10;
        [SerializeField] int height = 10;

        [Header("Canvas grid visual")]
        [SerializeField] float cellWidth = 80f;
        [SerializeField] float cellHeight = 100f;
        [SerializeField] float lineThickness = 2f;
        [SerializeField] Color lineColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] bool fitRectToGrid = true;

        const string RootName = "__CanvasGridLinesRoot";
        const string HoverLayerName = "__GridHoverLayer";
        RectTransform _selfRect;
        RectTransform _linesRoot;
        RectTransform _hoverLayerRoot;

        public int GridWidthCells => width;
        public int GridHeightCells => height;
        public float GridCellWidth => cellWidth;
        public float GridCellHeight => cellHeight;
        public float GridLineThickness => lineThickness;

        /// <summary>Line positions use this rect’s local axes (centered, size = width×cell × height×cell). Parent <see cref="GridRect"/> may be moved on the canvas.</summary>
        public RectTransform LinesContentRect
        {
            get
            {
                EnsureRoot();
                return _linesRoot;
            }
        }

        public RectTransform GridRect => (RectTransform)transform;

        /// <summary>Parent for hover tint rects: same local space as line geometry, never cleared by <see cref="RebuildCanvasGrid"/>.</summary>
        public RectTransform HighlightLayerRect
        {
            get
            {
                EnsureRoot();
                return _hoverLayerRoot;
            }
        }

        int _lastWidth;
        int _lastHeight;
        float _lastCellWidth;
        float _lastCellHeight;
        float _lastLineThickness;
        Color _lastLineColor;
        bool _lastFitRectToGrid;
        bool _rebuildQueued;

        void OnEnable() => _rebuildQueued = true;
        void OnValidate()
        {
            // Do not create/destroy objects inside OnValidate; Unity blocks that and logs SendMessage warnings.
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellWidth = Mathf.Max(1f, cellWidth);
            cellHeight = Mathf.Max(1f, cellHeight);
            lineThickness = Mathf.Max(1f, lineThickness);
            _rebuildQueued = true;
        }

        void Update()
        {
            if (_rebuildQueued)
            {
                _rebuildQueued = false;
                RebuildIfDirty(force: true);
                return;
            }

            RebuildIfDirty(force: false);
        }
        void Reset()
        {
            width = 10;
            height = 10;
            cellWidth = 80f;
            cellHeight = 100f;
            lineThickness = 2f;
            fitRectToGrid = true;
        }

        public void SetDimensions(int nextWidth, int nextHeight)
        {
            width = Mathf.Max(1, nextWidth);
            height = Mathf.Max(1, nextHeight);
            RebuildIfDirty(force: true);
        }

        public void SetWidth(int value) => SetDimensions(value, height);
        public void SetHeight(int value) => SetDimensions(width, value);
        public void SetWidthFromFloat(float value) => SetWidth(Mathf.RoundToInt(value));
        public void SetHeightFromFloat(float value) => SetHeight(Mathf.RoundToInt(value));
        public void SetDimensionsFromStrings(string widthText, string heightText)
        {
            if (!int.TryParse(widthText, out var w)) return;
            if (!int.TryParse(heightText, out var h)) return;
            SetDimensions(w, h);
        }

        void RebuildIfDirty(bool force)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellWidth = Mathf.Max(1f, cellWidth);
            cellHeight = Mathf.Max(1f, cellHeight);
            lineThickness = Mathf.Max(1f, lineThickness);

            if (!force &&
                _lastWidth == width &&
                _lastHeight == height &&
                Mathf.Approximately(_lastCellWidth, cellWidth) &&
                Mathf.Approximately(_lastCellHeight, cellHeight) &&
                _lastLineColor == lineColor &&
                Mathf.Approximately(_lastLineThickness, lineThickness) &&
                _lastFitRectToGrid == fitRectToGrid)
                return;

            RebuildCanvasGrid();

            _lastWidth = width;
            _lastHeight = height;
            _lastCellWidth = cellWidth;
            _lastCellHeight = cellHeight;
            _lastLineColor = lineColor;
            _lastLineThickness = lineThickness;
            _lastFitRectToGrid = fitRectToGrid;
        }

        void RebuildCanvasGrid()
        {
            EnsureRoot();
            ClearRoot();

            var gridW = width * cellWidth;
            var gridH = height * cellHeight;
            if (fitRectToGrid)
                _selfRect.sizeDelta = new Vector2(gridW, gridH);

            _linesRoot.sizeDelta = new Vector2(gridW, gridH);

            var halfCellW = cellWidth * 0.5f;
            var halfCellH = cellHeight * 0.5f;

            var verticalCount = 2 * width + 1;
            for (var i = 0; i < verticalCount; i++)
            {
                var fx = -gridW * 0.5f + i * halfCellW;
                CreateVerticalLine($"XV{i}", fx, gridH);
            }

            var horizontalCount = 2 * height + 1;
            for (var j = 0; j < horizontalCount; j++)
            {
                var fy = -gridH * 0.5f + j * halfCellH;
                CreateHorizontalLine($"YH{j}", fy, gridW);
            }

            if (_hoverLayerRoot)
                _hoverLayerRoot.SetAsLastSibling();
        }

        void EnsureRoot()
        {
            _selfRect = (RectTransform)transform;
            if (_linesRoot != null)
            {
                EnsureHoverLayerUnderLinesRoot();
                return;
            }

            var existing = transform.Find(RootName);
            if (existing != null)
            {
                _linesRoot = (RectTransform)existing;
                EnsureHoverLayerUnderLinesRoot();
                return;
            }

            var root = new GameObject(RootName);
            var rt = root.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            _linesRoot = rt;
            EnsureHoverLayerUnderLinesRoot();
        }

        void EnsureHoverLayerUnderLinesRoot()
        {
            if (_linesRoot == null) return;
            if (_hoverLayerRoot && _hoverLayerRoot.parent == _linesRoot)
                return;

            var found = _linesRoot.Find(HoverLayerName);
            if (found != null)
            {
                _hoverLayerRoot = (RectTransform)found;
                return;
            }

            var go = new GameObject(HoverLayerName, typeof(RectTransform));
            var h = (RectTransform)go.transform;
            h.SetParent(_linesRoot, false);
            h.anchorMin = h.anchorMax = h.pivot = new Vector2(0.5f, 0.5f);
            h.anchoredPosition = Vector2.zero;
            h.sizeDelta = Vector2.zero;
            h.localScale = Vector3.one;
            _hoverLayerRoot = h;
        }

        void ClearRoot()
        {
            if (_linesRoot == null) return;
            for (var i = _linesRoot.childCount - 1; i >= 0; i--)
            {
                var child = _linesRoot.GetChild(i);
                if (_hoverLayerRoot && child == _hoverLayerRoot)
                    continue;
                var go = child.gameObject;
                if (Application.isPlaying)
                    Destroy(go);
                else
                    DestroyImmediate(go);
            }
        }

        void CreateVerticalLine(string name, float localX, float gridHeight)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = lineColor;
            rt.SetParent(_linesRoot, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(localX, 0f);
            rt.sizeDelta = new Vector2(lineThickness, gridHeight + lineThickness);
        }

        void CreateHorizontalLine(string name, float localY, float gridWidth)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = lineColor;
            rt.SetParent(_linesRoot, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, localY);
            rt.sizeDelta = new Vector2(gridWidth + lineThickness, lineThickness);
        }
    }
}
