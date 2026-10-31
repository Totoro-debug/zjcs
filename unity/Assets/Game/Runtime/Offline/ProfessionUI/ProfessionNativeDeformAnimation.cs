using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Zjcs.Offline.ProfessionUI
{
    // The eight verified player attachment timelines each contain one original
    // deform key. Keep source-unit offsets separate from shared attachment data.
    public sealed class ProfessionNativeDeformAnimation
    {
        private sealed class Track
        {
            public int Slot;
            public ProfessionNativeAttachment Attachment;
            public float Time;
            public IReadOnlyList<float> Values;
        }
        private readonly List<Track> tracks = new List<Track>();
        private readonly int slotCount;
        public int TrackCount => tracks.Count;

        public ProfessionNativeDeformAnimation(ProfessionNativeSkeletonData data, string animation)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            slotCount = data.SlotCount;
            if (!data.GetAnimation(animation).TryGetValue("attachments", out object value)) return;
            foreach (var skin in Map(value))
            {
                var sourceSkin = data.SkinCatalog.FindSkin(skin.Key) as ProfessionNativeSkin
                    ?? throw new InvalidDataException("Original deform skin is absent: " + skin.Key);
                foreach (var slot in Map(skin.Value))
                {
                    int index = data.FindSlot(slot.Key);
                    if (index < 0) throw new InvalidDataException("Original deform slot is absent: " + slot.Key);
                    foreach (var attachment in Map(slot.Value))
                    {
                        var original = sourceSkin.GetAttachment(index, attachment.Key) as ProfessionNativeAttachment;
                        if (original == null || original.DeformValueCount == 0)
                            throw new InvalidDataException("Original deform attachment layout is unavailable: " + attachment.Key);
                        var channels = Map(attachment.Value);
                        if (channels.Count != 1 || !channels.TryGetValue("deform", out object frames))
                            throw new NotSupportedException("Restore additional original attachment animation channels.");
                        var keys = frames as List<object> ?? throw new InvalidDataException("Original deform timeline is not an array.");
                        if (keys.Count != 1) throw new NotSupportedException("Verify additional original multiframe attachment deformation.");
                        var key = Map(keys[0]);
                        var values = new float[original.DeformValueCount];
                        int offset = key.TryGetValue("offset", out object start) ? Convert.ToInt32(start, CultureInfo.InvariantCulture) : 0;
                        var source = key.TryGetValue("vertices", out object vertices)
                            ? (List<object>)vertices : new List<object>();
                        if (offset < 0 || offset > values.Length || source.Count > values.Length - offset)
                            throw new InvalidDataException("Original deform sparse range exceeds attachment influences.");
                        for (int component = 0; component < source.Count; component++)
                        {
                            float number = Convert.ToSingle(source[component], CultureInfo.InvariantCulture);
                            if (float.IsNaN(number) || float.IsInfinity(number)) throw new InvalidDataException("Original deform value is not finite.");
                            values[offset + component] = number;
                        }
                        float time = key.TryGetValue("time", out object position) ? Convert.ToSingle(position, CultureInfo.InvariantCulture) : 0;
                        if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new InvalidDataException("Original deform key time is invalid.");
                        if (tracks.Any(track => track.Slot == index && ReferenceEquals(track.Attachment, original)))
                            throw new InvalidDataException("Original deform attachment has multiple bindings.");
                        tracks.Add(new Track { Slot = index, Attachment = original, Time = time, Values = Array.AsReadOnly(values) });
                    }
                }
            }
        }

        public IReadOnlyList<float> Sample(int slot, ProfessionNativeAttachment attachment, float time)
        {
            if (slot < 0 || slot >= slotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            foreach (var track in tracks)
                if (track.Slot == slot && ReferenceEquals(track.Attachment, attachment) && time >= track.Time)
                    return track.Values;
            return null;
        }

        private static Dictionary<string, object> Map(object value)
            => value as Dictionary<string, object> ?? throw new InvalidDataException("Original deform fields are not an object.");
    }
}
