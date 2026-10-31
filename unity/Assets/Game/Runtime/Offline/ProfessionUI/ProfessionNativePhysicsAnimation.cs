using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Original physics setup and unmixed parameter timelines only. These values
    // do not advance a physical simulation or modify bone world matrices.
    public sealed class ProfessionNativePhysicsAnimation
    {
        public sealed class State
        {
            public string Name;
            public int ConstraintIndex, Order, Bone;
            public bool SkinRequired;
            public float X, Y, Rotate, ScaleX, ShearX, Limit, Step;
            public float Inertia, Strength, Damping, MassInverse, Wind, Gravity, Mix;
            public bool InertiaGlobal, StrengthGlobal, DampingGlobal, MassGlobal, WindGlobal, GravityGlobal, MixGlobal;
            public IReadOnlyDictionary<string, object> OriginalFields;
        }

        private readonly ProfessionNativeSkeletonData data;
        private readonly Dictionary<string, ProfessionNativeCurve> mixes = new Dictionary<string, ProfessionNativeCurve>(StringComparer.Ordinal);
        public string Name { get; }
        public int TrackCount => mixes.Count;

        public ProfessionNativePhysicsAnimation(ProfessionNativeSkeletonData data, string animation)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            Name = animation;
            var fields = data.GetAnimation(animation);
            if (!fields.TryGetValue("physics", out object value)) return;
            foreach (var constraint in Map(value))
            {
                if (!data.Constraints.Any(row => row.Kind == "physics" && row.Name == constraint.Key))
                    throw new NotSupportedException("Bind original physics timeline target: " + constraint.Key);
                foreach (var channel in Map(constraint.Value))
                {
                    if (channel.Key != "mix") throw new NotSupportedException("Bind original physics channel: " + channel.Key);
                    var rows = ((List<object>)channel.Value).Select(Map).ToArray();
                    var keys = new ProfessionNativeCurve.Key[rows.Length];
                    for (int i = 0; i < rows.Length; i++)
                    {
                        var row = rows[i];
                        // The lossless binary projection omits zero timeline
                        // values, unlike the setup mix whose absent value is one.
                        var key = new ProfessionNativeCurve.Key { time = Number(row, "time"), value = Number(row, "value") };
                        if (row.TryGetValue("curve", out object curve))
                        {
                            if (curve is string text)
                            {
                                if (text != "stepped") throw new InvalidDataException("Unknown original physics interpolation.");
                                key.mode = 1;
                            }
                            else
                            {
                                var handles = (List<object>)curve;
                                if (handles.Count != 4) throw new InvalidDataException("Original physics Bezier handle count differs.");
                                key.mode = 2;
                                for (int axis = 0; axis < 4; axis++) key.controls[axis] = Convert.ToSingle(handles[axis], CultureInfo.InvariantCulture);
                            }
                        }
                        keys[i] = key;
                    }
                    mixes.Add(constraint.Key, new ProfessionNativeCurve(keys));
                }
            }
        }

        // Inspection of one source constraint, independently of skin selection.
        public State Sample(string constraint, float time)
        {
            ValidateTime(time);
            for (int i = 0; i < data.Constraints.Count; i++)
                if (data.Constraints[i].Kind == "physics" && data.Constraints[i].Name == constraint) return Sample(i, time);
            throw new ArgumentException("Original physics constraint is missing: " + constraint, nameof(constraint));
        }

        public State[] Sample(float time, IReadOnlyList<int> skinConstraints)
        {
            ValidateTime(time);
            if (skinConstraints == null) throw new ArgumentNullException(nameof(skinConstraints));
            var selected = new HashSet<int>();
            foreach (int index in skinConstraints)
            {
                if (index < 0 || index >= data.Constraints.Count) throw new ArgumentOutOfRangeException(nameof(skinConstraints));
                selected.Add(index);
            }
            var result = new List<State>();
            for (int i = 0; i < data.Constraints.Count; i++)
            {
                var definition = data.Constraints[i];
                if (definition.Kind == "physics" && (!Bool(definition.Fields, "skin") || selected.Contains(i))) result.Add(Sample(i, time));
            }
            return result.OrderBy(state => state.Order).ToArray();
        }

        private State Sample(int index, float time)
        {
            var definition = data.Constraints[index]; var row = definition.Fields;
            int bone = data.FindBone((string)row["bone"]);
            if (bone < 0) throw new InvalidDataException("Original physics bone is missing: " + row["bone"]);
            double mass = row.TryGetValue("mass", out object value) ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 1;
            float inverse = (float)(mass == 0 ? 0 : 1 / mass);
            if (double.IsNaN(mass) || double.IsInfinity(mass) || float.IsInfinity(inverse)) throw new InvalidDataException("Original physics mass is not finite.");
            var state = new State { Name = definition.Name, ConstraintIndex = index, Order = (int)Number(row, "order"), Bone = bone,
                SkinRequired = Bool(row, "skin"), X = Number(row, "x"), Y = Number(row, "y"), Rotate = Number(row, "rotate"),
                ScaleX = Number(row, "scaleX"), ShearX = Number(row, "shearX"), Limit = Number(row, "limit", 5000), Step = Number(row, "step"),
                Inertia = Number(row, "inertia"), Strength = Number(row, "strength"), Damping = Number(row, "damping"),
                MassInverse = inverse, Wind = Number(row, "wind"), Gravity = Number(row, "gravity"), Mix = Number(row, "mix", 1),
                InertiaGlobal = Bool(row, "inertiaGlobal"), StrengthGlobal = Bool(row, "strengthGlobal"), DampingGlobal = Bool(row, "dampingGlobal"),
                MassGlobal = Bool(row, "massGlobal"), WindGlobal = Bool(row, "windGlobal"), GravityGlobal = Bool(row, "gravityGlobal"),
                MixGlobal = Bool(row, "mixGlobal"), OriginalFields = row };
            if (mixes.TryGetValue(state.Name, out var curve) && curve.TryEvaluate(time, out float mix)) state.Mix = mix;
            return state;
        }

        private static void ValidateTime(float time)
        { if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time)); }
        private static Dictionary<string, object> Map(object value) => value as Dictionary<string, object> ?? throw new InvalidDataException("Original physics fields are not an object.");
        private static bool Bool(IReadOnlyDictionary<string, object> row, string name) => row.TryGetValue(name, out object value) && (bool)value;
        private static float Number(IReadOnlyDictionary<string, object> row, string name, float fallback = 0)
        {
            float result = row.TryGetValue(name, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new InvalidDataException("Original physics value is not finite: " + name);
            return result;
        }
    }
}
