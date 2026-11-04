using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Independent spatial adapter for the original shadow-only BoneFollower
    // configuration. Retain its serialized names; no Spine SDK types are used.
    public sealed class OfficialBattleShadowBoneFollower : MonoBehaviour
    {
        public Component skeletonRenderer;
        public string boneName;
        public bool followXYPosition = true, followZPosition, followBoneRotation, followSkeletonFlip;
        public bool followLocalScale = true, followParentWorldScale;
        public int maintainedAxisOrientation = 1;
        public bool initializeOnAwake = true;

        public void Bind(Component renderer)
        {
            if (!(renderer is IProfessionFollowerRenderer native) || native.Skeleton?.FindBone(boneName) == null)
                throw new ArgumentException("Bind the original shadow's live native bone.", nameof(renderer));
            if (!followXYPosition || followZPosition || followBoneRotation || followSkeletonFlip
                || followParentWorldScale || maintainedAxisOrientation != 1)
                throw new NotSupportedException("Restore the additional original shadow follower configuration.");
            skeletonRenderer = renderer;
            ForceUpdate();
        }

        private void LateUpdate() => ForceUpdate();

        public void ForceUpdate()
        {
            if (skeletonRenderer == null) return;
            var source = (IProfessionFollowerRenderer)skeletonRenderer;
            var bone = source.Skeleton?.FindBone(boneName);
            if (bone == null) throw new InvalidOperationException("The original shadow bone was removed.");
            Vector3 position = skeletonRenderer.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0));
            position.z = transform.position.z;
            transform.position = position;
            if (followLocalScale) transform.localScale = new Vector3(bone.ScaleX, bone.ScaleY, 1);
        }
    }
}
