using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Hud
{
    /// <summary>Six rack slot images for wrong-tile collection feedback.</summary>
    public sealed class RackBarView : MonoBehaviour
    {
        [SerializeField] Image[] rackSlotImages;

        public Image[] RackSlotImages => rackSlotImages;
    }
}
