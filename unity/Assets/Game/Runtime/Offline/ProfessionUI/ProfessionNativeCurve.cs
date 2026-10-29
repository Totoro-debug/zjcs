using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent conversion of source time/value Beziers into Unity's weighted
    // native curves. This contains no skeleton-runtime implementation.
    public sealed class ProfessionNativeCurve
    {
        [Serializable]
        public sealed class Key
        {
            public float time, value;
            public int mode; // 0 linear, 1 stepped, 2 cubic Bezier.
            public Vector4 controls; // Absolute (time, value) of both handles.
        }

        private readonly AnimationCurve curve;
        private readonly Key[] original;
        private readonly float firstTime;
        public int KeyCount => curve.length;

        public ProfessionNativeCurve(Key[] source)
        {
            if (source == null || source.Length == 0) throw new ArgumentException("A source curve must have keys.", nameof(source));
            var keys = new Keyframe[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Key key = source[i] ?? throw new ArgumentException("A source key is null.", nameof(source));
                Finite(key.time); Finite(key.value);
                if (key.mode < 0 || key.mode > 2 || (i > 0 && key.time <= source[i - 1].time))
                    throw new ArgumentException("Source curve keys must have increasing times and a known interpolation mode.", nameof(source));
                keys[i] = new Keyframe(key.time, key.value) { weightedMode = WeightedMode.Both, inWeight = 1f / 3f, outWeight = 1f / 3f };
            }
            firstTime = source[0].time;
            original = new Key[source.Length];
            for (int i = 0; i < source.Length; i++)
                original[i] = new Key { time = source[i].time, value = source[i].value, mode = source[i].mode, controls = source[i].controls };
            for (int i = 0; i + 1 < source.Length; i++)
            {
                Key a = source[i], b = source[i + 1];
                Keyframe left = keys[i], right = keys[i + 1];
                float duration = b.time - a.time;
                if (a.mode == 1)
                {
                    left.outTangent = float.PositiveInfinity;
                    right.inTangent = float.PositiveInfinity;
                }
                else if (a.mode == 0)
                {
                    left.outTangent = right.inTangent = (b.value - a.value) / duration;
                }
                else
                {
                    Vector4 c = a.controls;
                    Finite(c.x); Finite(c.y); Finite(c.z); Finite(c.w);
                    // Unity carries the original keys; evaluate cubic source handles
                    // independently when weighted time inversion needs more precision.
                    // Vertical, crossed and extrapolating handles remain unchanged.
                    left.outWeight = Mathf.Clamp01((c.x - a.time) / duration);
                    right.inWeight = Mathf.Clamp01((b.time - c.z) / duration);
                    left.outTangent = c.x == a.time ? 0 : (c.y - a.value) / (c.x - a.time);
                    right.inTangent = c.z == b.time ? 0 : (b.value - c.w) / (b.time - c.z);
                }
                keys[i] = left; keys[i + 1] = right;
            }
            curve = new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
        }

        public bool TryEvaluate(float time, out float value)
        {
            Finite(time);
            // Source timelines do not apply any value before their first key.
            if (time < firstTime) { value = 0; return false; }
            int lo = 0, hi = original.Length;
            while (lo + 1 < hi)
            {
                int middle = lo + (hi - lo) / 2;
                if (original[middle].time <= time) lo = middle; else hi = middle;
            }
            Key a = original[lo];
            if (lo == original.Length - 1 || time == a.time) { value = a.value; return true; }
            if (a.mode != 2) { value = curve.Evaluate(time); return true; }
            Key b = original[lo + 1];
            double parameter = InvertTime(a.time, a.controls.x, a.controls.z, b.time, time);
            value = (float)Cubic(a.value, a.controls.y, a.controls.w, b.value, parameter);
            return true;
        }

        private static double InvertTime(double a, double b, double c, double d, double time)
        {
            // Split at the derivative roots before inversion. Some original
            // handles leave the key interval; a single global binary search
            // would then assume monotonicity which the source does not have.
            double qa = -a + 3 * b - 3 * c + d, qb = 2 * (a - 2 * b + c), qc = b - a;
            double first = 1, second = 1;
            if (Math.Abs(qa) < 1e-14)
            {
                if (Math.Abs(qb) > 1e-14) { double r = -qc / qb; if (r > 0 && r < 1) first = r; }
            }
            else
            {
                double discriminant = qb * qb - 4 * qa * qc;
                if (discriminant >= 0)
                {
                    double root = Math.Sqrt(discriminant);
                    double r1 = (-qb - root) / (2 * qa), r2 = (-qb + root) / (2 * qa);
                    if (r1 > r2) { double swap = r1; r1 = r2; r2 = swap; }
                    if (r1 > 0 && r1 < 1) first = r1;
                    if (r2 > 0 && r2 < 1) { if (first == 1) first = r2; else second = r2; }
                }
            }
            double lower = 0;
            for (int span = 0; span < 3; span++)
            {
                double upper = span == 0 ? first : span == 1 ? second : 1;
                double from = Cubic(a, b, c, d, lower), to = Cubic(a, b, c, d, upper);
                if (time >= Math.Min(from, to) && time <= Math.Max(from, to))
                {
                    bool ascending = to >= from;
                    for (int i = 0; i < 48; i++)
                    {
                        double middle = (lower + upper) * .5;
                        if ((Cubic(a, b, c, d, middle) < time) == ascending) lower = middle; else upper = middle;
                    }
                    return (lower + upper) * .5;
                }
                lower = upper;
            }
            throw new InvalidOperationException("Original cubic time has no root inside the source segment.");
        }

        private static double Cubic(double a, double b, double c, double d, double t)
        { double u = 1 - t; return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d; }

        private static void Finite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Curve inputs must be finite."); }
    }
}
