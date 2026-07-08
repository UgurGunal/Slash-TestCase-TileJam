using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Single rack slot: icon image for a collected non-matching tile.</summary>
    public sealed class RackSlotView : MonoBehaviour
    {
        [SerializeField] Image iconImage;

        public Image IconImage => iconImage != null ? iconImage : GetComponent<Image>();
    }
}
