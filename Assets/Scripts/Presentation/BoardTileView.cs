using System;
using Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Root tile object: expects direct children named <see cref="ChildBackgroundName"/> and <see cref="ChildIconName"/>,
    /// each with an <see cref="Image"/> (created automatically in <see cref="Reset"/> when missing).
    /// Icon sprite comes from <see cref="TileIconLibrary"/> or <c>Resources/TileIcons/{TileKind}</c> (e.g. Type0).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardTileView : MonoBehaviour, IPointerClickHandler
    {
        public const string ChildBackgroundName = "Background";
        public const string ChildIconName = "Icon";

        /// <summary>Resources path prefix; loads <c>Resources/TileIcons/Type0</c> etc. when library has no sprite.</summary>
        public const string TileIconsResourcesFolder = "TileIcons";

        [SerializeField] Image background;
        [SerializeField] Image icon;
        [Tooltip("Multiplied with background/icon base colors when the tile is not clickable (covered from above).")]
        [SerializeField] Color blockedTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        Color _baseBackgroundColor = Color.white;
        Color _baseIconColor = Color.white;
        Color _activeOrderTint = Color.white;
        bool _clickableState = true;
        Action<BoardTileView> _clicked;

        public TileKind Kind { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public int LayerIndex { get; private set; }

        void Awake() => ResolveChildImages();

        public void Bind(TileKind kind, int gridX, int gridY, int layerIndex, Vector2 anchoredPosition, Vector2 cellSize, float tileSizeInCellScale, TileIconLibrary iconLibrary = null)
        {
            Kind = kind;
            GridX = gridX;
            GridY = gridY;
            LayerIndex = layerIndex;

            var rt = (RectTransform)transform;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = cellSize * tileSizeInCellScale;

            ApplyIconSprite(iconLibrary);

            if (background != null)
            {
                background.raycastTarget = true;
                _baseBackgroundColor = background.color;
            }

            if (icon != null)
            {
                icon.raycastTarget = false;
                _baseIconColor = icon.color;
            }

            _clickableState = true;
            _activeOrderTint = Color.white;
            RefreshTintedColors();
        }

        public void SetClickHandler(Action<BoardTileView> onClicked) => _clicked = onClicked;

        public RectTransform IconRectTransform => icon != null ? icon.rectTransform : null;

        /// <summary>Used when flying only the icon to rack/order HUD; removes the board backing immediately.</summary>
        public void DestroyBackgroundImmediate()
        {
            if (background == null) return;
            Destroy(background.gameObject);
            background = null;
        }

        /// <summary>Multiplies base colors (e.g. slight green for “active” order columns in the level editor).</summary>
        public void SetActiveOrderHighlight(bool enabled, Color tint)
        {
            _activeOrderTint = enabled ? tint : Color.white;
            RefreshTintedColors();
        }

        public void SetClickableVisual(bool clickable)
        {
            _clickableState = clickable;
            RefreshTintedColors();
        }

        void RefreshTintedColors()
        {
            if (background != null)
                background.color = _clickableState ? _baseBackgroundColor * _activeOrderTint : _baseBackgroundColor * _activeOrderTint * blockedTint;
            if (icon != null)
                icon.color = _clickableState ? _baseIconColor * _activeOrderTint : _baseIconColor * _activeOrderTint * blockedTint;
        }

        public void OnPointerClick(PointerEventData eventData) => _clicked?.Invoke(this);

        void ApplyIconSprite(TileIconLibrary iconLibrary)
        {
            if (icon == null || Kind == TileKind.None) return;

            Sprite sprite = null;
            if (iconLibrary != null && iconLibrary.TryGetSprite(Kind, out var fromLib))
                sprite = fromLib;
            if (sprite == null)
                sprite = Resources.Load<Sprite>($"{TileIconsResourcesFolder}/{Kind}");

            icon.sprite = sprite;
            icon.enabled = sprite != null;
            if (sprite != null) icon.preserveAspect = true;
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
