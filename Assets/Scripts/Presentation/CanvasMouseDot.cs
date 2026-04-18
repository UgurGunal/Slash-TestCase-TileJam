using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Small red circle under the mouse, drawn in UI space so it shows in Game view / captures.
    /// Put this component on any object under your <see cref="Canvas"/> (or on the Canvas root).
    /// </summary>
    public sealed class CanvasMouseDot : MonoBehaviour
    {
        [SerializeField] float diameterPixels = 14f;
        [SerializeField] Color dotColor = Color.red;
        [SerializeField] int textureResolution = 32;

        Canvas _canvas;
        RectTransform _canvasRect;
        RectTransform _dotRect;

        void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                Debug.LogError("[CanvasMouseDot] Add this under a Canvas (or on the Canvas GameObject).", this);
                enabled = false;
                return;
            }

            _canvasRect = _canvas.transform as RectTransform;
            CreateDot();
        }

        void LateUpdate()
        {
            if (_dotRect == null || _canvasRect == null) return;
            var cam = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Input.mousePosition, cam, out var local))
                return;
            _dotRect.localPosition = local;
        }

        void CreateDot()
        {
            var go = new GameObject("MouseDot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            go.transform.SetAsLastSibling();

            _dotRect = go.GetComponent<RectTransform>();
            _dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            _dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            _dotRect.pivot = new Vector2(0.5f, 0.5f);
            _dotRect.sizeDelta = new Vector2(diameterPixels, diameterPixels);

            var img = go.GetComponent<Image>();
            img.sprite = CreateCircleSprite(Mathf.Max(8, textureResolution), dotColor);
            img.color = Color.white;
            img.raycastTarget = false;
        }

        static Sprite CreateCircleSprite(int size, Color color)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            var r = size * 0.5f - 0.5f;
            var c = new Vector2(r, r);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), c);
                t.SetPixel(x, y, d <= r ? color : Color.clear);
            }

            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        void OnValidate()
        {
            if (_dotRect != null)
                _dotRect.sizeDelta = new Vector2(diameterPixels, diameterPixels);
        }
    }
}
