using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Zjcs.Offline.ProfessionUI
{
    // Keep source-unit sparse offsets separate from shared attachment data.
    // Each interval has one original percent curve for all deform components.
    public sealed class ProfessionNativeDeformAnimation
    {
        private sealed class Track
        {
            public int Slot;
            public ProfessionNativeAttachment Attachment;
            public Key[] Keys;
        }
        private sealed class Key
        {
            public float Time;
            public IReadOnlyList<float> Values;
            public ProfessionNativeCurve Transition;
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
                        if (keys.Count == 0) throw new InvalidDataException("Original deform timeline is empty.");
                        var sampled = new Key[keys.Count];
                        for (int frame = 0; frame < keys.Count; frame++)
                        {
                            var key = Map(keys[frame]);
                            var values = new float[original.DeformValueCount];
                            int offset = key.TryGetValue("offset", out object start) ? Convert.ToInt32(start, CultureInfo.InvariantCulture) : 0;
                            var source = key.TryGetValue("vertices", out object vertices) ? (List<object>)vertices : new List<object>();
                            if (offset < 0 || offset > values.Length || source.Count > values.Length - offset)
                                throw new InvalidDataException("Original deform sparse range exceeds attachment influences.");
                            for (int component = 0; component < source.Count; component++)
                            {
                                float number = Convert.ToSingle(source[component], CultureInfo.InvariantCulture);
                                if (float.IsNaN(number) || float.IsInfinity(number)) throw new InvalidDataException("Original deform value is not finite.");
                                values[offset + component] = number;
                            }
                            float time = Number(key, "time");
                            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0 || frame > 0 && time <= sampled[frame - 1].Time)
                                throw new InvalidDataException("Original deform key time is invalid or unordered.");
                            sampled[frame] = new Key { Time = time, Values = Array.AsReadOnly(values) };
                        }
                        for (int frame = 0; frame < sampled.Length - 1; frame++)
                        {
                            var row = Map(keys[frame]);
                            var first = new ProfessionNativeCurve.Key { time = sampled[frame].Time, value = 0 };
                            if (row.TryGetValue("curve", out object interpolation))
                            {
                                if (interpolation is string text)
                                {
                                    if (text != "stepped") throw new InvalidDataException("Unknown original deform interpolation.");
                                    first.mode = 1;
                                }
                                else
                                {
                                    var controls = (List<object>)interpolation;
                                    if (controls.Count != 4) throw new InvalidDataException("Original deform percent handle count differs.");
                                    first.mode = 2;
                                    for (int axis = 0; axis < 4; axis++) first.controls[axis] = Convert.ToSingle(controls[axis], CultureInfo.InvariantCulture);
                                }
                            }
                            sampled[frame].Transition = new ProfessionNativeCurve(new[] { first,
                                new ProfessionNativeCurve.Key { time = sampled[frame + 1].Time, value = 1 } });
                        }
                        if (tracks.Any(track => track.Slot == index && ReferenceEquals(track.Attachment, original)))
                            throw new InvalidDataException("Original deform attachment has multiple bindings.");
                        tracks.Add(new Track { Slot = index, Attachment = original, Keys = sampled });
                    }
                }
            }
        }

        public IReadOnlyList<float> Sample(int slot, ProfessionNativeAttachment attachment, float time)
        {
            if (slot < 0 || slot >= slotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            foreach (var track in tracks)
                if (track.Slot == slot && ReferenceEquals(track.Attachment, attachment) && time >= track.Keys[0].Time)
                {
                    int index = track.Keys.Length - 1;
                    while (index > 0 && track.Keys[index].Time > time) index--;
                    var first = track.Keys[index];
                    if (first.Transition == null || !first.Transition.TryEvaluate(time, out float mix) || mix == 0) return first.Values;
                    var next = track.Keys[index + 1].Values;
                    var result = new float[next.Count];
                    for (int component = 0; component < result.Length; component++)
                        result[component] = first.Values[component] + (next[component] - first.Values[component]) * mix;
                    return Array.AsReadOnly(result);
                }
            return null;
        }

        private static Dictionary<string, object> Map(object value)
            => value as Dictionary<string, object> ?? throw new InvalidDataException("Original deform fields are not an object.");
        private static float Number(Dictionary<string, object> value, string name)
            => value.TryGetValue(name, out object number) ? Convert.ToSingle(number, CultureInfo.InvariantCulture) : 0;
    }
}
