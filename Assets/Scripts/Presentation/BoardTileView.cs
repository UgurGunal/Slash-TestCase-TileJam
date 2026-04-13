using TileMatch.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.Presentation
{
    /// <summary>
    /// Root tile object: expects direct children named <see cref="ChildBackgroundName"/> and <see cref="ChildIconName"/>,
    /// each with an <see cref="Image"/> (created automatically in <see cref="Reset"/> when missing).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardTileView : MonoBehaviour
    {
        public const string ChildBackgroundName = "Background";
        public const string ChildIconName = "Icon";

        [SerializeField] Image background;
        [SerializeField] Image icon;

        public TileKind Kind { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public int LayerIndex { get; private set; }

        void Awake() => ResolveChildImages();

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

        void OnValidate() => ResolveChildImages();

        void Reset()
        {
            EnsureChildWithImage(ChildBackgroundName, 0);
            EnsureChildWithImage(ChildIconName, 1);
            ResolveChildImages();
        }

        void ResolveChildImages()
        {
            if (background == null)
                background = transform.Find(ChildBackgroundName)?.GetComponent<Image>();
            if (icon == null)
                icon = transform.Find(ChildIconName)?.GetComponent<Image>();
        }

        void EnsureChildWithImage(string childName, int siblingIndex)
        {
            var existing = transform.Find(childName);
            if (existing != null)
            {
                if (existing.GetComponent<Image>() == null)
                    existing.gameObject.AddComponent<Image>();
                existing.SetSiblingIndex(siblingIndex);
                StretchToParent((RectTransform)existing);
                return;
            }

            var go = new GameObject(childName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.SetSiblingIndex(siblingIndex);
            StretchToParent(rt);
        }

        static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
    }
}
