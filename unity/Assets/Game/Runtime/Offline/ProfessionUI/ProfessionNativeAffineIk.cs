using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent planar construction: recover the current affine map in the
    // parent's frame, aim its X axis, then change its length when requested.
    // This uses original rig fields; it contains no Spine SDK implementation.
    public static class ProfessionNativeAffineIk
    {
        public static void ApplySingle(ProfessionNativeBonePose pose,
            ProfessionNativeConstraintAnimation.IkState state, float originalLength)
        {
            if (pose == null || state == null) throw new ArgumentNullException();
            if (state.Bones == null || state.Bones.Length != 1 || !float.IsFinite(state.Mix) ||
                !float.IsFinite(originalLength) || originalLength < 0)
                throw new ArgumentException("Original single-bone IK state is invalid.");
            if (state.Mix == 0) return;
            int bone = state.Bones[0];
            if (!pose.IsActive(bone) || !pose.IsActive(state.Target))
                throw new ArgumentException("Original single-bone IK depends on an inactive bone.");
            int inheritance = pose.Inheritance(bone);
            if (inheritance != 0 && inheritance != 3 && inheritance != 4)
                throw new NotSupportedException("Bind original single-bone IK selective inheritance.");
            int parentIndex = pose.Parent(bone);
            // No-scale inheritance already constructs an orthonormal world
            // frame. Aim its preserved affine axes directly in that frame.
            Matrix4x4 parent = parentIndex < 0 || inheritance == 3 || inheritance == 4
                ? Matrix4x4.identity : pose.World(parentIndex);
            if (Mathf.Abs(parent.m00 * parent.m11 - parent.m01 * parent.m10) < 1e-12f)
                throw new NotSupportedException("Bind original single-bone IK singular parent.");
            Matrix4x4 inverse = parent.inverse;
            Matrix4x4 local = inverse * pose.World(bone);
            Matrix4x4 targetWorld = pose.World(state.Target);
            Vector3 target = inverse.MultiplyPoint3x4(new Vector3(targetWorld.m03, targetWorld.m13, 0));
            Vector2 direction = new Vector2(target.x - local.m03, target.y - local.m13);
            var xAxis = new Vector2(local.m00, local.m10);
            var yAxis = new Vector2(local.m01, local.m11);
            float initialAngle = Mathf.Atan2(xAxis.y, xAxis.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(initialAngle, targetAngle) * state.Mix * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(delta), sine = Mathf.Sin(delta);
            Vector2 Rotate(Vector2 axis) => new Vector2(cosine * axis.x - sine * axis.y,
                sine * axis.x + cosine * axis.y);
            float length = xAxis.magnitude * originalLength;
            float desired = direction.magnitude;
            float factor = 1;
            if (length > 0 && (state.Compress && desired < length || state.Stretch && desired > length))
                factor += (desired / length - 1) * state.Mix;
            xAxis = Rotate(xAxis) * factor;
            yAxis = Rotate(yAxis) * (state.Uniform ? factor : 1);
            local.m00 = xAxis.x; local.m10 = xAxis.y;
            local.m01 = yAxis.x; local.m11 = yAxis.y;
            pose.OverrideWorld(bone, parent * local);
        }

        public static void ApplyPair(ProfessionNativeBonePose pose,
            ProfessionNativeConstraintAnimation.IkState state)
        {
            if (pose == null || state == null) throw new ArgumentNullException();
            if (state.Bones == null || state.Bones.Length != 2 || !float.IsFinite(state.Mix) ||
                !float.IsFinite(state.Softness) || state.Softness < 0 ||
                !float.IsFinite(state.ChildLength) || state.ChildLength < 0)
                throw new ArgumentException("Original two-bone IK state is invalid: " + state.Name +
                    "; mix=" + state.Mix.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; length=" + state.ChildLength.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (state.Mix == 0) return;
            if (state.ChildLength == 0)
                throw new NotSupportedException("Bind original active two-bone IK zero-length child.");
            int first = state.Bones[0], child = state.Bones[1];
            if (!pose.IsActive(first) || !pose.IsActive(child) || !pose.IsActive(state.Target) || pose.Parent(child) != first)
                throw new ArgumentException("Original two-bone IK hierarchy is invalid or inactive.");
            if (pose.Inheritance(first) != 0 || pose.Inheritance(child) != 0)
                throw new NotSupportedException("Bind original two-bone IK selective inheritance.");
            int parentIndex = pose.Parent(first);
            Matrix4x4 parent = parentIndex < 0 ? Matrix4x4.identity : pose.World(parentIndex);
            if (Mathf.Abs(Determinant(parent)) < 1e-12f)
                throw new NotSupportedException("Bind original two-bone IK singular parent.");
            Matrix4x4 firstWorld = pose.World(first);
            Matrix4x4 local = parent.inverse * firstWorld;
            var x = new Vector2(local.m00, local.m10);
            var y = new Vector2(local.m01, local.m11);
            float size = x.magnitude;
            float tolerance = .00002f * Mathf.Max(x.sqrMagnitude, y.sqrMagnitude);
            if (size < 1e-7f || Mathf.Abs(Determinant(local)) < 1e-12f || Mathf.Abs(Vector2.Dot(x, y)) > tolerance)
                throw new NotSupportedException("Bind original two-bone IK singular/sheared first bone: "
                    + state.Name + "; axes=" + x.ToString("R") + "/" + y.ToString("R"));
            Matrix4x4 childLocal = firstWorld.inverse * pose.World(child);
            float childScale = new Vector2(childLocal.m00, childLocal.m10).magnitude;
            if (childScale < 1e-7f)
                throw new NotSupportedException("Bind original two-bone IK zero child axis.");
            Matrix4x4 targetWorld = pose.World(state.Target);
            Vector3 target = parent.inverse.MultiplyPoint3x4(new Vector3(targetWorld.m03, targetWorld.m13, 0));
            Vector2 targetDelta = new Vector2(target.x - local.m03, target.y - local.m13);
            if (Mathf.Abs(x.sqrMagnitude - y.sqrMagnitude) > tolerance)
            {
                ApplyEllipticPair(pose, state, parent, local, childLocal, targetDelta, size,
                    y.magnitude * Mathf.Sign(Determinant(local)), childScale);
                return;
            }
            var direction = targetDelta / size;
            float firstAngle = Mathf.Atan2(x.y, x.x) * Mathf.Rad2Deg;
            float childAngle = Mathf.Atan2(childLocal.m10, childLocal.m00) * Mathf.Rad2Deg;
            // The existing independent cosine-law solver works in this circular
            // frame. Retain both current affine maps instead of discarding prior
            // constraint results by resetting their local source transforms.
            Vector2 childOffset = new Vector2(childLocal.m03, childLocal.m13);
            if (state.Stretch) childOffset.y = childLocal.m13 = 0;
            if (direction.sqrMagnitude == 0 || childOffset.sqrMagnitude == 0)
                throw new NotSupportedException("Coincident original two-bone IK origins require a verified branch.");
            float childLength = state.ChildLength * childScale;
            double a = childOffset.magnitude, b = childLength;
            double reach = a + b;
            float distance = direction.magnitude;
            if (state.Stretch && state.Softness == 0 && distance > reach)
            {
                if (reach <= 0) throw new NotSupportedException("Original two-bone IK stretch has no reachable length.");
                float stretch = 1 + ((float)(distance / reach) - 1) * state.Mix;
                if (!(stretch > 0) || !float.IsFinite(stretch))
                    throw new NotSupportedException("Original two-bone IK stretch produced an invalid scale.");
                local.m00 *= stretch; local.m10 *= stretch;
                if (state.Uniform) { local.m01 *= stretch; local.m11 *= stretch; }
                size *= stretch;
                direction = targetDelta / size;
                distance = direction.magnitude;
            }
            float softness = state.Softness / size;
            if (softness > 0 && distance > reach - softness)
            {
                float penetration = Mathf.Min(distance - (float)reach + softness, 2 * softness);
                float softenedDistance = distance - penetration * penetration / (4 * softness);
                if (distance > 0) direction *= softenedDistance / distance;
            }
            double cosine = (direction.sqrMagnitude - a * a - b * b) / (2 * a * b);
            cosine = Math.Max(-1, Math.Min(1, cosine));
            double elbow = Math.Acos(cosine) * (state.BendPositive ? 1 : -1);
            double offset = Math.Atan2(childOffset.y, childOffset.x);
            double shoulder = Math.Atan2(direction.y, direction.x) - Mathf.Sign(Determinant(local)) *
                (Math.Atan2(b * Math.Sin(elbow), a + b * Math.Cos(elbow)) + offset);
            float parentDelta = Mathf.DeltaAngle(firstAngle, (float)(shoulder * (180 / Math.PI))) * state.Mix;
            float childDelta = Mathf.DeltaAngle(childAngle, (float)((elbow + offset) * (180 / Math.PI))) * state.Mix;
            local = RotateAxes(local, parentDelta);
            childLocal = RotateAxes(childLocal, childDelta);
            Matrix4x4 solved = parent * local;
            pose.OverrideWorld(first, solved);
            pose.OverrideWorld(child, solved * childLocal);
        }

        // With unequal orthogonal scales the child tip traces an ellipse about
        // its origin. Intersect its squared radius with the target circle, then
        // rotate that intersection onto the target. This is derived directly
        // from planar geometry, without an animation SDK implementation.
        private static void ApplyEllipticPair(ProfessionNativeBonePose pose,
            ProfessionNativeConstraintAnimation.IkState state, Matrix4x4 parent,
            Matrix4x4 local, Matrix4x4 child, Vector2 target, double sx, double sy, double childScale)
        {
            if (state.Softness != 0)
                throw new NotSupportedException("Bind original nonuniform two-bone IK softness: " + state.Name);
            if (child.m03 <= 0 || Math.Abs(sy) < 1e-7)
                throw new NotSupportedException("Bind original nonuniform two-bone IK negative/coincident child origin: " + state.Name);
            // The public constraint specification sets child Y to zero for
            // nonuniform parent scale and disables stretch in this branch.
            child.m13 = 0;
            double origin = sx * child.m03, length = state.ChildLength * childScale;
            double ex = sx * length, ey = sy * length;
            double quadratic = ex * ex - ey * ey, linear = 2 * origin * ex;
            double constant = origin * origin + ey * ey;
            double RadiusSquared(double c) => (quadratic * c + linear) * c + constant;
            double minimum = Math.Min(RadiusSquared(-1), RadiusSquared(1));
            double maximum = Math.Max(RadiusSquared(-1), RadiusSquared(1));
            double stationary = -linear / (2 * quadratic);
            if (stationary > -1 && stationary < 1)
            {
                double value = RadiusSquared(stationary);
                minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
            }
            double squared = Math.Max(minimum, Math.Min(maximum, (double)target.x * target.x + (double)target.y * target.y));
            double discriminant = Math.Max(0, linear * linear - 4 * quadratic * (constant - squared));
            // Avoid cancellation for nearly equal scales. Both algebraic roots
            // are checked; the smaller bend resolves the possible double hit.
            double q = -.5 * (linear + Math.Sqrt(discriminant));
            double rootA = q / quadratic, rootB = q == 0 ? 0 : (constant - squared) / q;
            double cosine = double.NegativeInfinity;
            foreach (double candidate in new[] { rootA, rootB })
                if (candidate >= -1.0000001 && candidate <= 1.0000001)
                    cosine = Math.Max(cosine, Math.Max(-1, Math.Min(1, candidate)));
            if (double.IsNegativeInfinity(cosine))
                throw new InvalidOperationException("Original nonuniform IK ellipse intersection failed: " + state.Name);
            double elbow = Math.Acos(cosine) * (state.BendPositive ? 1 : -1);
            double shoulder = Math.Atan2(target.y, target.x) - Math.Atan2(ey * Math.Sin(elbow), origin + ex * cosine);
            float firstAngle = Mathf.Atan2(local.m10, local.m00) * Mathf.Rad2Deg;
            float childAngle = Mathf.Atan2(child.m10, child.m00) * Mathf.Rad2Deg;
            local = RotateAxes(local, Mathf.DeltaAngle(firstAngle, (float)(shoulder * (180 / Math.PI))) * state.Mix);
            child = RotateAxes(child, Mathf.DeltaAngle(childAngle, (float)(elbow * (180 / Math.PI))) * state.Mix);
            Matrix4x4 solved = parent * local;
            pose.OverrideWorld(state.Bones[0], solved);
            pose.OverrideWorld(state.Bones[1], solved * child);
        }

        private static float Determinant(Matrix4x4 value) => value.m00 * value.m11 - value.m01 * value.m10;
        private static Matrix4x4 RotateAxes(Matrix4x4 value, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad, cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            float a = value.m00, b = value.m01, c = value.m10, d = value.m11;
            value.m00 = cosine * a - sine * c; value.m10 = sine * a + cosine * c;
            value.m01 = cosine * b - sine * d; value.m11 = sine * b + cosine * d;
            return value;
        }
    }
}
