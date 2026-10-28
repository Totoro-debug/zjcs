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
                    // Vertical or extrapolating time handles require a different
                    // native representation. Reject them rather than alter them.
                    if (!(c.x > a.time && c.x <= b.time && c.z >= a.time && c.z < b.time && c.x <= c.z))
                        throw new NotSupportedException("The source Bezier time handles cannot be represented by this native curve binding.");
                    left.outWeight = (c.x - a.time) / duration;
                    right.inWeight = (b.time - c.z) / duration;
                    left.outTangent = (c.y - a.value) / (c.x - a.time);
                    right.inTangent = (b.value - c.w) / (b.time - c.z);
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
            // Unity's weighted curve inversion can lose visible source precision
            // on the weapon's translated handles. Invert the original monotone
            // cubic in double precision; do not sample or bake a replacement.
            Key b = original[lo + 1]; double lower = 0, upper = 1;
            for (int i = 0; i < 48; i++)
            {
                double middle = (lower + upper) * .5;
                if (Cubic(a.time, a.controls.x, a.controls.z, b.time, middle) < time) lower = middle;
                else upper = middle;
            }
            value = (float)Cubic(a.value, a.controls.y, a.controls.w, b.value, (lower + upper) * .5);
            return true;
        }

        private static double Cubic(double a, double b, double c, double d, double t)
        { double u = 1 - t; return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d; }

        private static void Finite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Curve inputs must be finite."); }
    }
}
