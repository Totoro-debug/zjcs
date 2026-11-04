using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent geometry for the two verified direct-child 3100 followers.
    // This consumes the completed pose; it never advances animation or physics.
    [DefaultExecutionOrder(100)]
    public sealed class ProfessionNativePlayerBoneFollower : MonoBehaviour
    {
        public ProfessionNativeSceneRenderer Renderer { get; private set; }
        public string BoneName { get; private set; }

        public void Bind(ProfessionNativeSceneRenderer renderer, Dictionary<string, object> fields)
        {
            if (!renderer || transform.parent != renderer.transform)
                throw new NotSupportedException("The original 3100 bone follower must be a direct child of its renderer.");
            foreach (string flag in new[] { "followXYPosition", "followZPosition", "followBoneRotation",
                "followSkeletonFlip", "followLocalScale", "followParentWorldScale", "initializeOnAwake" })
                if (!Convert.ToBoolean(fields[flag])) throw new NotSupportedException("Verify another original bone follower configuration: " + flag);
            if (Convert.ToInt32(fields["maintainedAxisOrientation"]) != 1)
                throw new NotSupportedException("Verify another original bone follower axis orientation.");
            Renderer = renderer; BoneName = (string)fields["boneName"];
        }

        private void LateUpdate() => ApplyCurrentPose();

        public void ApplyCurrentPose()
        {
            if (!Renderer || Renderer.State == null) return;
            if (transform.parent != Renderer.transform) throw new InvalidOperationException("Original player bone follower parent changed.");
            var state = Renderer.State;
            int index = state.Source.FindBone(BoneName);
            if (index < 0) throw new InvalidOperationException("Original player attachment bone is absent: " + BoneName);
            int parent = state.Pose.Parent(index);
            ApplyPose(transform, state.FindBone(BoneName), parent < 0 ? Matrix4x4.identity : state.Pose.World(parent));
        }

        // Native skeleton scale is one; character reflection belongs to its
        // original renderer Transform and is inherited by this direct child.
        public static void ApplyPose(Transform target, IProfessionFollowerBone bone, Matrix4x4 parentWorld)
        {
            target.localPosition = new Vector3(bone.WorldX, bone.WorldY, 0);
            float angle = Mathf.Atan2(bone.C, bone.A) * Mathf.Rad2Deg;
            if (bone.ScaleX < 0) angle += 180;
            target.localRotation = Quaternion.Euler(0, 0, angle);
            target.localScale = new Vector3(bone.ScaleX * new Vector2(parentWorld.m00, parentWorld.m10).magnitude,
                bone.ScaleY * new Vector2(parentWorld.m01, parentWorld.m11).magnitude, 1);
        }
    }
}
