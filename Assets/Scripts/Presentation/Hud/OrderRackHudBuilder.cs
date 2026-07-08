using UnityEngine;

namespace Presentation.Hud
{
    /// <summary>
    /// Build-time composition: reads <see cref="HudLayoutConfig"/>, spawns Order/Rack prefabs and rack slots,
    /// then wires the resulting views into <see cref="OrderRackHud"/>.
    /// Lives on the same GameObject as <see cref="OrderRackHud"/> (typically <c>ObjectiveHud</c>).
    /// </summary>
    // Runs before GameCompositionRoot (-100) so views are configured before the session binds.
    [DefaultExecutionOrder(-200)]
    public sealed class OrderRackHudBuilder : MonoBehaviour
    {
        [SerializeField] HudLayoutConfig config;
        [SerializeField] RectTransform orderContainer;
        [SerializeField] RectTransform rackContainer;
        [SerializeField] OrderRackHud hud;
        [SerializeField] bool buildOnAwake;

        public bool BuildsOnAwake => buildOnAwake;

        void Awake()
        {
            if (hud == null)
                hud = GetComponent<OrderRackHud>();

            if (buildOnAwake)
                Build();
        }

        [ContextMenu("Build HUD from prefabs")]
        public void Build()
        {
            if (hud == null)
            {
                Debug.LogError("[OrderRackHudBuilder] Assign OrderRackHud.", this);
                return;
            }

            if (config == null)
            {
                Debug.LogError("[OrderRackHudBuilder] Assign a HudLayoutConfig.", this);
                return;
            }

            if (orderContainer == null || rackContainer == null ||
                config.OrderPrefab == null || config.OrderSlotPrefab == null ||
                config.RackPrefab == null || config.RackSlotPrefab == null)
            {
                Debug.LogError("[OrderRackHudBuilder] Assign containers and all prefabs in the HudLayoutConfig.", this);
                return;
            }

            ClearChildren(orderContainer);

            var orderCount = config.ActiveOrderSlotCount;
            var orders = new OrderView[orderCount];
            for (var i = 0; i < orderCount; i++)
                orders[i] = Instantiate(config.OrderPrefab, orderContainer);

            // If the container is itself a Rack (has RackView), fill it directly; otherwise spawn one.
            // This avoids nesting a Rack inside a Rack when the scene already holds a Rack instance.
            var rack = rackContainer.GetComponent<RackView>();
            if (rack == null)
            {
                ClearChildren(rackContainer);
                rack = Instantiate(config.RackPrefab, rackContainer);
            }

            BuildRackSlots(rack);

            hud.ConfigureFromPrefabViews(orders, rack, config.OrderSlotPrefab);
        }

        void BuildRackSlots(RackView rack)
        {
            var parent = rack.SlotContainer;
            ClearChildren(parent);

            var capacity = config.RackCapacity;
            var slots = new RackSlotView[capacity];
            for (var i = 0; i < capacity; i++)
                slots[i] = Instantiate(config.RackSlotPrefab, parent);

            rack.SetSlots(slots);
        }

        static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }
}
