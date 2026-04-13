using TileMatch.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.Presentation
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardTileView : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image icon;

        public TileKind Kind { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public int LayerIndex { get; private set; }

        public void Bind(TileKind kind, int gridX, int gridY, int layerIndex, Vector2 anchoredPosition, Vector2 cellSize)
        {
            Kind = kind;
            GridX = gridX;
            GridY = gridY;
            LayerIndex = layerIndex;

            var rt = (RectTransform)transform;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = cellSize * 0.92f;
        }

        void Reset()
        {
            if (background == null)
                background = GetComponent<Image>();
        }
    }
}
