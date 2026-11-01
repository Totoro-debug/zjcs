using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Independent planar construction: recover the current affine map in the
    // parent's frame, aim its X axis, then change its length when requested.
    // This uses original rig fields; it contains no Spine SDK implementation.
    public static class PartnerNativePortraitIk
    {
        public static void ApplySingle(ProfessionNativeBonePose pose,
            ProfessionNativeConstraintAnimation.IkState state, float originalLength)
        {
            if (pose == null || state == null) throw new ArgumentNullException();
            if (state.Bones == null || state.Bones.Length != 1 || !float.IsFinite(state.Mix) ||
                state.Mix < 0 || state.Mix > 1 || !float.IsFinite(originalLength) || originalLength < 0)
                throw new ArgumentException("Original single-bone IK state is invalid.");
            if (state.Mix == 0) return;
            int bone = state.Bones[0];
            if (!pose.IsActive(bone) || !pose.IsActive(state.Target))
                throw new ArgumentException("Original single-bone IK depends on an inactive bone.");
            if (pose.Inheritance(bone) != 0)
                throw new NotSupportedException("Bind original single-bone IK selective inheritance.");
            int parentIndex = pose.Parent(bone);
            Matrix4x4 parent = parentIndex < 0 ? Matrix4x4.identity : pose.World(parentIndex);
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
    }
}
