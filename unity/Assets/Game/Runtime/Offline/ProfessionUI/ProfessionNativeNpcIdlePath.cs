using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent geometry for the original four single-bone, translation-only
    // constraints on body_8441's open cubic path. No animation SDK is used.
    public static class ProfessionNativeNpcIdlePath
    {
        private static readonly double[] Nodes = { -.906179845938664, -.538469310105683, 0, .538469310105683, .906179845938664 };
        private static readonly double[] Weights = { .236926885056189, .478628670499366, .568888888888889, .478628670499366, .236926885056189 };

        public static void Apply(ProfessionNativeAppearanceSkeleton skeleton)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            var selected = new HashSet<int>(skeleton.SkinConstraints);
            var source = skeleton.Source;
            foreach (int index in Enumerable.Range(0, source.Constraints.Count)
                .Where(i => source.Constraints[i].Kind == "path" && (!Flag(source.Constraints[i].Fields, "skin") || selected.Contains(i)))
                .OrderBy(i => Number(source.Constraints[i].Fields, "order")))
            {
                var row = source.Constraints[index].Fields;
                var names = (List<object>)row["bones"];
                if (names.Count != 1 || Text(row, "positionMode", "fixed") != "percent" || Number(row, "mixRotate", 1) != 0)
                    throw new NotSupportedException("Verify additional original NPC path mode: " + source.Constraints[index].Name);
                int slot = source.FindSlot((string)row["target"]);
                var path = skeleton.GetAttachment(slot) as ProfessionNativePathAttachment;
                if (path == null) continue; // The original slot can have no path attached.
                if (path.Closed || !path.ConstantSpeed || path.VertexCount != 6)
                    throw new NotSupportedException("Verify additional original NPC path geometry: " + path.Name);
                var points = new Vector2[6]; path.ComputeVertices(skeleton.Pose, points);
                // The first/last handles extend an open path beyond its endpoints.
                var a = points[1] / source.DataScale; var b = points[2] / source.DataScale;
                var c = points[3] / source.DataScale; var d = points[4] / source.DataScale;
                double total = Length(a, b, c, d, 1), distance = Number(row, "position") * total;
                Vector2 position;
                if (distance < 0) position = a + (b - a).normalized * (float)distance;
                else if (distance > total) position = d + (d - c).normalized * (float)(distance - total);
                else
                {
                    double lo = 0, hi = 1;
                    for (int iteration = 0; iteration < 28; iteration++)
                    { double middle = (lo + hi) * .5; if (Length(a, b, c, d, middle) < distance) lo = middle; else hi = middle; }
                    float t = (float)((lo + hi) * .5), u = 1 - t;
                    position = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
                }
                int bone = source.FindBone((string)names[0]);
                if (!skeleton.Pose.IsActive(bone)) continue;
                var matrix = skeleton.Pose.World(bone);
                matrix.m03 = Mathf.LerpUnclamped(matrix.m03, position.x, Number(row, "mixX", 1));
                matrix.m13 = Mathf.LerpUnclamped(matrix.m13, position.y, Number(row, "mixY", 1));
                skeleton.Pose.OverrideWorld(bone, matrix);
            }
        }
        private static double Length(Vector2 a, Vector2 b, Vector2 c, Vector2 d, double end)
        {
            // Gaussian integration of |B'(t)| keeps the original cubic points
            // and percentage position, including nonuniform world deformation.
            double result = 0;
            for (int interval = 0; interval < 8; interval++)
            {
                double left = end * interval / 8, right = end * (interval + 1) / 8, half = (right - left) * .5, middle = (right + left) * .5;
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
        private static float Number(IReadOnlyDictionary<string, object> row, string name, float fallback = 0)
            => row.TryGetValue(name, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        private static string Text(IReadOnlyDictionary<string, object> row, string name, string fallback)
            => row.TryGetValue(name, out object value) ? (string)value : fallback;
        private static bool Flag(IReadOnlyDictionary<string, object> row, string name)
            => row.TryGetValue(name, out object value) && (bool)value;
    }
}
