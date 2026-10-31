using System;
using System.Linq;

namespace Zjcs.Offline.ProfessionUI
{
    // An explicit unmixed, pre-physics pipeline stage. Reuse the independently
    // verified geometric solvers and reject their unbound branches.
    public static class ProfessionNativeConstraintPose
    {
        public static void EvaluateBeforePhysics(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels channels, float time, bool loop = true)
        {
            if (skeleton == null || channels == null) throw new ArgumentNullException();
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (!ReferenceEquals(channels.OriginalFields, skeleton.Source.GetAnimation(channels.Name)))
                throw new ArgumentException("Original constraint animation belongs to another skeleton.");
            for (int index = 0; index < skeleton.Source.Constraints.Count; index++)
            {
                var definition = skeleton.Source.Constraints[index];
                if (definition.Kind == "ik" || definition.Kind == "transform" || definition.Kind == "physics") continue;
                if (definition.Fields.TryGetValue("skin", out object required) && (bool)required
                    && !skeleton.SkinConstraints.Contains(index)) continue;
                throw new NotSupportedException("Restore original world constraint kind: " + definition.Kind);
            }
            float position = loop && channels.Duration > 0 ? time % channels.Duration : time;
            var ik = channels.Constraints.SampleIk(position, skeleton.SkinConstraints);
            var transforms = channels.Constraints.SampleTransforms(position, skeleton.SkinConstraints);
            // All verified player IK precedes transform constraints. SetRotation
            // invalidates pose caches, so interleaved source orders need a new solver contract.
            if (ik.Length > 0 && transforms.Any(value => value.Order <= ik[ik.Length - 1].Order))
                throw new NotSupportedException("Restore interleaved original world constraint ordering.");
            if (ik.Any(value => value.Softness != 0 || value.Stretch || value.Compress || value.Uniform))
                throw new NotSupportedException("Restore additional original IK softness/stretch/compress branches.");
            skeleton.EvaluateNativeAnimation(channels, time, loop);
            ProfessionNativeIdleIk.Apply(skeleton.Pose, ik.Select(value => new ProfessionNativeIdleIk.Constraint {
                name = value.Name, order = value.Order, target = value.Target, bones = value.Bones,
                childLength = value.ChildLength, mix = value.Mix, bendPositive = value.BendPositive }).ToArray());
            ProfessionNativeIdleTransform.Apply(skeleton.Pose, transforms);
            skeleton.Pose.UpdateWorld();
        }
    }
}
