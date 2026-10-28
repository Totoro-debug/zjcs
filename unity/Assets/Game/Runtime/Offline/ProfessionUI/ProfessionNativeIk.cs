using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independently derived planar geometry for the unit-scale, unsheared
    // chains used by the six recovered idle animations. No SDK implementation.
    public static class ProfessionNativeIk
    {
        public readonly struct Pair
        {
            public readonly float ParentRotation, ChildRotation;
            public Pair(float parent, float child) { ParentRotation = parent; ChildRotation = child; }
        }

        public static float Aim(Vector2 directionInParent, float originalRotation, float mix)
        {
            Validate(directionInParent, originalRotation, mix);
            if (mix == 0) return originalRotation;
            Nonzero(directionInParent);
            double angle = Math.Atan2(directionInParent.y, directionInParent.x) * (180 / Math.PI);
            return originalRotation + Mathf.DeltaAngle(originalRotation, (float)angle) * mix;
        }

        public static Pair SolvePair(Vector2 targetInParent, Vector2 childOffset, float childLength,
            float parentRotation, float childRotation, bool bendPositive, float mix)
        {
            Validate(targetInParent, parentRotation, mix); Finite(childRotation); Finite(childOffset.x); Finite(childOffset.y); Finite(childLength);
            if (mix == 0) return new Pair(parentRotation, childRotation);
            Nonzero(targetInParent); Nonzero(childOffset);
            if (childLength <= 0) throw new ArgumentOutOfRangeException(nameof(childLength));
            double x = targetInParent.x, y = targetInParent.y;
            double a = Math.Sqrt((double)childOffset.x * childOffset.x + (double)childOffset.y * childOffset.y), b = childLength;
            double cosine = (x * x + y * y - a * a - b * b) / (2 * a * b);
            cosine = Math.Max(-1, Math.Min(1, cosine));
            double elbow = Math.Acos(cosine) * (bendPositive ? 1 : -1);
            double offset = Math.Atan2(childOffset.y, childOffset.x);
            double shoulder = Math.Atan2(y, x) - Math.Atan2(b * Math.Sin(elbow), a + b * Math.Cos(elbow)) - offset;
            float desiredParent = (float)(shoulder * (180 / Math.PI));
            float desiredChild = (float)((elbow + offset) * (180 / Math.PI));
            return new Pair(parentRotation + Mathf.DeltaAngle(parentRotation, desiredParent) * mix,
                childRotation + Mathf.DeltaAngle(childRotation, desiredChild) * mix);
        }

        private static void Validate(Vector2 direction, float rotation, float mix)
        {
            Finite(direction.x); Finite(direction.y); Finite(rotation); Finite(mix);
            if (mix < 0 || mix > 1) throw new ArgumentOutOfRangeException(nameof(mix));
        }
        private static void Nonzero(Vector2 value)
        {
            if (value.x == 0 && value.y == 0)
                throw new NotSupportedException("Coincident IK origins require a separately verified source branch.");
        }
        private static void Finite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("IK geometry must be finite."); }
    }
}
