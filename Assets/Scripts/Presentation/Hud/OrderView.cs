using UnityEngine;

namespace Presentation.Hud
{
    /// <summary>
    /// One customer order row. Owns its <see cref="OrderSlotView"/>s (spawned at bind time from the
    /// level's max order size) and exposes high-level commands so callers can say things like
    /// "complete slot 1" without touching images or ticks.
    /// </summary>
    public sealed class OrderView : MonoBehaviour
    {
        [SerializeField] RectTransform container;

        OrderSlotView[] _slots;

        public RectTransform Container => container != null ? container : transform as RectTransform;

        public void BuildSlots(OrderSlotView prefab, int count)
        {
            var parent = Container;
            if (parent == null || prefab == null) return;

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            var n = Mathf.Max(0, count);
            _slots = new OrderSlotView[n];
            for (var i = 0; i < n; i++)
                _slots[i] = Instantiate(prefab, parent);
        }

        public OrderSlotView GetSlot(int index) =>
            _slots != null && (uint)index < (uint)_slots.Length ? _slots[index] : null;

        // High-level commands used by the presenter / gameplay-facing code.
        public void SetSlotIcon(int index, Sprite sprite, bool completed) => GetSlot(index)?.SetIcon(sprite, completed);
        public void ClearSlot(int index) => GetSlot(index)?.Clear();

        /// <summary>
        /// Marks every slot that currently shows an icon as completed. Used to flash the finished
        /// order fully ticked before it animates out, since the gameplay model advances to the next
        /// customer synchronously (so a plain refresh would already show the new, uncompleted order).
        /// </summary>
        public void CompleteAllVisible()
        {
            if (_slots == null) return;
            for (var i = 0; i < _slots.Length; i++)
                _slots[i]?.SetCompleted(true);
        }

        public bool TryGetSlotRect(int index, out RectTransform rect)
        {
            rect = null;
            var slot = GetSlot(index);
            if (slot != null)
            {
                rect = slot.IconRect;
                return rect != null;
            }

            if (_slots != null)
            {
                for (var i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] == null) continue;
                    rect = _slots[i].IconRect;
                    if (rect != null) return true;
                }
            }

            return false;
        }
    }
}
