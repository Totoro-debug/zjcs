using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    // Data boundary for the game's own AttachmentFollower application component.
    // All values come from our live planar solver; no animation SDK code is used.
    public sealed class NativeMap13ApplicationPose
    {
        private readonly NativeMap13SkeletonSource source;
        private readonly NativeMap13SkeletonView view;
        private readonly NativeMap13ApplicationBone[] bones;
        private readonly NativeMap13ApplicationSlot[] slots;
        public NativeMap13ApplicationOrder DrawOrder { get; }
        public NativeMap13ApplicationPose(NativeMap13SkeletonSource source, NativeMap13SkeletonView view)
        {
            this.source = source; this.view = view;
            bones = new NativeMap13ApplicationBone[source.bones.Length];
            for (int i = 0; i < bones.Length; i++) bones[i] = new NativeMap13ApplicationBone(view, i);
            slots = new NativeMap13ApplicationSlot[source.slots.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = new NativeMap13ApplicationSlot(source, view, i, bones[source.slots[i].bone]);
            DrawOrder = new NativeMap13ApplicationOrder(slots, view);
        }
        public NativeMap13ApplicationBone FindBone(string name)
        { int index = Array.FindIndex(source.bones, b => b.name == name); return index < 0 ? null : bones[index]; }
        public NativeMap13ApplicationSlot FindSlot(string name)
        { int index = Array.FindIndex(source.slots, s => s.name == name); return index < 0 ? null : slots[index]; }
        public NativeMap13Attachment GetAttachment(string slot, string name)
        {
            int index = Array.FindIndex(source.slots, s => s.name == slot);
            return index < 0 ? null : Array.Find(source.attachments, a => a.slot == index && a.name == name);
        }
    }
    public sealed class NativeMap13ApplicationBone
    {
        private readonly NativeMap13SkeletonView view; private readonly int index;
        internal NativeMap13ApplicationBone(NativeMap13SkeletonView view, int index) { this.view = view; this.index = index; }
        private Matrix4x4 Matrix => view.BoneSkeletonMatrix(index);
        // This source projection contains no skin-required bones or constraints.
        public bool Active => true;
        public float WorldX => Matrix.m03;
        public float WorldY => Matrix.m13;
        public float A => Matrix.m00;
        public float C => Matrix.m10;
        public float WorldRotationX => Mathf.Atan2(C, A) * Mathf.Rad2Deg;
        public float ScaleX => view.BoneLocalScale(index).x;
        public float ScaleY => view.BoneLocalScale(index).y;
    }
    public sealed class NativeMap13ApplicationSlot
    {
        private readonly NativeMap13SkeletonSource source; private readonly NativeMap13SkeletonView view; private readonly int index;
        public NativeMap13ApplicationBone Bone { get; }
        internal NativeMap13ApplicationSlot(NativeMap13SkeletonSource source, NativeMap13SkeletonView view, int index, NativeMap13ApplicationBone bone)
        { this.source = source; this.view = view; this.index = index; Bone = bone; }
        public NativeMap13Attachment Attachment => view.SlotAttachments[index] < 0 ? null : source.attachments[view.SlotAttachments[index]];
    }
    public sealed class NativeMap13ApplicationOrder
    {
        private readonly NativeMap13ApplicationSlot[] slots, ordered;
        private readonly NativeMap13SkeletonView view;
        internal NativeMap13ApplicationOrder(NativeMap13ApplicationSlot[] slots, NativeMap13SkeletonView view)
        { this.slots = slots; this.view = view; ordered = new NativeMap13ApplicationSlot[slots.Length]; }
        public NativeMap13ApplicationSlot[] Items
        { get { for (int i = 0; i < ordered.Length; i++) ordered[i] = slots[view.SlotDrawOrder[i]]; return ordered; } }
    }
}
