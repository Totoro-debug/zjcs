using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.World
{
    // Original world props use closed two-cubic paths and one translated bone.
    // Retain the complete keyed percentage and mix channels. Other modes fail
    // explicitly instead of silently omitting a constraint.
    internal sealed class MainlineGroundPathAnimation
    {
        private sealed class Binding
        {
            public int Bone, Slot;
            public float Position, X, Y;
            public ProfessionNativeCurve PositionCurve, XCurve, YCurve;
        }
        private readonly ProfessionNativeAppearanceSkeleton skeleton;
        private readonly Binding[] bindings;
        private static readonly double[] Nodes = { -.906179845938664, -.538469310105683, 0, .538469310105683, .906179845938664 };
        private static readonly double[] Weights = { .236926885056189, .478628670499366, .568888888888889, .478628670499366, .236926885056189 };

        public MainlineGroundPathAnimation(ProfessionNativeAppearanceSkeleton skeleton, ProfessionNativeAnimationChannels animation)
        {
            this.skeleton = skeleton;
            var result = new List<Binding>();
            foreach (var constraint in skeleton.Source.Constraints.OrderBy(c => Number(c.Fields, "order")))
            {
                var row = constraint.Fields;
                if (constraint.Kind != "path" || (row.TryGetValue("skin", out var skin) && (bool)skin) ||
                    (string)row["positionMode"] != "percent" || Number(row, "mixRotate", 1) != 0 ||
                    ((List<object>)row["bones"]).Count != 1)
                    throw new NotSupportedException("Restore original world constraint: " + constraint.Name);
                var binding = new Binding { Bone = skeleton.Source.FindBone((string)((List<object>)row["bones"])[0]),
                    Slot = skeleton.Source.FindSlot((string)row["target"]), Position = Number(row, "position"),
                    X = Number(row, "mixX", 1), Y = Number(row, "mixY", Number(row, "mixX", 1)) };
                if (animation != null && animation.OriginalFields.TryGetValue("path", out var paths) &&
                    Map(paths).TryGetValue(constraint.Name, out var timelines))
                {
                    foreach (var timeline in Map(timelines))
                    {
                        if (timeline.Key == "position") binding.PositionCurve = Curve((List<object>)timeline.Value, "value", 0);
                        else if (timeline.Key == "mix")
                        {
                            var keys = (List<object>)timeline.Value;
                            if (keys.Any(key => Number(Map(key), "mixRotate", 1) != 0))
                                throw new NotSupportedException("Restore original rotating path timeline.");
                            binding.XCurve = Curve(keys, "mixX", 1);
                            binding.YCurve = Curve(keys, "mixY", 1, "mixX");
                        }
                        else throw new NotSupportedException("Restore original world path timeline: " + timeline.Key);
                    }
                }
                result.Add(binding);
            }
            bindings = result.ToArray();
        }

        public void Apply(float time)
        {
            foreach (var binding in bindings)
            {
                var path = skeleton.GetAttachment(binding.Slot) as ProfessionNativePathAttachment;
                if (path == null) continue;
                if (!path.Closed || !path.ConstantSpeed || path.VertexCount != 6)
                    throw new NotSupportedException("Restore original world path geometry: " + path.Name);
                var points = new Vector2[6]; path.ComputeVertices(skeleton.Pose, points);
                for (int i = 0; i < points.Length; i++) points[i] /= skeleton.Source.DataScale;
                // Closed path points begin with the last incoming handle.
                var a = points[1]; var b = points[2]; var c = points[3]; var d = points[4];
                var e = points[5]; var f = points[0];
                double first = Length(a, b, c, d, 1), second = Length(d, e, f, a, 1);
                double total = first + second;
                if (!(total > 0)) throw new InvalidDataException("Original world path has zero length.");
                double distance = Sample(binding.PositionCurve, time, binding.Position) * total;
                distance = (distance % total + total) % total;
                if (distance >= first) { distance -= first; (a, b, c, d) = (d, e, f, a); }
                double lo = 0, hi = 1;
                for (int iteration = 0; iteration < 28; iteration++)
                { double mid = (lo + hi) * .5; if (Length(a, b, c, d, mid) < distance) lo = mid; else hi = mid; }
                float t = (float)((lo + hi) * .5), u = 1 - t;
                Vector2 value = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
                var matrix = skeleton.Pose.World(binding.Bone);
                matrix.m03 = Mathf.LerpUnclamped(matrix.m03, value.x, Sample(binding.XCurve, time, binding.X));
                matrix.m13 = Mathf.LerpUnclamped(matrix.m13, value.y, Sample(binding.YCurve, time, binding.Y));
                skeleton.Pose.OverrideWorld(binding.Bone, matrix);
            }
        }

        private static double Length(Vector2 a, Vector2 b, Vector2 c, Vector2 d, double end)
        {
            double result = 0;
            for (int interval = 0; interval < 8; interval++)
            {
                double left = end * interval / 8, right = end * (interval + 1) / 8;
                double half = (right - left) * .5, middle = (right + left) * .5;
                for (int sample = 0; sample < Nodes.Length; sample++)
                {
                    double t = middle + half * Nodes[sample], u = 1 - t;
                    double x = 3 * (u * u * (b.x - a.x) + 2 * u * t * (c.x - b.x) + t * t * (d.x - c.x));
                    double y = 3 * (u * u * (b.y - a.y) + 2 * u * t * (c.y - b.y) + t * t * (d.y - c.y));
                    result += half * Weights[sample] * Math.Sqrt(x * x + y * y);
                }
            }
            return result;
        }
        private static ProfessionNativeCurve Curve(List<object> rows, string field, float fallback, string inherited = null)
            => new ProfessionNativeCurve(rows.Select(value =>
            {
                var row = Map(value); var key = new ProfessionNativeCurve.Key { time = Number(row, "time"),
                    value = Number(row, field, inherited == null ? fallback : Number(row, inherited, fallback)) };
                if (row.TryGetValue("curve", out var curve))
                {
                    if (curve is string text) { if (text != "stepped") throw new InvalidDataException("Original path interpolation differs."); key.mode = 1; }
                    else
                    {
                        var handles = (List<object>)curve; int axis = field == "mixX" ? 1 : field == "mixY" ? 2 : 0;
                        if (handles.Count != (field == "value" ? 4 : 12)) throw new InvalidDataException("Original path Bezier controls differ.");
                        key.mode = 2;
                        for (int i = 0; i < 4; i++) key.controls[i] = Convert.ToSingle(handles[axis * 4 + i], CultureInfo.InvariantCulture);
                    }
                }
                return key;
            }).ToArray());
        private static float Sample(ProfessionNativeCurve curve, float time, float fallback)
            => curve != null && curve.TryEvaluate(time, out float value) ? value : fallback;
        private static IReadOnlyDictionary<string, object> Map(object value) => (IReadOnlyDictionary<string, object>)value;
        private static float Number(IReadOnlyDictionary<string, object> row, string key, float fallback = 0)
            => row.TryGetValue(key, out var value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
    }
}
