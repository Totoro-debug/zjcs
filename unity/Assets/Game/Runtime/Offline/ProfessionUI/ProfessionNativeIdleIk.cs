using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Binds only the verified original idle-chain geometry. The source data
    // supplies constraint order and current mix/bend states explicitly.
    public static class ProfessionNativeIdleIk
    {
        [Serializable]
        public sealed class Constraint
        {
            public string name;
            public int order, target;
            public int[] bones;
            public float childLength, mix;
            public bool bendPositive;
        }

        public static void Apply(ProfessionNativeBonePose pose, IReadOnlyList<Constraint> constraints)
        {
            if (pose == null || constraints == null) throw new ArgumentNullException();
            int previousOrder = -1;
            foreach (Constraint constraint in constraints)
            {
                if (constraint == null || constraint.order <= previousOrder || constraint.bones == null ||
                    constraint.bones.Length < 1 || constraint.bones.Length > 2 ||
                    float.IsNaN(constraint.mix) || constraint.mix < 0 || constraint.mix > 1)
                    throw new ArgumentException("Original idle IK state or order is invalid.");
                previousOrder = constraint.order;
                if (constraint.mix == 0) continue;
                foreach (int bone in constraint.bones)
                {
                    Vector2 scale = pose.LocalScale(bone), shear = pose.Shear(bone);
                    if (!pose.IsActive(bone) || scale.x != 1 || scale.y != 1 || shear.x != 0 || shear.y != 0 || pose.Inheritance(bone) != 0)
                        throw new NotSupportedException("This original IK chain requires a different scale, shear or inheritance binding.");
                }
                if (!pose.IsActive(constraint.target)) throw new ArgumentException("Original IK target is inactive.");
                int first = constraint.bones[0];
                Matrix4x4 targetWorld = pose.World(constraint.target);
                int parentIndex = pose.Parent(first);
                Matrix4x4 parent = parentIndex < 0 ? Matrix4x4.identity : pose.World(parentIndex);
                if (Mathf.Abs(parent.m00 * parent.m11 - parent.m01 * parent.m10) < 1e-12f)
                    throw new NotSupportedException("A singular original parent transform requires a separately verified IK branch.");
                Vector2 target = parent.inverse.MultiplyPoint3x4(new Vector3(targetWorld.m03, targetWorld.m13, 0));
                target -= pose.LocalPosition(first);
                if (constraint.bones.Length == 1)
                    pose.SetRotation(first, ProfessionNativeIk.Aim(target, pose.LocalRotation(first), constraint.mix));
                else
                {
                    int child = constraint.bones[1];
                    if (pose.Parent(child) != first) throw new ArgumentException("Original two-bone IK chain is not consecutive.");
                    var result = ProfessionNativeIk.SolvePair(target, pose.LocalPosition(child), constraint.childLength,
                        pose.LocalRotation(first), pose.LocalRotation(child), constraint.bendPositive, constraint.mix);
                    pose.SetRotation(first, result.ParentRotation);
                    pose.SetRotation(child, result.ChildRotation);
                }
            }
        }
    }
}
