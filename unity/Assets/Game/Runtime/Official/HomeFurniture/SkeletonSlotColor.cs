// Original AppearanceSystem.SpineExt slot tint controller.
using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Zjcs.Official.HomeFurniture
{
    [Serializable] public class SingleSlotColor { public string SlotName; public Color Color; }

    [ExecuteInEditMode]
    public class SkeletonSlotColor : MonoBehaviour
    {
        public SingleSlotColor[] SlotColors;
        public bool UseAllSlotColor;
        public Color AllSlotColor;
        public float AnimAlpha = 1f;
        private ISkeletonComponent skeletonComponent;
        private ISkeletonComponent SkeletonComponent => skeletonComponent ?? (skeletonComponent = GetComponent<ISkeletonComponent>());
        private void OnEnable() => ApplyColor();
        private void OnDidApplyAnimationProperties() => ApplyColor();

        public void ApplyColor()
        {
            Skeleton skeleton = SkeletonComponent.Skeleton;
            if (UseAllSlotColor)
                foreach (Slot slot in skeleton.Slots) SetColor(slot, AllSlotColor);
            if (SlotColors == null) return;
            foreach (SingleSlotColor item in SlotColors)
            {
                Slot slot = skeleton.FindSlot(item.SlotName);
                if (slot != null) SetColor(slot, item.Color);
            }
        }

        private void SetColor(Slot slot, Color color)
        {
            slot.R = color.r; slot.G = color.g; slot.B = color.b; slot.A = color.a * AnimAlpha;
        }
    }
}
