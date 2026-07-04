using Core;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Single order strip row: container + icon image slots.</summary>
    public sealed class OrderStripView : MonoBehaviour
    {
        [SerializeField] RectTransform stripContainer;
        [SerializeField] Image[] iconImages;

        public RectTransform StripContainer => stripContainer != null ? stripContainer : transform as RectTransform;
        public Image[] IconImages => iconImages;
    }
}
