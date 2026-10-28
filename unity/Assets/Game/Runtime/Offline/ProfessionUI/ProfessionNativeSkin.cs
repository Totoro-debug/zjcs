using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Native attachment identity and membership tables from the exported data.
    // Geometry/material objects remain shared; combining skins does not clone them.
    public sealed class ProfessionNativeSkin : IProfessionAppearanceSkin
    {
        public sealed class Entry
        {
            public readonly int Slot;
            public readonly string Key;
            public readonly object Attachment;
            public Entry(int slot, string key, object attachment) { Slot = slot; Key = key; Attachment = attachment; }
        }
        private readonly int slotCount, boneCount, constraintCount;
        private readonly Dictionary<(int, string), object> attachments = new Dictionary<(int, string), object>();
        private readonly List<int> bones = new List<int>(), constraints = new List<int>();
        private readonly HashSet<int> boneSet = new HashSet<int>(), constraintSet = new HashSet<int>();
        public string Name { get; }
        public int SlotCount => slotCount;
        public int BoneCount => boneCount;
        public int ConstraintCount => constraintCount;
        public int AttachmentCount => attachments.Count;
        public IEnumerable<Entry> Entries
        {
            get
            {
                foreach (var item in attachments)
                    yield return new Entry(item.Key.Item1, item.Key.Item2, item.Value);
            }
        }
        public ReadOnlyCollection<int> Bones { get; }
        public ReadOnlyCollection<int> Constraints { get; }

        public ProfessionNativeSkin(string name, int slotCount, int boneCount, int constraintCount,
            Entry[] entries, int[] skinBones, int[] skinConstraints)
        {
            if (name == null || slotCount <= 0 || boneCount <= 0 || constraintCount < 0 || entries == null || skinBones == null || skinConstraints == null)
                throw new ArgumentException("Original skin layout or membership is invalid.");
            Name = name; this.slotCount = slotCount; this.boneCount = boneCount; this.constraintCount = constraintCount;
            Bones = bones.AsReadOnly(); Constraints = constraints.AsReadOnly();
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.Slot < 0 || entry.Slot >= slotCount || entry.Key == null || entry.Attachment == null)
                    throw new ArgumentException("Original skin attachment key or identity is invalid.");
                if (attachments.ContainsKey((entry.Slot, entry.Key))) throw new ArgumentException("Original skin has a duplicate attachment key.");
                attachments.Add((entry.Slot, entry.Key), entry.Attachment);
            }
            AddMembers(skinBones, boneCount, bones, boneSet);
            AddMembers(skinConstraints, constraintCount, constraints, constraintSet);
        }
        public void Clear()
        { attachments.Clear(); bones.Clear(); constraints.Clear(); boneSet.Clear(); constraintSet.Clear(); }
        public void AddSkin(IProfessionAppearanceSkin source)
        {
            var skin = source as ProfessionNativeSkin;
            if (skin == null || skin.slotCount != slotCount || skin.boneCount != boneCount || skin.constraintCount != constraintCount)
                throw new ArgumentException("Combined original skins must belong to the same skeleton layout.");
            if (ReferenceEquals(skin, this)) return;
            foreach (var entry in skin.attachments) attachments[entry.Key] = entry.Value;
            AddMembers(skin.bones, boneCount, bones, boneSet);
            AddMembers(skin.constraints, constraintCount, constraints, constraintSet);
        }
        public object GetAttachment(int slot, string name)
        {
            if (slot < 0 || slot >= slotCount || name == null) throw new ArgumentException("Original skin lookup key is invalid.");
            return attachments.TryGetValue((slot, name), out object attachment) ? attachment : null;
        }
        private static void AddMembers(IEnumerable<int> source, int count, List<int> ordered, HashSet<int> members)
        {
            foreach (int index in source)
            {
                if (index < 0 || index >= count) throw new ArgumentException("Original skin membership index is outside its skeleton.");
                if (members.Add(index)) ordered.Add(index);
            }
        }
    }
    public sealed class ProfessionNativeSkinCatalog : IProfessionAppearanceSkeletonData
    {
        private readonly Dictionary<string, ProfessionNativeSkin> skins = new Dictionary<string, ProfessionNativeSkin>(StringComparer.Ordinal);
        private readonly ProfessionNativeSkin defaultSkin;
        public ProfessionNativeSkinCatalog(ProfessionNativeSkin[] skins)
        {
            if (skins == null) throw new ArgumentNullException(nameof(skins));
            foreach (ProfessionNativeSkin skin in skins)
            {
                if (skin == null || this.skins.ContainsKey(skin.Name)) throw new ArgumentException("Original skin names must be unique.");
                this.skins.Add(skin.Name, skin);
            }
            this.skins.TryGetValue("default", out defaultSkin);
        }
        public IProfessionAppearanceSkin FindSkin(string name)
        { return skins.TryGetValue(name, out ProfessionNativeSkin skin) ? skin : null; }
        public object ResolveAttachment(ProfessionNativeSkin active, int slot, string name)
        { return name == null ? null : active?.GetAttachment(slot, name) ?? defaultSkin?.GetAttachment(slot, name); }
    }
}
