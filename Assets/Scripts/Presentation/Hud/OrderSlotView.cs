using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>
    /// One order slot. Owns its icon image and completion tick, plus the logic that
    /// decides how a slot looks. The strip talks to it with high-level commands
    /// (<see cref="SetIcon"/>, <see cref="SetCompleted"/>, <see cref="Clear"/>).
    /// </summary>
    public sealed class OrderSlotView : MonoBehaviour
    {
        [SerializeField] Image iconImage;
        [SerializeField] GameObject tick;
        [Tooltip("Icon brightness once the slot is completed (1 = no dim).")]
        [Range(0.4f, 1f)] [SerializeField] float completedIconTint = 0.78f;

        bool _completed;

        public Image IconImage => iconImage != null ? iconImage : GetComponent<Image>();
        public RectTransform IconRect => IconImage != null ? IconImage.rectTransform : transform as RectTransform;
        public bool IsCompleted => _completed;

        void Awake() => SetTickVisible(false);

        /// <summary>Empty cell: no icon and no tick (slot is beyond this order's length or idle).</summary>
        public void Clear()
        {
            _completed = false;
            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.color = Color.white;
            }

            SetTickVisible(false);
        }

        /// <summary>Show an icon sprite and set whether that icon is already collected.</summary>
        public void SetIcon(Sprite sprite, bool completed)
        {
            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }

            SetCompleted(completed);
        }

        /// <summary>High-level command: mark this slot complete/incomplete (tick + dim).</summary>
        public void SetCompleted(bool completed)
        {
            _completed = completed;
            var hasIcon = iconImage != null && iconImage.enabled;

            if (hasIcon)
                iconImage.color = completed
                    ? new Color(completedIconTint, completedIconTint, completedIconTint, 1f)
                    : Color.white;

            SetTickVisible(completed && hasIcon);
        }

        void SetTickVisible(bool visible)
        {
            if (tick != null && tick.activeSelf != visible)
                tick.SetActive(visible);
        }
    }
}
