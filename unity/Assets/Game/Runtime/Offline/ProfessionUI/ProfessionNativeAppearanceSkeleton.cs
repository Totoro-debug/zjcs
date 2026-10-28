using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Connect the migrated application contracts to actual independent native
    // bone/slot/skin objects. The explicitly named pre-physics sampler is a
    // pipeline stage, never a fallback for unimplemented physical motion.
    public sealed class ProfessionNativeAppearanceSkeleton : IProfessionAppearanceSkeleton, IProfessionAppearancePartSkeleton
    {
        private sealed class Slot : IProfessionNativeAttachmentSlot
        {
            private readonly ProfessionNativeAppearanceSkeleton owner;
            internal readonly int Index;
            internal Slot(ProfessionNativeAppearanceSkeleton owner, int index) { this.owner = owner; Index = index; }
            public ProfessionNativeBonePose Pose => owner.Pose;
            public object Attachment => owner.attachments.Attachment(Index);
        }
        private readonly ProfessionNativeSkinSlots attachments;
        private readonly Slot[] slots;
        private readonly IProfessionSkinClipSlot[] drawOrder;
        private readonly Dictionary<string, ProfessionNativeBonePose.Track[]> boneClips = new Dictionary<string, ProfessionNativeBonePose.Track[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProfessionNativeSlotPose.Clip> slotClips = new Dictionary<string, ProfessionNativeSlotPose.Clip>(StringComparer.Ordinal);
        public ProfessionNativeSkeletonData Source { get; }
        public ProfessionNativeBonePose Pose { get; }
        public ProfessionNativeSlotPose SlotPose { get; }
        public IProfessionAppearanceSkeletonData Data => Source.SkinCatalog;
        public IProfessionAppearanceSkin Skin => attachments.Skin;
        public int SlotCount => slots.Length;
        public IReadOnlyList<int> SkinConstraints => attachments.Skin?.Constraints ?? (IReadOnlyList<int>)Array.Empty<int>();
        public IProfessionSkinClipSlot[] DrawOrder
        {
            get
            {
                for (int i = 0; i < drawOrder.Length; i++) drawOrder[i] = slots[SlotPose.DrawSlot(i)];
                return drawOrder;
            }
        }
        public ProfessionNativeAppearanceSkeleton(ProfessionNativeSkeletonData data)
        {
            Source = data ?? throw new ArgumentNullException(nameof(data));
            Pose = data.CreateBonePose(); SlotPose = data.CreateSlotPose(Pose);
            attachments = new ProfessionNativeSkinSlots(data.SkinCatalog, Enumerable.Range(0, data.SlotCount).Select(SlotPose.Attachment).ToArray());
            slots = Enumerable.Range(0, data.SlotCount).Select(i => new Slot(this, i)).ToArray();
            drawOrder = new IProfessionSkinClipSlot[slots.Length];
        }
        public ProfessionNativeSkin CreateSkin(string name) => new ProfessionNativeSkin(name, Source.SlotCount, Source.BoneCount,
            Source.Constraints.Count, Array.Empty<ProfessionNativeSkin.Entry>(), Array.Empty<int>(), Array.Empty<int>());
        public string SetupAttachmentName(int slot) => attachments.SetupName(slot);
        public object GetAttachment(int slot) => attachments.Attachment(slot);
        public object GetAttachment(int slot, string name) => attachments.Resolve(slot, name);
        public IProfessionSkinClipSlot FindSlot(string name)
        { int index = Source.FindSlot(name); return index < 0 ? null : slots[index]; }
        public void SetSlotAttachment(int slot, object attachment)
        {
            if (attachment != null && (!(attachment is ProfessionNativeAttachment native) || native.SlotBone != SlotPose.Bone(slot)))
                throw new NotSupportedException("Verify original cross-slot or non-native attachment binding.");
            attachments.SetAttachment(slot, attachment);
        }
        public void SetSkin(IProfessionAppearanceSkin skin)
        {
            var native = skin as ProfessionNativeSkin;
            if (skin != null && (native == null || native.SlotCount != Source.SlotCount || native.BoneCount != Source.BoneCount || native.ConstraintCount != Source.Constraints.Count))
                throw new ArgumentException("Original combined skin belongs to another skeleton layout.");
            attachments.SetSkin(native); UpdateCache();
        }
        public void SetToSetupPose()
        { Pose.Reset(); SlotPose.Reset(); attachments.ResetSlots(); }
        public void UpdateCache() => Pose.SetSkinBones(attachments.Skin?.Bones ?? (IReadOnlyList<int>)Array.Empty<int>());

        public void EvaluateUnmixedIdleBeforePhysics(string animation, float time)
        {
            if (animation != "home/idle_01" && animation != "100/idle_01" && animation != "200/idle_01")
                throw new NotSupportedException("Verify additional original native skeleton animation: " + animation);
            if (!boneClips.TryGetValue(animation, out var bones))
            { bones = Source.CreateBoneTracks(animation); boneClips.Add(animation, bones); }
            if (!slotClips.TryGetValue(animation, out var clip))
            { clip = Source.CreateSlotClip(animation); slotClips.Add(animation, clip); }
            Pose.Apply(bones, time); SlotPose.Apply(clip, time);
            for (int i = 0; i < slots.Length; i++)
                attachments.SetAttachment(i, attachments.Resolve(i, SlotPose.Attachment(i)));
            ProfessionNativeIdleIk.Apply(Pose, Source.CreateIdleIk(animation, time));
            ProfessionNativeIdleTransform.Apply(Pose, ProfessionNativeIdleTransform.Bind(Source, animation, time, SkinConstraints));
        }

        public ProfessionNativeMeshBatches.Slot[] CreateDrawSlots(IReadOnlyList<int> breaksAfter = null)
        {
            var result = new ProfessionNativeMeshBatches.Slot[slots.Length];
            for (int i = 0; i < result.Length; i++)
            {
                int index = SlotPose.DrawSlot(i);
                result[i] = new ProfessionNativeMeshBatches.Slot(index, (ProfessionNativeAttachment)attachments.Attachment(index),
                    SlotPose.Color(index), SlotPose.Blend(index), Pose.IsActive(SlotPose.Bone(index)), breaksAfter != null && breaksAfter.Contains(index));
            }
            return result;
        }
    }
}
