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
    public sealed class ProfessionNativeAppearanceSkeleton : IProfessionAppearanceSkeleton, IProfessionAppearancePartSkeleton, IProfessionFollowerSkeleton
    {
        private sealed class Bone : IProfessionFollowerBone
        {
            private readonly ProfessionNativeAppearanceSkeleton owner;
            private readonly int index;
            internal Bone(ProfessionNativeAppearanceSkeleton owner, int index) { this.owner = owner; this.index = index; }
            private Matrix4x4 World => owner.Pose.World(index);
            public bool Active => owner.Pose.IsActive(index);
            public float WorldX => World.m03 * owner.Source.DataScale;
            public float WorldY => World.m13 * owner.Source.DataScale;
            public float WorldRotationX => Mathf.Atan2(World.m10, World.m00) * Mathf.Rad2Deg;
            public float ScaleX => owner.Pose.LocalScale(index).x;
            public float ScaleY => owner.Pose.LocalScale(index).y;
            public float A => World.m00;
            public float B => World.m01;
            public float C => World.m10;
            public float D => World.m11;
        }
        private sealed class Slot : IProfessionNativeAttachmentSlot, IProfessionFollowerSlot
        {
            private readonly ProfessionNativeAppearanceSkeleton owner;
            public int Index { get; }
            public IProfessionFollowerBone Bone => owner.bones[owner.SlotPose.Bone(Index)];
            public float zOrder => owner.CustomZOrder(Index);
            internal Slot(ProfessionNativeAppearanceSkeleton owner, int index) { this.owner = owner; Index = index; }
            public ProfessionNativeBonePose Pose => owner.Pose;
            public object Attachment => owner.attachments.Attachment(Index);
        }
        private readonly ProfessionNativeSkinSlots attachments;
        private readonly Slot[] slots;
        private readonly IProfessionSkinClipSlot[] drawOrder;
        private readonly IProfessionFollowerSlot[] followerOrder;
        private readonly Bone[] bones;
        private readonly Func<int, float> customZOrder;
        private readonly Color[] animationColors;
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
        IProfessionFollowerSlot[] IProfessionFollowerSkeleton.DrawOrder
        {
            get
            {
                for (int i = 0; i < followerOrder.Length; i++) followerOrder[i] = slots[SlotPose.DrawSlot(i)];
                return followerOrder;
            }
        }
        IProfessionFollowerSlot IProfessionFollowerSkeleton.FindSlot(string name)
        { int index = Source.FindSlot(name); return index < 0 ? null : slots[index]; }
        public IProfessionFollowerBone FindBone(string name)
        { int index = Source.FindBone(name); return index < 0 ? null : bones[index]; }
        private float CustomZOrder(int slot)
        {
            if (customZOrder == null) throw new NotSupportedException("Original custom slot depth has not been bound.");
            float value = customZOrder(slot);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Original custom slot depth is not finite.");
            return value;
        }
        public ProfessionNativeAppearanceSkeleton(ProfessionNativeSkeletonData data, Func<int, float> customZOrder = null)
        {
            this.customZOrder = customZOrder;
            Source = data ?? throw new ArgumentNullException(nameof(data));
            Pose = data.CreateBonePose(); SlotPose = data.CreateSlotPose(Pose);
            attachments = new ProfessionNativeSkinSlots(data.SkinCatalog, Enumerable.Range(0, data.SlotCount).Select(SlotPose.Attachment).ToArray());
            bones = Enumerable.Range(0, data.BoneCount).Select(i => new Bone(this, i)).ToArray();
            slots = Enumerable.Range(0, data.SlotCount).Select(i => new Slot(this, i)).ToArray();
            drawOrder = new IProfessionSkinClipSlot[slots.Length];
            followerOrder = new IProfessionFollowerSlot[slots.Length];
            animationColors = Enumerable.Repeat(Color.white, slots.Length).ToArray();
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

        // Battle NPCs use the same source channels, but their animation names are
        // character-prefixed (for example npc_8402/idle_01). Keep the generic
        // channel application here so battle does not invent a second pose graph.
        public void EvaluateNativeAnimation(ProfessionNativeAnimationChannels channels, float time)
        {
            if (channels == null) throw new ArgumentNullException(nameof(channels));
            channels.ApplyLocalPose(Pose, SlotPose, animationColors, Mathf.Max(0f, time), true);
            for (int i = 0; i < slots.Length; i++)
                attachments.SetAttachment(i, attachments.Resolve(i, SlotPose.Attachment(i)));
        }

        // The two original default weapon skins have no active constraints.
        // Refuse constrained parts rather than silently displaying a setup pose.
        public void EvaluateUnmixedWeaponAnimation(string animation, float time)
        {
            if (Source.Sex != "primary-weapon") throw new NotSupportedException("Expected the original primary weapon model.");
            if (float.IsNaN(time) || float.IsInfinity(time)) throw new ArgumentException("Original weapon time must be finite.");
            for (int i = 0; i < Source.Constraints.Count; i++)
                if (!(Source.Constraints[i].Fields.TryGetValue("skin", out var required) && (bool)required) || SkinConstraints.Contains(i))
                    throw new NotSupportedException("Restore active original weapon constraint: " + Source.Constraints[i].Name);
            foreach (var section in Source.GetAnimation(animation))
                if (section.Key != "bones") throw new NotSupportedException("Restore additional original weapon timeline: " + section.Key);
            if (!boneClips.TryGetValue(animation, out var tracks))
            { tracks = Source.CreateBoneTracks(animation); boneClips.Add(animation, tracks); }
            Pose.Apply(tracks, time); SlotPose.Reset(); attachments.ResetSlots();
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
