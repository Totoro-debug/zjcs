using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent binding of authored path timelines. Geometry below uses only
    // cubic control points, arc length and planar affine transformations.
    public sealed class ProfessionNativePathAnimation
    {
        public sealed class State
        {
            public string Name, PositionMode, SpacingMode, RotateMode;
            public int Order, Slot;
            public int[] Bones;
            public float Position, Spacing, Rotation, MixRotate, MixX, MixY;
        }
        private readonly ProfessionNativeSkeletonData data;
        private readonly Dictionary<string, Dictionary<string, ProfessionNativeCurve>> tracks =
            new Dictionary<string, Dictionary<string, ProfessionNativeCurve>>(StringComparer.Ordinal);
        private static readonly string[] Axes = { "position", "spacing", "mixRotate", "mixX", "mixY" };

        public ProfessionNativePathAnimation(ProfessionNativeSkeletonData data, string animation, bool originalBezierSegments = false)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (!data.GetAnimation(animation).TryGetValue("path", out object paths)) return;
            foreach (var definition in Map(paths))
            {
                var channels = new Dictionary<string, ProfessionNativeCurve>(StringComparer.Ordinal);
                foreach (var timeline in Map(definition.Value))
                {
                    string[] axes = timeline.Key == "mix" ? new[] { "mixRotate", "mixX", "mixY" }
                        : timeline.Key == "position" || timeline.Key == "spacing" ? new[] { timeline.Key }
                        : throw new NotSupportedException("Restore additional original path timeline: " + timeline.Key);
                    var rows = ((List<object>)timeline.Value).Select(Map).ToArray();
                    for (int axis = 0; axis < axes.Length; axis++)
                    {
                        var keys = new ProfessionNativeCurve.Key[rows.Length];
                        for (int i = 0; i < rows.Length; i++)
                        {
                            var row = rows[i];
                            var key = new ProfessionNativeCurve.Key { time = Number(row, "time"),
                                value = Number(row, axes.Length == 1 ? "value" : axes[axis], axes.Length == 1 ? 0 : 1) };
                            if (row.TryGetValue("curve", out object controls))
                            {
                                if (controls is string text)
                                { if (text != "stepped") throw new InvalidDataException("Unknown original path interpolation."); key.mode = 1; }
                                else
                                {
                                    var handles = (List<object>)controls;
                                    if (handles.Count != axes.Length * 4) throw new InvalidDataException("Original path handle count differs.");
                                    key.mode = 2;
                                    for (int j = 0; j < 4; j++) key.controls[j] = Convert.ToSingle(handles[axis * 4 + j], CultureInfo.InvariantCulture);
                                }
                            }
                            keys[i] = key;
                        }
                        var curve = new ProfessionNativeCurve(keys);
                        if (originalBezierSegments) curve.UseOriginalBezierSegments();
                        channels.Add(axes[axis], curve);
                    }
                }
                tracks.Add(definition.Key, channels);
            }
        }

        public State[] Sample(float time, IReadOnlyList<int> selected)
        {
            if (!float.IsFinite(time) || time < 0 || selected == null) throw new ArgumentException("Original path sampling arguments are invalid.");
            var result = new List<State>();
            for (int i = 0; i < data.Constraints.Count; i++)
            {
                var definition = data.Constraints[i]; var row = definition.Fields;
                if (definition.Kind != "path" || row.TryGetValue("skin", out object skin) && (bool)skin && !selected.Contains(i)) continue;
                var state = new State { Name = definition.Name, Order = (int)Number(row, "order"),
                    Slot = data.FindSlot(Text(row, "target")), Bones = ((List<object>)row["bones"]).Select(value => data.FindBone((string)value)).ToArray(),
                    PositionMode = Text(row, "positionMode", "fixed"), SpacingMode = Text(row, "spacingMode", "length"),
                    RotateMode = Text(row, "rotateMode", "tangent"), Rotation = Number(row, "rotation"),
                    Position = Number(row, "position"), Spacing = Number(row, "spacing"),
                    MixRotate = Number(row, "mixRotate", 1), MixX = Number(row, "mixX", 1), MixY = Number(row, "mixY", 1) };
                ApplyReplace(state, time, 1);
                result.Add(state);
            }
            return result.ToArray();
        }

        public void ApplyReplace(State[] states, float time, float alpha)
        {
            if (!float.IsFinite(time) || time < 0 || !float.IsFinite(alpha) || alpha < 0 || alpha > 1)
                throw new ArgumentOutOfRangeException();
            if (alpha != 0) foreach (var state in states) ApplyReplace(state, time, alpha);
        }
        private void ApplyReplace(State state, float time, float alpha)
        {
            if (!tracks.TryGetValue(state.Name, out var channels)) return;
            float[] values = { state.Position, state.Spacing, state.MixRotate, state.MixX, state.MixY };
            for (int i = 0; i < Axes.Length; i++)
                if (channels.TryGetValue(Axes[i], out var curve) && curve.TryEvaluate(time, out float target))
                    values[i] = ProfessionNativeBonePose.MixValue(values[i], target, alpha);
            state.Position = values[0]; state.Spacing = values[1]; state.MixRotate = values[2]; state.MixX = values[3]; state.MixY = values[4];
        }

        public static void Apply(ProfessionNativeAppearanceSkeleton skeleton, State state)
        {
            if (state.MixRotate == 0 && state.MixX == 0 && state.MixY == 0) return;
            if (state.PositionMode != "percent" || state.SpacingMode != "length"
                || state.RotateMode != "tangent" && state.RotateMode != "chain")
                throw new NotSupportedException("Restore additional original partner path modes: " + state.Name);
            var attachment = skeleton.GetAttachment(state.Slot) as ProfessionNativePathAttachment;
            if (attachment == null || !skeleton.Pose.IsActive(skeleton.SlotPose.Bone(state.Slot))) return;
            if (attachment.Closed || !attachment.ConstantSpeed)
                throw new NotSupportedException("Restore additional original partner closed/nonconstant path: " + state.Name);
            var points = new Vector2[attachment.VertexCount];
            var slot = (IProfessionNativeDeformSlot)skeleton.FindSlot((string)((Dictionary<string, object>)
                ((List<object>)skeleton.Source.SourceFields["slots"])[state.Slot])["name"]);
            attachment.ComputeVertices(skeleton.Pose, points, slot.Deform);
            for (int i = 0; i < points.Length; i++) points[i] /= skeleton.Source.DataScale;
            var path = new OpenCurve(points);
            var lengths = new float[state.Bones.Length];
            var distances = new double[state.Bones.Length + 1];
            distances[0] = state.Position * path.Length;
            var boneRows = (List<object>)skeleton.Source.SourceFields["bones"];
            for (int i = 0; i < lengths.Length; i++)
            {
                int bone = state.Bones[i];
                float length = Number((Dictionary<string, object>)boneRows[bone], "length");
                lengths[i] = length;
                float worldScale = skeleton.Pose.World(bone).MultiplyVector(Vector3.right).magnitude;
                distances[i + 1] = distances[i] + (length + state.Spacing) * worldScale;
            }
            Vector2 anchor = path.Sample(distances[0], out _);
            Matrix4x4 slotWorld = skeleton.Pose.World(skeleton.SlotPose.Bone(state.Slot));
            float offset = state.Rotation * (slotWorld.m00 * slotWorld.m11 - slotWorld.m01 * slotWorld.m10 < 0 ? -1 : 1);
            for (int i = 0; i < state.Bones.Length; i++)
            {
                int bone = state.Bones[i];
                Vector2 next = path.Sample(distances[i + 1], out _);
                path.Sample(distances[i], out Vector2 tangent);
                if (!skeleton.Pose.IsActive(bone)) { anchor = next; continue; }
                Matrix4x4 matrix = skeleton.Pose.World(bone);
                matrix.m03 = Mathf.LerpUnclamped(matrix.m03, anchor.x, state.MixX);
                matrix.m13 = Mathf.LerpUnclamped(matrix.m13, anchor.y, state.MixY);
                Vector2 direction = state.RotateMode == "tangent" ? tangent : next - anchor;
                if (direction.sqrMagnitude == 0) direction = tangent;
                float angle = Mathf.Atan2(matrix.m10, matrix.m00) * Mathf.Rad2Deg;
                float rotation = Mathf.DeltaAngle(angle, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + offset) * state.MixRotate;
                float radians = rotation * Mathf.Deg2Rad, c = Mathf.Cos(radians), s = Mathf.Sin(radians);
                float a = matrix.m00, b = matrix.m01, d = matrix.m10, e = matrix.m11;
                matrix.m00 = c * a - s * d; matrix.m10 = s * a + c * d;
                matrix.m01 = c * b - s * e; matrix.m11 = s * b + c * e;
                skeleton.Pose.OverrideWorld(bone, matrix);
                if (state.RotateMode == "chain" && state.Rotation == 0)
                    next += (anchor + new Vector2(matrix.m00, matrix.m10) * lengths[i] - next) * state.MixRotate;
                anchor = next;
            }
        }

        // Open authored paths include one external handle at each end. Keep all
        // interior cubic segments; integrate their speed with Gaussian quadrature
        // and invert accumulated length by bisection. No baked animation is used.
        public sealed class OpenCurve
        {
            private static readonly double[] Nodes = { -.906179845938664, -.538469310105683, 0, .538469310105683, .906179845938664 };
            private static readonly double[] Weights = { .236926885056189, .478628670499366, .568888888888889, .478628670499366, .236926885056189 };
            private readonly Vector2[] points;
            private readonly double[] ends;
            public double Length => ends[ends.Length - 1];
            public OpenCurve(Vector2[] vertices)
            {
                if (vertices == null || vertices.Length < 6 || vertices.Length % 3 != 0)
                    throw new ArgumentException("Original open cubic path control points are invalid.");
                points = (Vector2[])vertices.Clone(); ends = new double[vertices.Length / 3 - 1];
                for (int i = 0; i < ends.Length; i++) ends[i] = (i == 0 ? 0 : ends[i - 1]) + Arc(i, 1);
                if (!double.IsFinite(Length) || Length <= 0) throw new NotSupportedException("Restore original zero-length path.");
            }
            public Vector2 Sample(double distance, out Vector2 tangent)
            {
                if (!double.IsFinite(distance)) throw new ArgumentOutOfRangeException(nameof(distance));
                if (distance < 0)
                { tangent = (points[2] - points[1]).normalized; return points[1] + tangent * (float)distance; }
                if (distance > Length)
                { tangent = (points[points.Length - 2] - points[points.Length - 3]).normalized;
                    return points[points.Length - 2] + tangent * (float)(distance - Length); }
                int segment = 0;
                while (segment < ends.Length - 1 && distance > ends[segment]) segment++;
                distance -= segment == 0 ? 0 : ends[segment - 1];
                double lo = 0, hi = 1;
                for (int i = 0; i < 28; i++) { double mid = (lo + hi) * .5; if (Arc(segment, mid) < distance) lo = mid; else hi = mid; }
                float t = (float)((lo + hi) * .5), u = 1 - t; int start = 1 + segment * 3;
                Vector2 a = points[start], b = points[start + 1], c = points[start + 2], d = points[start + 3];
                tangent = (3 * (u * u * (b - a) + 2 * u * t * (c - b) + t * t * (d - c))).normalized;
                return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
            }
            private double Arc(int segment, double end)
            {
                int start = 1 + segment * 3;
                Vector2 a = points[start], b = points[start + 1], c = points[start + 2], d = points[start + 3];
                double result = 0;
                for (int interval = 0; interval < 8; interval++)
                {
                    double half = end / 16, middle = end * (interval + .5) / 8;
                    for (int sample = 0; sample < Nodes.Length; sample++)
                    {
                        double t = middle + half * Nodes[sample], u = 1 - t;
                        double x = 3 * (u * u * (b.x - a.x) + 2 * u * t * (c.x - b.x) + t * t * (d.x - c.x));
                        double y = 3 * (u * u * (b.y - a.y) + 2 * u * t * (c.y - b.y) + t * t * (d.y - c.y));
                        result += Weights[sample] * Math.Sqrt(x * x + y * y) * half;
                    }
                }
                return result;
            }
        }
        private static Dictionary<string, object> Map(object value) => value as Dictionary<string, object> ?? throw new InvalidDataException("Original path fields differ.");
        private static float Number(IReadOnlyDictionary<string, object> row, string key, float fallback = 0)
            => row.TryGetValue(key, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        private static string Text(IReadOnlyDictionary<string, object> row, string key, string fallback = null)
            => row.TryGetValue(key, out object value) ? (string)value : fallback;
    }
}
