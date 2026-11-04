using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public class SkeletonBoneRotOverride : SkeletonBoneOverride
    {
        public OverrideRotMode RotMode;
        [Tooltip("使用计算Z变化量，不勾选则是直接使用Z值")]
        public bool UseDelta;
        private float mLastAngle;
        public override OverrideUpdateMode UpdateMode => OverrideUpdateMode.Local;
        protected override void OnUpdateBone(IProfessionLocalOverrideBone bone)
        {
            float num = transform.localEulerAngles.z;
            if (num > 180f) num -= 360f;
            switch (RotMode)
            {
                case OverrideRotMode.Offset:
                    if (UseDelta)
                    {
                        float num2 = num - mLastAngle;
                        bone.Rotation += num2;
                        bone.AppliedRotation += num2;
                    }
                    else
                    {
                        bone.Rotation += num;
                        bone.AppliedRotation += num;
                    }
                    break;
                case OverrideRotMode.Override:
                    bone.Rotation = num;
                    bone.AppliedRotation = num;
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
            mLastAngle = num;
        }
    }
}
