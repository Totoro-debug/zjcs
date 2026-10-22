using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Presentation.Character
{
    /// <summary>
    /// Original skill-slot drag interaction: dragging one battle/passive slot onto another swaps
    /// the two installed skills inside the selected plan.
    /// </summary>
    internal sealed class SkillSlotDrag : MonoBehaviour, IBeginDragHandler, IDropHandler
    {
        private static SkillSlotDrag dragging;

        public int ItemType;
        public int Slot;
        public Action<int, int, int> Dropped;

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = this;
        }

        public void OnDrop(PointerEventData eventData)
        {
            SkillSlotDrag source = dragging;
            dragging = null;
            if (source == null || source == this) return;
            source.Dropped?.Invoke(source.ItemType, source.Slot, Slot);
        }
    }
}
