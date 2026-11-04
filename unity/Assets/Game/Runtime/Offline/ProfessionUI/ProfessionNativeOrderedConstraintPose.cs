using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Zjcs.Offline.ProfessionUI
{
    // Original constraint order, with the independently verified affine IK
    // construction also used by partner illustrations. Physics is a later stage.
    public static class ProfessionNativeOrderedConstraintPose
    {
        private static readonly ConditionalWeakTable<ProfessionNativeAnimationChannels, ProfessionNativePathAnimation> PathChannels =
            new ConditionalWeakTable<ProfessionNativeAnimationChannels, ProfessionNativePathAnimation>();
        private static ProfessionNativePathAnimation Paths(ProfessionNativeAppearanceSkeleton skeleton, ProfessionNativeAnimationChannels animation)
            => PathChannels.GetValue(animation, value => new ProfessionNativePathAnimation(skeleton.Source, value.Name, value.OriginalBezierSegments));
        public static void EvaluateBeforePhysics(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels animation, float time, bool loop = true)
        {
            Validate(skeleton, animation, time);
            float position = loop && animation.Duration > 0 ? time % animation.Duration : time;
            var ik = animation.Constraints.SampleIk(position, skeleton.SkinConstraints);
            var transforms = animation.Constraints.SampleTransforms(position, skeleton.SkinConstraints);
            var paths = Paths(skeleton, animation).Sample(position, skeleton.SkinConstraints);
            skeleton.EvaluateNativeAnimation(animation, time, loop);
            Solve(skeleton, ik, transforms, paths);
        }

        public static void EvaluateMixNextBeforePhysics(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels outgoing, float outgoingTime, bool outgoingLoop,
            ProfessionNativeAnimationChannels incoming, float incomingTime, bool incomingLoop, float weight)
        {
            Validate(skeleton, outgoing, outgoingTime);
            Validate(skeleton, incoming, incomingTime);
            if (!float.IsFinite(weight) || weight < 0 || weight > 1)
                throw new ArgumentOutOfRangeException(nameof(weight));
            float from = outgoingLoop && outgoing.Duration > 0 ? outgoingTime % outgoing.Duration : outgoingTime;
            float to = incomingLoop && incoming.Duration > 0 ? incomingTime % incoming.Duration : incomingTime;
            var ik = outgoing.Constraints.SampleIk(from, skeleton.SkinConstraints);
            var transforms = outgoing.Constraints.SampleTransforms(from, skeleton.SkinConstraints);
            var paths = Paths(skeleton, outgoing).Sample(from, skeleton.SkinConstraints);
            incoming.Constraints.ApplyReplace(ik, transforms, to, weight);
            Paths(skeleton, incoming).ApplyReplace(paths, to, weight);
            skeleton.EvaluateNativeAnimation(outgoing, outgoingTime, outgoingLoop);
            skeleton.ApplyMixNext(incoming, to, weight);
            Solve(skeleton, ik, transforms, paths);
        }

        private static void Validate(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels animation, float time)
        {
            if (skeleton == null || animation == null) throw new ArgumentNullException();
            if (skeleton.Source.Manifest != "884_156692" ||
                !skeleton.Source.Sex.StartsWith("partner-battle:", StringComparison.Ordinal) ||
                !ReferenceEquals(animation.OriginalFields, skeleton.Source.GetAnimation(animation.Name)))
                throw new ArgumentException("Original full-binary partner battle animation identity differs.");
            if (!float.IsFinite(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            for (int index = 0; index < skeleton.Source.Constraints.Count; index++)
            {
                var definition = skeleton.Source.Constraints[index];
                if (definition.Kind == "ik" || definition.Kind == "transform" || definition.Kind == "path" || definition.Kind == "physics") continue;
                if (definition.Fields.TryGetValue("skin", out object required) && (bool)required
                    && !skeleton.SkinConstraints.Contains(index)) continue;
                throw new NotSupportedException("Restore original world constraint kind: " + definition.Kind);
            }
        }

        private static void Solve(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeConstraintAnimation.IkState[] ik, ProfessionNativeIdleTransform.Constraint[] transforms,
            ProfessionNativePathAnimation.State[] paths)
        {
            int[] orders = ik.Select(value => value.Order).Concat(transforms.Select(value => value.Order))
                .Concat(paths.Select(value => value.Order))
                .OrderBy(value => value).ToArray();
            if (orders.Distinct().Count() != orders.Length)
                throw new ArgumentException("Original partner constraint order is ambiguous.");
            foreach (int order in orders)
            {
                var chain = ik.FirstOrDefault(value => value.Order == order);
                if (chain != null && chain.Bones.Length == 1)
                {
                    var bones = (List<object>)skeleton.Source.SourceFields["bones"];
                    var bone = (Dictionary<string, object>)bones[chain.Bones[0]];
                    float length = bone.TryGetValue("length", out object value)
                        ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : 0;
                    ProfessionNativeAffineIk.ApplySingle(skeleton.Pose, chain, length);
                }
                else if (chain != null) ProfessionNativeAffineIk.ApplyPair(skeleton.Pose, chain);
                else if (paths.FirstOrDefault(value => value.Order == order) is ProfessionNativePathAnimation.State path)
                    ProfessionNativePathAnimation.Apply(skeleton, path);
                else ApplyTransform(skeleton.Pose, transforms.Single(value => value.Order == order));
            }
            skeleton.Pose.UpdateWorld();
        }

        private static void ApplyTransform(ProfessionNativeBonePose pose, ProfessionNativeIdleTransform.Constraint state)
        {
            if (!state.Relative)
            {
                ProfessionNativeIdleTransform.ApplyUnclamped(pose, new[] { state });
                return;
            }
            if (state.Rotation == 0 && state.X == 0 && state.Y == 0 && state.ScaleX == 0 && state.ScaleY == 0 && state.Shear == 0)
                return;
            // Original 11042 rotate_01/rotate_02 animate only relative local
            // rotation. Add the target's local angle; preserve solved parents.
            if (!state.Local || state.X != 0 || state.Y != 0 || state.ScaleX != 0 || state.ScaleY != 0 || state.Shear != 0)
                throw new NotSupportedException("Restore additional original relative transform axes: " + state.Name);
            if (!float.IsFinite(state.Rotation) || !float.IsFinite(state.OffsetRotation))
                throw new ArgumentException("Original relative local rotation is not finite.");
            if (!pose.IsActive(state.Target)) return;
            if (pose.HasWorldOverride(state.Target))
                throw new NotSupportedException("Restore original relative local target decomposition: " + state.Name);
            float angle = (pose.LocalRotation(state.Target) + state.OffsetRotation) * state.Rotation;
            foreach (int bone in state.Bones)
            {
                if (!pose.IsActive(bone)) continue;
                if (bone == state.Target) throw new ArgumentException("Original relative constraint targets itself.");
                pose.SetConstrainedRotation(bone, pose.LocalRotation(bone) + angle);
            }
        }
    }
}
