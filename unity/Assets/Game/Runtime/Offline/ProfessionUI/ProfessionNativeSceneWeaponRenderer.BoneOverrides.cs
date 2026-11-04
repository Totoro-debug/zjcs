using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSceneWeaponRenderer
    {
        private sealed class LocalBone : IProfessionLocalOverrideBone
        {
            internal readonly ProfessionNativeAppearanceSkeleton Skeleton;
            internal readonly int Index;
            internal LocalBone(ProfessionNativeAppearanceSkeleton skeleton, int index) { Skeleton = skeleton; Index = index; }
            public float Rotation { get => Skeleton.Pose.LocalRotation(Index); set => Skeleton.Pose.SetRotation(Index, value); }
            public float AppliedRotation { get; set; }
        }
        private readonly Dictionary<string, LocalBone> overrideBones = new Dictionary<string, LocalBone>(StringComparer.Ordinal);
        public event Action<IProfessionBoneOverrideAnimation> UpdateLocal, UpdateComplete;
        public IProfessionLocalOverrideBone FindOverrideBone(string name)
        {
            if (State == null) return null;
            int index = State.Source.FindBone(name);
            if (index < 0) return null;
            if (!overrideBones.TryGetValue(name, out var bone) || !ReferenceEquals(bone.Skeleton, State))
                overrideBones[name] = bone = new LocalBone(State, index) { AppliedRotation = State.Pose.LocalRotation(index) };
            return bone;
        }
        private void OnUpdateLocal(ProfessionNativeAppearanceSkeleton skeleton)
        {
            // A fresh animation application initializes applied-local values
            // before the original application's paired Rotation/AppliedRotation
            // writes. World and spring solvers consume the modified local pose.
            foreach (var bone in overrideBones.Values)
                if (ReferenceEquals(bone.Skeleton, skeleton)) bone.AppliedRotation = bone.Rotation;
            UpdateLocal?.Invoke(this);
        }
    }
}
