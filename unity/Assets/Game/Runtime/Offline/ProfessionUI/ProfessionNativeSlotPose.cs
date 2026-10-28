using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent, unmixed discrete source timelines. Rendering, skin lookup
    // and animation blending remain separate stages; no SDK code is used.
    public sealed class ProfessionNativeSlotPose
    {
        [Serializable] public sealed class Slot
        {
            public string name, attachment;
            public int bone, blend;
            public Color32 color = new Color32(255, 255, 255, 255), darkColor;
            public bool hasDarkColor;
        }
        [Serializable] public sealed class AttachmentKey { public float time; public string name; }
        [Serializable] public sealed class AttachmentTrack { public int slot; public AttachmentKey[] keys; }
        [Serializable] public sealed class DrawOrderKey { public float time; public int[] slots; }
        public sealed class Clip
        {
            internal readonly AttachmentTrack[] Tracks;
            internal readonly DrawOrderKey[] Order;
            internal readonly int SlotCount;
            public Clip(int slotCount, AttachmentTrack[] tracks, DrawOrderKey[] order)
            {
                if (slotCount <= 0 || tracks == null || order == null) throw new ArgumentException("Original slot timelines are invalid.");
                SlotCount = slotCount; Tracks = new AttachmentTrack[tracks.Length]; Order = new DrawOrderKey[order.Length];
                var occupied = new HashSet<int>();
                for (int i = 0; i < tracks.Length; i++)
                {
                    AttachmentTrack track = tracks[i];
                    if (track == null || track.slot < 0 || track.slot >= slotCount || !occupied.Add(track.slot) || track.keys == null || track.keys.Length == 0)
                        throw new ArgumentException("Original attachment timeline binding is invalid.");
                    var keys = new AttachmentKey[track.keys.Length]; float previous = float.NegativeInfinity;
                    for (int j = 0; j < keys.Length; j++)
                    {
                        AttachmentKey key = track.keys[j] ?? throw new ArgumentException("Original attachment key is null.");
                        Finite(key.time); if (key.time < previous) throw new ArgumentException("Original attachment key order is invalid.");
                        previous = key.time; keys[j] = new AttachmentKey { time = key.time, name = key.name };
                    }
                    Tracks[i] = new AttachmentTrack { slot = track.slot, keys = keys };
                }
                float last = float.NegativeInfinity;
                for (int i = 0; i < order.Length; i++)
                {
                    DrawOrderKey key = order[i] ?? throw new ArgumentException("Original draw order key is null.");
                    Finite(key.time);
                    if (key.time < last || key.slots == null || key.slots.Length != slotCount) throw new ArgumentException("Original draw order timeline is invalid.");
                    last = key.time; var seen = new bool[slotCount];
                    foreach (int slot in key.slots)
                    {
                        if (slot < 0 || slot >= slotCount || seen[slot]) throw new ArgumentException("Original draw order is not a permutation.");
                        seen[slot] = true;
                    }
                    Order[i] = new DrawOrderKey { time = key.time, slots = (int[])key.slots.Clone() };
                }
            }
        }
        private readonly Slot[] source;
        private readonly ProfessionNativeBonePose bones;
        private readonly string[] attachments;
        private readonly int[] order;
        public int Count => source.Length;
        public string Name(int slot) => source[slot].name;
        public int Bone(int slot) => source[slot].bone;
        public Color32 Color(int slot) => source[slot].color;
        public bool HasDarkColor(int slot) => source[slot].hasDarkColor;
        public Color32 DarkColor(int slot) => source[slot].darkColor;
        public int Blend(int slot) => source[slot].blend;
        public string Attachment(int slot) => attachments[slot];
        public int DrawSlot(int index) => order[index];

        public ProfessionNativeSlotPose(Slot[] slots, ProfessionNativeBonePose bones)
        {
            if (slots == null || slots.Length == 0 || bones == null) throw new ArgumentException("Original slot setup is missing.");
            this.bones = bones; source = new Slot[slots.Length]; attachments = new string[slots.Length]; order = new int[slots.Length];
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.name) || !names.Add(slot.name) || slot.bone < 0 || slot.bone >= bones.Count || slot.blend < 0 || slot.blend > 3)
                    throw new ArgumentException("Original slot name, bone or blend is invalid.");
                source[i] = new Slot { name = slot.name, attachment = slot.attachment, bone = slot.bone, blend = slot.blend,
                    color = slot.color, darkColor = slot.darkColor, hasDarkColor = slot.hasDarkColor };
            }
            Reset();
        }
        public void Reset()
        {
            for (int i = 0; i < Count; i++) { attachments[i] = source[i].attachment; order[i] = i; }
        }
        public void Apply(Clip clip, float time)
        {
            if (clip == null || clip.SlotCount != Count) throw new ArgumentException("Original slot clip layout differs.");
            Finite(time); Reset();
            foreach (AttachmentTrack track in clip.Tracks)
            {
                if (!bones.IsActive(source[track.slot].bone)) continue;
                foreach (AttachmentKey key in track.keys)
                { if (key.time > time) break; attachments[track.slot] = key.name; }
            }
            DrawOrderKey selected = null;
            foreach (DrawOrderKey key in clip.Order) { if (key.time > time) break; selected = key; }
            if (selected != null) Array.Copy(selected.slots, order, Count);
        }
        private static void Finite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Original slot time must be finite."); }
    }
}
