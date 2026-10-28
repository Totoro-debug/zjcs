using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent world-space geometry for the six verified original idle clips.
    // Config defaults and constant timeline defaults come from the pinned MIT
    // binary format reader, not from an animation SDK implementation.
    public static class ProfessionNativeIdleTransform
    {
        public sealed class Constraint
        {
            public string Name;
            public int Order, Target;
            public int[] Bones;
            public bool Local, Relative;
            public Vector2 OffsetPosition, OffsetScale;
            public float OffsetRotation, OffsetShear;
            public float Rotation, X, Y, ScaleX, ScaleY, Shear;
        }

        public static Constraint[] Bind(ProfessionNativeSkeletonData data, string animation, float time,
            IReadOnlyList<int> selectedConstraints)
        {
            if (data == null || selectedConstraints == null) throw new ArgumentNullException();
            Finite(time);
            if (animation != "home/idle_01" && animation != "100/idle_01" && animation != "200/idle_01")
                throw new NotSupportedException("Verify additional original transform animation: " + animation);
            var selected = new HashSet<int>(selectedConstraints);
            foreach (int index in selected)
                if (index < 0 || index >= data.Constraints.Count) throw new ArgumentOutOfRangeException(nameof(selectedConstraints));
            var clip = data.GetAnimation(animation);
            var animated = clip.TryGetValue("transform", out var value) ? Object(value) : null;
            var result = new List<Constraint>();
            for (int i = 0; i < data.Constraints.Count; i++)
            {
                var definition = data.Constraints[i]; var row = definition.Fields;
                if (definition.Kind != "transform" || Boolean(row, "skin") && !selected.Contains(i)) continue;
                var state = new Constraint { Name = definition.Name, Order = (int)Number(row, "order"),
                    Target = Bone(data, (string)row["target"]), Bones = ((List<object>)row["bones"]).Select(n => Bone(data, (string)n)).ToArray(),
                    Local = Boolean(row, "local"), Relative = Boolean(row, "relative"),
                    OffsetPosition = new Vector2(Number(row, "x"), Number(row, "y")),
                    OffsetScale = new Vector2(Number(row, "scaleX"), Number(row, "scaleY")),
                    OffsetRotation = Number(row, "rotation"), OffsetShear = Number(row, "shearY"),
                    Rotation = Number(row, "mixRotate"), X = Number(row, "mixX"), Y = Number(row, "mixY"),
                    ScaleX = Number(row, "mixScaleX"), ScaleY = Number(row, "mixScaleY"), Shear = Number(row, "mixShearY") };
                if (animated != null && animated.TryGetValue(definition.Name, out var frames))
                {
                    var keys = (List<object>)frames;
                    if (keys.Count != 1) throw new NotSupportedException("Verify nonconstant original transform timeline: " + definition.Name);
                    var key = Object(keys[0]);
                    if (time >= Number(key, "time"))
                    {
                        // Binary animation stores six floats. The independent
                        // format projection omits ones for its first five axes,
                        // but omits zero for shear. Setup fields instead omit zero.
                        state.Rotation = Number(key, "mixRotate", 1); state.X = Number(key, "mixX", 1); state.Y = Number(key, "mixY", 1);
                        state.ScaleX = Number(key, "mixScaleX", 1); state.ScaleY = Number(key, "mixScaleY", 1); state.Shear = Number(key, "mixShearY");
                    }
                }
                result.Add(state);
            }
            return result.OrderBy(c => c.Order).ToArray();
        }

        public static void Apply(ProfessionNativeBonePose pose, IReadOnlyList<Constraint> constraints)
        {
            if (pose == null || constraints == null) throw new ArgumentNullException();
            int previous = -1;
            foreach (var state in constraints)
            {
                if (state == null || state.Order <= previous || state.Bones == null || state.Bones.Length == 0)
                    throw new ArgumentException("Original transform constraints require their explicit source order and bones.");
                previous = state.Order;
                foreach (float mix in new[] { state.Rotation, state.X, state.Y, state.ScaleX, state.ScaleY, state.Shear })
                    if (float.IsNaN(mix) || mix < 0 || mix > 1) throw new ArgumentException("Original transform mix is invalid.");
                Finite(state.OffsetRotation); Finite(state.OffsetShear);
                Finite(state.OffsetPosition.x); Finite(state.OffsetPosition.y); Finite(state.OffsetScale.x); Finite(state.OffsetScale.y);
                if (state.Rotation == 0 && state.X == 0 && state.Y == 0 && state.ScaleX == 0 && state.ScaleY == 0 && state.Shear == 0) continue;
                if (!pose.IsActive(state.Target)) continue;
                if (state.Local || state.Relative) throw new NotSupportedException("Verify original local/relative transform constraint: " + state.Name);
                var target = pose.World(state.Target);
                // Source idle offsets have no scale or shear; a nonzero rotation
                // offset on a reflected target needs its own verified contract.
                if (state.OffsetScale != Vector2.zero || state.OffsetShear != 0 ||
                    state.OffsetRotation != 0 && target.m00 * target.m11 - target.m01 * target.m10 < 0)
                    throw new NotSupportedException("Verify original reflected/scale/shear transform offset: " + state.Name);
                foreach (int bone in state.Bones)
                {
                    if (!pose.IsActive(bone)) continue;
                    if (bone == state.Target) throw new ArgumentException("Original transform target equals constrained bone.");
                    var matrix = pose.World(bone);
                    var x = new Vector2(matrix.m00, matrix.m10); var y = new Vector2(matrix.m01, matrix.m11);
                    var tx = new Vector2(target.m00, target.m10); var ty = new Vector2(target.m01, target.m11);
                    if ((state.Rotation != 0 || state.Shear != 0) && (x.sqrMagnitude == 0 || tx.sqrMagnitude == 0 ||
                        state.Shear != 0 && (y.sqrMagnitude == 0 || ty.sqrMagnitude == 0)))
                        throw new NotSupportedException("Verify singular original transform directions: " + state.Name);
                    if (state.Rotation != 0)
                    {
                        float degrees = Mathf.DeltaAngle(Angle(x), Angle(tx) + state.OffsetRotation) * state.Rotation;
                        var rotation = Quaternion.Euler(0, 0, degrees); x = rotation * x; y = rotation * y;
                    }
                    var position = target.MultiplyPoint3x4(state.OffsetPosition);
                    matrix.m03 = Mathf.LerpUnclamped(matrix.m03, position.x, state.X);
                    matrix.m13 = Mathf.LerpUnclamped(matrix.m13, position.y, state.Y);
                    x = Resize(x, tx.magnitude, state.ScaleX, state.Name); y = Resize(y, ty.magnitude, state.ScaleY, state.Name);
                    if (state.Shear != 0)
                    {
                        float current = Mathf.DeltaAngle(Angle(x), Angle(y)), desired = Mathf.DeltaAngle(Angle(tx), Angle(ty));
                        float degrees = Mathf.DeltaAngle(current, desired) * state.Shear;
                        y = Quaternion.Euler(0, 0, degrees) * y;
                    }
                    matrix.m00 = x.x; matrix.m10 = x.y; matrix.m01 = y.x; matrix.m11 = y.y;
                    pose.OverrideWorld(bone, matrix);
                }
            }
        }

        private static Vector2 Resize(Vector2 axis, float target, float mix, string name)
        {
            if (mix == 0) return axis;
            float length = axis.magnitude;
            if (length == 0) throw new NotSupportedException("Verify zero-length original transform scale: " + name);
            return axis * (Mathf.LerpUnclamped(length, target, mix) / length);
        }
        private static float Angle(Vector2 axis) => Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
        private static int Bone(ProfessionNativeSkeletonData data, string name)
        { int index = data.FindBone(name); return index >= 0 ? index : throw new ArgumentException("Absent original transform bone: " + name); }
        private static IReadOnlyDictionary<string, object> Object(object value) => (Dictionary<string, object>)value;
        private static float Number(IReadOnlyDictionary<string, object> row, string name, float fallback = 0)
            => row.TryGetValue(name, out var value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        private static bool Boolean(IReadOnlyDictionary<string, object> row, string name) => row.TryGetValue(name, out var value) && (bool)value;
        private static void Finite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Original transform state must be finite."); }
    }
}
