using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Canvas-space grid for authoring: updates UI line images when width/height changes.
    /// Put this on a RectTransform under a Canvas.
    /// For <c>W</c>×<c>H</c> cells, draws <c>2W−1</c> vertical and <c>2H−1</c> horizontal lines (extra line between each pair of major cell edges).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class LevelEditorGridLinesView : MonoBehaviour
    {
        [Header("Board dimensions")]
        [SerializeField] int width = 10;
        [SerializeField] int height = 10;

        [Header("Canvas grid visual")]
        [SerializeField] float cellWidth = 160f;
        [SerializeField] float cellHeight = 200f;
        [SerializeField] float lineThickness = 2f;
        [SerializeField] Color lineColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] bool fitRectToGrid = true;

        const string RootName = "__CanvasGridLinesRoot";
        RectTransform _selfRect;
        RectTransform _linesRoot;

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
            cellWidth = 160f;
            cellHeight = 200f;
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

            var verticalCount = SubdividedLineCount(width);
            var horizontalCount = SubdividedLineCount(height);

            for (var i = 0; i < verticalCount; i++)
            {
                var fx = -gridW * 0.5f + EdgeToEdgeT(i, verticalCount) * gridW;
                CreateVerticalLine($"XV{i}", fx, gridH);
            }

            for (var i = 0; i < horizontalCount; i++)
            {
                var fy = -gridH * 0.5f + EdgeToEdgeT(i, horizontalCount) * gridH;
                CreateHorizontalLine($"YH{i}", fy, gridW);
            }
        }

        /// <summary><c>N</c> cells → <c>2N−1</c> lines (e.g. 5 → 9). Minimum 2 (both edges).</summary>
        static int SubdividedLineCount(int cellCount) =>
            Mathf.Max(2, 2 * Mathf.Max(1, cellCount) - 1);

        /// <summary><c>t</c> in <c>[0,1]</c> from first line through last, evenly spaced.</summary>
        static float EdgeToEdgeT(int index, int lineCount) =>
            lineCount <= 1 ? 0f : index / (float)(lineCount - 1);

        void EnsureRoot()
        {
            _selfRect = (RectTransform)transform;
            if (_linesRoot != null) return;
            var existing = transform.Find(RootName);
            if (existing != null)
            {
                _linesRoot = (RectTransform)existing;
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
        }

        void ClearRoot()
        {
            if (_linesRoot == null) return;
            for (var i = _linesRoot.childCount - 1; i >= 0; i--)
            {
                var child = _linesRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
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
