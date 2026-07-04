using Core;
using Gameplay;
using UnityEngine;

namespace Presentation.Hud
{
    /// <summary>
    /// Spawns modular strip/rack prefabs and wires them into <see cref="OrderRackHud"/>.
    /// </summary>
    public sealed class OrderRackHudRoot : MonoBehaviour
    {
        [SerializeField] OrderStripView stripPrefab;
        [SerializeField] RectTransform stripContainer;
        [SerializeField] RackBarView rackBarPrefab;
        [SerializeField] RectTransform rackBarContainer;
        [SerializeField] int activeOrderSlotCount = GameConstants.ActiveOrderSlotsCount;
        [SerializeField] OrderRackHud hud;
        [SerializeField] bool buildOnAwake;

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
                Debug.LogError("[OrderRackHudRoot] Assign OrderRackHud.", this);
                return;
            }

            if (stripPrefab == null || stripContainer == null || rackBarPrefab == null || rackBarContainer == null)
            {
                Debug.LogError("[OrderRackHudRoot] Assign strip/rack prefabs and containers.", this);
                return;
            }

            ClearChildren(stripContainer);
            ClearChildren(rackBarContainer);

            var count = Mathf.Max(1, activeOrderSlotCount);
            var strips = new OrderStripView[count];
            for (var i = 0; i < count; i++)
                strips[i] = Instantiate(stripPrefab, stripContainer);

            var rack = Instantiate(rackBarPrefab, rackBarContainer);
            hud.ConfigureFromPrefabViews(strips, rack);
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
