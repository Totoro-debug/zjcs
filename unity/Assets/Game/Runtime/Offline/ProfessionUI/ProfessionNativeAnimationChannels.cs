using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independently bind the original local channels. Constraint solving, mixing,
    // deformation and rendering consume the same source graph in later stages.
    public sealed class ProfessionNativeAnimationChannels
    {
        private sealed class InheritKey { public float Time; public int Mode; }
        private sealed class InheritTrack { public int Bone; public InheritKey[] Keys; }
        private sealed class ColorTrack { public int Slot; public ProfessionNativeCurve[] Channels; }
        private readonly ProfessionNativeSkeletonData data;
        private readonly ProfessionNativeBonePose.Track[] bones;
        private readonly ProfessionNativeSlotPose.Clip slots;
        private readonly InheritTrack[] inheritance;
        private readonly ColorTrack[] colors;
        private readonly Color[] setupColors;
        public string Name { get; }
        public float Duration { get; }
        public IReadOnlyList<ProfessionNativeBonePose.Track> BoneTracks => bones;
        public int InheritanceTrackCount => inheritance.Length;
        public int ColorTrackCount => colors.Length;
        public IReadOnlyDictionary<string, object> OriginalFields { get; }

        public ProfessionNativeAnimationChannels(ProfessionNativeSkeletonData data, string name)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            Name = name; OriginalFields = data.GetAnimation(name);
            bones = data.CreateBoneTracks(name); slots = data.CreateSlotClip(name, true);
            Duration = LastTime(OriginalFields);
            string[] modes = { "normal", "onlyTranslation", "noRotationOrReflection", "noScale", "noScaleOrReflection" };
            var inheritTracks = new List<InheritTrack>();
            foreach (var bone in Children(OriginalFields, "bones"))
            {
                var channels = Map(bone.Value);
                if (!channels.TryGetValue("inherit", out object value)) continue;
                var keys = Rows(value).Select(row =>
                {
                    int mode = System.Array.IndexOf(modes, (string)row["value"]);
                    if (mode < 0) throw new InvalidDataException("Unknown original animated inheritance mode.");
                    return new InheritKey { Time = Number(row, "time"), Mode = mode };
                }).ToArray();
                for (int i = 1; i < keys.Length; i++)
                    if (keys[i].Time <= keys[i - 1].Time) throw new InvalidDataException("Original inheritance keys are unordered.");
                inheritTracks.Add(new InheritTrack { Bone = data.FindBone(bone.Key), Keys = keys });
            }
            inheritance = inheritTracks.ToArray();
            setupColors = Rows(data.SourceFields["slots"]).Select(row =>
                ParseColor(row.TryGetValue("color", out object value) ? (string)value : "ffffffff")).ToArray();
            var colorTracks = new List<ColorTrack>();
            foreach (var slot in Children(OriginalFields, "slots"))
                foreach (var channel in Map(slot.Value))
                {
                    if (channel.Key == "attachment") continue;
                    if (channel.Key != "rgba") throw new NotSupportedException("Bind original color channel: " + channel.Key);
                    var rows = Rows(channel.Value).ToArray();
                    var curves = new ProfessionNativeCurve[4];
                    for (int axis = 0; axis < 4; axis++)
                    {
                        var keys = new ProfessionNativeCurve.Key[rows.Length];
                        for (int i = 0; i < rows.Length; i++)
                        {
                            var row = rows[i];
                            var key = new ProfessionNativeCurve.Key { time = Number(row, "time"),
                                value = ParseColor((string)row["color"])[axis] };
                            if (row.TryGetValue("curve", out object interpolation))
                            {
                                if (interpolation is string text)
                                {
                                    if (text != "stepped") throw new InvalidDataException("Unknown original color interpolation.");
                                    key.mode = 1;
                                }
                                else
                                {
                                    var handles = (List<object>)interpolation;
                                    if (handles.Count != 16) throw new InvalidDataException("Original RGBA handle count differs.");
                                    key.mode = 2;
                                    for (int j = 0; j < 4; j++) key.controls[j] = Convert.ToSingle(handles[axis * 4 + j], CultureInfo.InvariantCulture);
                                }
                            }
                            keys[i] = key;
                        }
                        curves[axis] = new ProfessionNativeCurve(keys);
                    }
                    colorTracks.Add(new ColorTrack { Slot = data.FindSlot(slot.Key), Channels = curves });
                }
            colors = colorTracks.ToArray();
        }

        // Explicitly named local-pose stage: callers must apply the original
        // ordered constraints before consuming world matrices or rendering.
        public void ApplyLocalPose(ProfessionNativeBonePose bonePose, ProfessionNativeSlotPose slotPose,
            Color[] slotColors, float time, bool loop)
        {
            if (bonePose == null || bonePose.Count != data.BoneCount || slotPose == null || slotPose.Count != data.SlotCount ||
                slotColors == null || slotColors.Length != data.SlotCount)
                throw new ArgumentException("Original animation destination layout differs.");
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (loop && Duration > 0) time %= Duration;
            bonePose.Apply(bones, time);
            foreach (var track in inheritance)
            {
                if (!bonePose.IsActive(track.Bone)) continue;
                foreach (var key in track.Keys)
                { if (key.Time > time) break; bonePose.SetInheritance(track.Bone, key.Mode); }
            }
            slotPose.Apply(slots, time); System.Array.Copy(setupColors, slotColors, setupColors.Length);
            foreach (var track in colors)
            {
                if (!bonePose.IsActive(slotPose.Bone(track.Slot))) continue;
                Color color = slotColors[track.Slot];
                for (int axis = 0; axis < 4; axis++)
                    if (track.Channels[axis].TryEvaluate(time, out float value)) color[axis] = value;
                slotColors[track.Slot] = color;
            }
        }

        private static Color ParseColor(string text)
        {
            if (text.Length != 8) throw new InvalidDataException("Original RGBA color must contain four channels.");
            var color = new Color();
            for (int i = 0; i < 4; i++) color[i] = byte.Parse(text.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
            return color;
        }
        private static float LastTime(object value)
        {
            float result = 0;
            if (value is IReadOnlyDictionary<string, object> map)
                foreach (var row in map) result = Mathf.Max(result, row.Key == "time" ? Convert.ToSingle(row.Value, CultureInfo.InvariantCulture) : LastTime(row.Value));
            else if (value is List<object> rows) foreach (var row in rows) result = Mathf.Max(result, LastTime(row));
            return result;
        }
        private static Dictionary<string, object> Map(object value) => value as Dictionary<string, object> ?? throw new InvalidDataException("Original channel object differs.");
        private static IEnumerable<Dictionary<string, object>> Rows(object value) => ((List<object>)value).Select(Map);
        private static IEnumerable<KeyValuePair<string, object>> Children(IReadOnlyDictionary<string, object> value, string key) =>
            value.TryGetValue(key, out object child) ? Map(child) : Enumerable.Empty<KeyValuePair<string, object>>();
        private static float Number(IReadOnlyDictionary<string, object> row, string key) =>
            row.TryGetValue(key, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : 0;
    }
}
