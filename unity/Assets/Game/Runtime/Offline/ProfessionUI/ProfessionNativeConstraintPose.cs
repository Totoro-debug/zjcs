using System;
using System.Linq;

namespace Zjcs.Offline.ProfessionUI
{
    // Explicit unmixed and MixNext pre-physics stages. Reuse the independently
    // verified geometric solvers and reject their unbound branches.
    public static class ProfessionNativeConstraintPose
    {
        public static void EvaluateSetupBeforePhysics(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeConstraintAnimation setup)
        {
            if (skeleton == null || setup == null) throw new ArgumentNullException();
            if (!ReferenceEquals(setup.Source, skeleton.Source) || setup.Name != null)
                throw new ArgumentException("Bind explicit original setup constraints for this skeleton.", nameof(setup));
            var ik = setup.SampleIk(0, skeleton.SkinConstraints);
            var transforms = setup.SampleTransforms(0, skeleton.SkinConstraints);
            ValidateConstraints(ik, transforms);
            skeleton.SetToSetupPose();
            Solve(skeleton, ik, transforms);
        }

        public static void EvaluateBeforePhysics(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels channels, float time, bool loop = true)
        {
            Validate(skeleton, channels, time);
            float position = loop && channels.Duration > 0 ? time % channels.Duration : time;
            var ik = channels.Constraints.SampleIk(position, skeleton.SkinConstraints);
            var transforms = channels.Constraints.SampleTransforms(position, skeleton.SkinConstraints);
            ValidateConstraints(ik, transforms);
            skeleton.EvaluateNativeAnimation(channels, time, loop);
            Solve(skeleton, ik, transforms);
        }

        public static void EvaluateMixNextBeforePhysics(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels outgoing, float outgoingTime, bool outgoingLoop,
            ProfessionNativeAnimationChannels incoming, float incomingTime, bool incomingLoop, float weight)
        {
            Validate(skeleton, outgoing, outgoingTime); Validate(skeleton, incoming, incomingTime);
            if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0 || weight > 1)
                throw new ArgumentOutOfRangeException(nameof(weight));
            float from = outgoingLoop && outgoing.Duration > 0 ? outgoingTime % outgoing.Duration : outgoingTime;
            float to = incomingLoop && incoming.Duration > 0 ? incomingTime % incoming.Duration : incomingTime;
            var ik = outgoing.Constraints.SampleIk(from, skeleton.SkinConstraints);
            var transforms = outgoing.Constraints.SampleTransforms(from, skeleton.SkinConstraints);
            incoming.Constraints.ApplyReplace(ik, transforms, to, weight);
            ValidateConstraints(ik, transforms);
            skeleton.EvaluateNativeAnimation(outgoing, outgoingTime, outgoingLoop);
            skeleton.ApplyMixNext(incoming, to, weight);
            Solve(skeleton, ik, transforms);
        }

        private static void Validate(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels channels, float time)
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
        }

        private static void ValidateConstraints(ProfessionNativeConstraintAnimation.IkState[] ik,
            ProfessionNativeIdleTransform.Constraint[] transforms)
        {
            // All verified player IK precedes transform constraints. SetRotation
            // invalidates pose caches, so interleaved source orders need a new solver contract.
            if (ik.Length > 0 && transforms.Any(value => value.Order <= ik[ik.Length - 1].Order))
                throw new NotSupportedException("Restore interleaved original world constraint ordering.");
            if (ik.Any(value => value.Softness != 0 || value.Stretch || value.Compress || value.Uniform))
                throw new NotSupportedException("Restore additional original IK softness/stretch/compress branches.");
        }

        private static void Solve(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeConstraintAnimation.IkState[] ik, ProfessionNativeIdleTransform.Constraint[] transforms)
        {
            ProfessionNativeIdleIk.Apply(skeleton.Pose, ik.Select(value => new ProfessionNativeIdleIk.Constraint {
                name = value.Name, order = value.Order, target = value.Target, bones = value.Bones,
                childLength = value.ChildLength, mix = value.Mix, bendPositive = value.BendPositive }).ToArray());
            // Battle rig 11007 retains mixY=-0.4; primaryWeapon's skin-bound
            // transform retains mixRotate=-1. Keep those original finite
            // extrapolating weights in the existing geometric solver.
            if (skeleton.Source.Sex.StartsWith("partner-battle:", StringComparison.Ordinal)
                || skeleton.Source.Sex == "primary-weapon")
                ProfessionNativeIdleTransform.ApplyUnclamped(skeleton.Pose, transforms);
            else ProfessionNativeIdleTransform.Apply(skeleton.Pose, transforms);
            skeleton.Pose.UpdateWorld();
        }
    }
}
