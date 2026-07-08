using UnityEngine;

namespace Presentation.Hud
{
    /// <summary>
    /// Single source of truth for HUD slot counts and the prefabs used to build them.
    /// Read by <see cref="OrderRackHudBuilder"/> (visuals) and by the level loader (gameplay capacity).
    /// </summary>
    [CreateAssetMenu(menuName = "TileJam/Hud Layout Config", fileName = "HudLayoutConfig")]
    public sealed class HudLayoutConfig : ScriptableObject
    {
        [Header("Counts")]
        [Min(1)] [SerializeField] int rackCapacity = 6;
        [Min(1)] [SerializeField] int activeOrderSlotCount = 2;

        [Header("Prefabs (atoms are reused; edit them to change slot type)")]
        [SerializeField] OrderView orderPrefab;
        [SerializeField] OrderSlotView orderSlotPrefab;
        [SerializeField] RackView rackPrefab;
        [SerializeField] RackSlotView rackSlotPrefab;

        public int RackCapacity => Mathf.Max(1, rackCapacity);
        public int ActiveOrderSlotCount => Mathf.Max(1, activeOrderSlotCount);

        public OrderView OrderPrefab => orderPrefab;
        public OrderSlotView OrderSlotPrefab => orderSlotPrefab;
        public RackView RackPrefab => rackPrefab;
        public RackSlotView RackSlotPrefab => rackSlotPrefab;
    }
}
