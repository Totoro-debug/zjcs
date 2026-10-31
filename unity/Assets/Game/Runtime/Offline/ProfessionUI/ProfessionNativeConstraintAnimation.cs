using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Samples original animation constraints. World solving, physics and mixing
    // remain separate stages; a sampled state is not a rendered skeleton pose.
    public sealed class ProfessionNativeConstraintAnimation
    {
        public sealed class IkState
        {
            public string Name;
            public int Order, Target;
            public int[] Bones;
            public float Mix, Softness, ChildLength;
            public bool BendPositive, Stretch, Compress, Uniform;
        }

        private sealed class Timeline
        {
            public Dictionary<string, object>[] Rows;
            public ProfessionNativeCurve[] Curves;
        }

        private readonly ProfessionNativeSkeletonData data;
        private readonly Dictionary<string, Timeline> ik = new Dictionary<string, Timeline>(StringComparer.Ordinal);
        private readonly Dictionary<string, Timeline> transforms = new Dictionary<string, Timeline>(StringComparer.Ordinal);
        private static readonly string[] IkAxes = { "mix", "softness" };
        private static readonly float[] IkDefaults = { 1, 0 };
        private static readonly string[] TransformAxes = { "mixRotate", "mixX", "mixY", "mixScaleX", "mixScaleY", "mixShearY" };
        private static readonly float[] TransformDefaults = { 1, 1, 1, 1, 1, 0 };
        public string Name { get; }

        public ProfessionNativeConstraintAnimation(ProfessionNativeSkeletonData data, string animation)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            Name = animation;
            var fields = data.GetAnimation(animation);
            Bind(fields, "ik", IkAxes, IkDefaults, ik);
            Bind(fields, "transform", TransformAxes, TransformDefaults, transforms);
        }

        public IkState[] SampleIk(float time, IReadOnlyList<int> skinConstraints)
        {
            var selected = Selection(time, skinConstraints);
            var result = new List<IkState>();
            var bones = (List<object>)data.SourceFields["bones"];
            for (int index = 0; index < data.Constraints.Count; index++)
            {
                var definition = data.Constraints[index];
                var row = definition.Fields;
                if (definition.Kind != "ik" || Bool(row, "skin") && !selected.Contains(index)) continue;
                var chain = ((List<object>)row["bones"]).Select(value => Bone((string)value)).ToArray();
                var state = new IkState { Name = definition.Name, Order = (int)Number(row, "order"),
                    Target = Bone((string)row["target"]), Bones = chain, Mix = Number(row, "mix", 1),
                    Softness = Number(row, "softness"), BendPositive = Bool(row, "bendPositive", true),
                    Stretch = Bool(row, "stretch"), Compress = Bool(row, "compress"), Uniform = Bool(row, "uniform"),
                    ChildLength = chain.Length == 2 ? Number(Map(bones[chain[1]]), "length") : 0 };
                if (ik.TryGetValue(definition.Name, out var timeline))
                {
                    if (timeline.Curves[0].TryEvaluate(time, out float mix)) state.Mix = mix;
                    if (timeline.Curves[1].TryEvaluate(time, out float softness)) state.Softness = softness;
                    // Flags are discrete; interpolation affects only mix/softness.
                    for (int frame = timeline.Rows.Length - 1; frame >= 0; frame--)
                    {
                        var key = timeline.Rows[frame];
                        if (Number(key, "time") > time) continue;
                        state.BendPositive = Bool(key, "bendPositive", true);
                        state.Stretch = Bool(key, "stretch"); state.Compress = Bool(key, "compress");
                        break;
                    }
                }
                result.Add(state);
            }
            return result.OrderBy(state => state.Order).ToArray();
        }

        public ProfessionNativeIdleTransform.Constraint[] SampleTransforms(float time, IReadOnlyList<int> skinConstraints)
        {
            var selected = Selection(time, skinConstraints);
            var result = new List<ProfessionNativeIdleTransform.Constraint>();
            for (int index = 0; index < data.Constraints.Count; index++)
            {
                var definition = data.Constraints[index]; var row = definition.Fields;
                if (definition.Kind != "transform" || Bool(row, "skin") && !selected.Contains(index)) continue;
                var values = TransformAxes.Select(axis => Number(row, axis)).ToArray();
                if (transforms.TryGetValue(definition.Name, out var timeline))
                    for (int axis = 0; axis < values.Length; axis++)
                        if (timeline.Curves[axis].TryEvaluate(time, out float value)) values[axis] = value;
                result.Add(new ProfessionNativeIdleTransform.Constraint { Name = definition.Name, Order = (int)Number(row, "order"),
                    Target = Bone((string)row["target"]), Bones = ((List<object>)row["bones"]).Select(value => Bone((string)value)).ToArray(),
                    Local = Bool(row, "local"), Relative = Bool(row, "relative"),
                    OffsetPosition = new Vector2(Number(row, "x"), Number(row, "y")),
                    OffsetScale = new Vector2(Number(row, "scaleX"), Number(row, "scaleY")),
                    OffsetRotation = Number(row, "rotation"), OffsetShear = Number(row, "shearY"),
                    Rotation = values[0], X = values[1], Y = values[2], ScaleX = values[3], ScaleY = values[4], Shear = values[5] });
            }
            return result.OrderBy(state => state.Order).ToArray();
        }

        private static void Bind(IReadOnlyDictionary<string, object> animation, string kind, string[] axes,
            float[] defaults, Dictionary<string, Timeline> destination)
        {
            if (!animation.TryGetValue(kind, out object value)) return;
            foreach (var pair in Map(value))
            {
                var rows = ((List<object>)pair.Value).Select(Map).ToArray();
                var curves = new ProfessionNativeCurve[axes.Length];
                for (int axis = 0; axis < axes.Length; axis++)
                {
                    var keys = new ProfessionNativeCurve.Key[rows.Length];
                    for (int index = 0; index < rows.Length; index++)
                    {
                        var row = rows[index];
                        var key = new ProfessionNativeCurve.Key { time = Number(row, "time"), value = Number(row, axes[axis], defaults[axis]) };
                        if (row.TryGetValue("curve", out object interpolation))
                        {
                            if (interpolation is string text)
                            {
                                if (text != "stepped") throw new InvalidDataException("Unknown original constraint interpolation.");
                                key.mode = 1;
                            }
                            else
                            {
                                var controls = (List<object>)interpolation;
                                if (controls.Count != axes.Length * 4) throw new InvalidDataException("Original constraint Bezier handle count differs.");
                                key.mode = 2;
                                for (int component = 0; component < 4; component++) key.controls[component] = Convert.ToSingle(controls[axis * 4 + component], CultureInfo.InvariantCulture);
                            }
                        }
                        keys[index] = key;
                    }
                    curves[axis] = new ProfessionNativeCurve(keys);
                }
                destination.Add(pair.Key, new Timeline { Rows = rows, Curves = curves });
            }
        }

        private HashSet<int> Selection(float time, IReadOnlyList<int> skinConstraints)
        {
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (skinConstraints == null) throw new ArgumentNullException(nameof(skinConstraints));
            var selected = new HashSet<int>();
            foreach (int index in skinConstraints)
            {
                if (index < 0 || index >= data.Constraints.Count) throw new ArgumentOutOfRangeException(nameof(skinConstraints));
                selected.Add(index);
            }
            return selected;
        }

        private int Bone(string name)
        { int index = data.FindBone(name); return index >= 0 ? index : throw new InvalidDataException("Original constraint bone is missing: " + name); }
        private static Dictionary<string, object> Map(object value) => value as Dictionary<string, object> ?? throw new InvalidDataException("Original constraint fields are not an object.");
        private static float Number(IReadOnlyDictionary<string, object> row, string name, float fallback = 0)
            => row.TryGetValue(name, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        private static bool Bool(IReadOnlyDictionary<string, object> row, string name, bool fallback = false)
            => row.TryGetValue(name, out object value) ? (bool)value : fallback;
    }
}
