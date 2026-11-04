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
        private sealed class ColorTrack { public int Slot, FirstAxis; public ProfessionNativeCurve[] Channels; }
        private readonly ProfessionNativeSkeletonData data;
        private readonly ProfessionNativeBonePose.Track[] bones;
        private readonly ProfessionNativeSlotPose.Clip slots;
        private readonly InheritTrack[] inheritance;
        private readonly ColorTrack[] colors;
        private readonly Color[] setupColors;
        private ProfessionNativeConstraintAnimation constraints;
        private ProfessionNativeDeformAnimation deforms;
        private ProfessionNativePhysicsAnimation physics;
        private readonly bool originalBezierSegments;
        public string Name { get; }
        public float Duration { get; }
        public IReadOnlyList<ProfessionNativeBonePose.Track> BoneTracks => bones;
        public int InheritanceTrackCount => inheritance.Length;
        public int ColorTrackCount => colors.Length;
        public IReadOnlyDictionary<string, object> OriginalFields { get; }
        internal bool OriginalBezierSegments => originalBezierSegments;
        public ProfessionNativeConstraintAnimation Constraints {
            get { if (constraints == null) { constraints = new ProfessionNativeConstraintAnimation(data, Name);
                if (originalBezierSegments) constraints.UseOriginalBezierSegments(); } return constraints; }
        }
        public ProfessionNativeDeformAnimation Deforms {
            get { if (deforms == null) { deforms = new ProfessionNativeDeformAnimation(data, Name);
                if (originalBezierSegments) deforms.UseOriginalBezierSegments(); } return deforms; }
        }
        public ProfessionNativePhysicsAnimation Physics {
            get { if (physics == null) { physics = new ProfessionNativePhysicsAnimation(data, Name);
                if (originalBezierSegments) physics.UseOriginalBezierSegments(); } return physics; }
        }

        public ProfessionNativeAnimationChannels(ProfessionNativeSkeletonData data, string name, bool originalBezierSegments = false)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            Name = name; OriginalFields = data.GetAnimation(name);
            this.originalBezierSegments = originalBezierSegments;
            bones = data.CreateBoneTracks(name); slots = data.CreateSlotClip(name, true);
            if (originalBezierSegments) foreach (var track in bones) track.Curve.UseOriginalBezierSegments();
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
                    if (channel.Key != "rgba" && channel.Key != "alpha") throw new NotSupportedException("Bind original color channel: " + channel.Key);
                    bool alpha = channel.Key == "alpha";
                    var rows = Rows(channel.Value).ToArray();
                    var curves = new ProfessionNativeCurve[alpha ? 1 : 4];
                    for (int axis = 0; axis < curves.Length; axis++)
                    {
                        var keys = new ProfessionNativeCurve.Key[rows.Length];
                        for (int i = 0; i < rows.Length; i++)
                        {
                            var row = rows[i];
                            var key = new ProfessionNativeCurve.Key { time = Number(row, "time"),
                                value = alpha ? (row.TryGetValue("alpha", out object opacity) ? Convert.ToSingle(opacity, CultureInfo.InvariantCulture) : 1)
                                    : ParseColor((string)row["color"])[axis] };
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
                                    if (handles.Count != curves.Length * 4) throw new InvalidDataException("Original color handle count differs.");
                                    key.mode = 2;
                                    for (int j = 0; j < 4; j++) key.controls[j] = Convert.ToSingle(handles[axis * 4 + j], CultureInfo.InvariantCulture);
                                }
                            }
                            keys[i] = key;
                        }
                        curves[axis] = new ProfessionNativeCurve(keys);
                        if (originalBezierSegments) curves[axis].UseOriginalBezierSegments();
                    }
                    colorTracks.Add(new ColorTrack { Slot = data.FindSlot(slot.Key), FirstAxis = alpha ? 3 : 0, Channels = curves });
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
            slotPose.Apply(slots, time); System.Array.Copy(setupColors, slotColors, setupColors.Length);
            ApplyDiscreteAndColors(bonePose, slotPose, slotColors, time, 1);
        }

        internal void ApplyReplace(ProfessionNativeBonePose bonePose, ProfessionNativeSlotPose slotPose,
            Color[] slotColors, float time, float alpha)
        {
            bonePose.ApplyReplace(bones, time, alpha);
            if (alpha == 0) return;
            slotPose.ApplyReplace(slots, time);
            ApplyDiscreteAndColors(bonePose, slotPose, slotColors, time, alpha);
        }

        private void ApplyDiscreteAndColors(ProfessionNativeBonePose bonePose, ProfessionNativeSlotPose slotPose,
            Color[] slotColors, float time, float alpha)
        {
            foreach (var track in inheritance)
            {
                if (!bonePose.IsActive(track.Bone)) continue;
                foreach (var key in track.Keys)
                { if (key.Time > time) break; bonePose.SetInheritance(track.Bone, key.Mode); }
            }
            foreach (var track in colors)
            {
                if (!bonePose.IsActive(slotPose.Bone(track.Slot))) continue;
                Color color = slotColors[track.Slot];
                for (int axis = 0; axis < track.Channels.Length; axis++)
                    if (track.Channels[axis].TryEvaluate(time, out float value))
                        color[track.FirstAxis + axis] = ProfessionNativeBonePose.MixValue(color[track.FirstAxis + axis], value, alpha);
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
