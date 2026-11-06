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
        public static void EvaluateSetupPose(ProfessionNativeAppearanceSkeleton skeleton)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            if (skeleton.Source.Constraints.Any(value => value.Kind != "ik" && value.Kind != "transform"))
                throw new NotSupportedException("Bind path/physics setup constraints before using this endpoint.");
            var setup = ProfessionNativeConstraintAnimation.CreateSetup(skeleton.Source);
            skeleton.SetToSetupPose(); skeleton.UpdateCache();
            Solve(skeleton, setup.SampleIk(0, skeleton.SkinConstraints), setup.SampleTransforms(0, skeleton.SkinConstraints),
                Array.Empty<ProfessionNativePathAnimation.State>(), null);
        }
        public readonly struct AnimationLayer
        {
            public readonly ProfessionNativeAnimationChannels Animation;
            public readonly float Time, Weight;
            public readonly bool Loop;
            public AnimationLayer(ProfessionNativeAnimationChannels animation, float time, bool loop, float weight)
            { Animation = animation; Time = time; Loop = loop; Weight = weight; }
        }
        public static void EvaluateStackWithAdditionalConstraints(ProfessionNativeAppearanceSkeleton skeleton,
            IReadOnlyList<AnimationLayer> layers, IReadOnlyDictionary<int, Action> additional)
        {
            if (layers == null || layers.Count == 0) throw new ArgumentException("At least one original animation is required.");
            foreach (var layer in layers)
            {
                Validate(skeleton, layer.Animation, layer.Time);
                if (!float.IsFinite(layer.Weight) || layer.Weight < 0 || layer.Weight > 1)
                    throw new ArgumentOutOfRangeException(nameof(layers));
            }
            var first = layers[0];
            float position = first.Loop && first.Animation.Duration > 0 ? first.Time % first.Animation.Duration : first.Time;
            var ik = first.Animation.Constraints.SampleIk(position, skeleton.SkinConstraints);
            var transforms = first.Animation.Constraints.SampleTransforms(position, skeleton.SkinConstraints);
            var paths = Paths(skeleton, first.Animation).Sample(position, skeleton.SkinConstraints);
            skeleton.EvaluateNativeAnimation(first.Animation, first.Time, first.Loop);
            for (int index = 1; index < layers.Count; index++)
            {
                var layer = layers[index];
                position = layer.Loop && layer.Animation.Duration > 0 ? layer.Time % layer.Animation.Duration : layer.Time;
                layer.Animation.Constraints.ApplyReplace(ik, transforms, position, layer.Weight);
                Paths(skeleton, layer.Animation).ApplyReplace(paths, position, layer.Weight);
                skeleton.ApplyMixNext(layer.Animation, position, layer.Weight);
            }
            Solve(skeleton, ik, transforms, paths, additional);
        }
        private static readonly ConditionalWeakTable<ProfessionNativeAnimationChannels, ProfessionNativePathAnimation> PathChannels =
            new ConditionalWeakTable<ProfessionNativeAnimationChannels, ProfessionNativePathAnimation>();
        private static ProfessionNativePathAnimation Paths(ProfessionNativeAppearanceSkeleton skeleton, ProfessionNativeAnimationChannels animation)
            => PathChannels.GetValue(animation, value => new ProfessionNativePathAnimation(skeleton.Source, value.Name, value.OriginalBezierSegments));
        public static void EvaluateBeforePhysics(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels animation, float time, bool loop = true)
            => EvaluateWithAdditionalConstraints(skeleton, animation, time, loop, null);

        public static void EvaluateWithAdditionalConstraints(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels animation, float time, bool loop,
            IReadOnlyDictionary<int, Action> additional)
        {
            Validate(skeleton, animation, time);
            float position = loop && animation.Duration > 0 ? time % animation.Duration : time;
            var ik = animation.Constraints.SampleIk(position, skeleton.SkinConstraints);
            var transforms = animation.Constraints.SampleTransforms(position, skeleton.SkinConstraints);
            var paths = Paths(skeleton, animation).Sample(position, skeleton.SkinConstraints);
            skeleton.EvaluateNativeAnimation(animation, time, loop);
            Solve(skeleton, ik, transforms, paths, additional);
        }

        public static void EvaluateMixNextBeforePhysics(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels outgoing, float outgoingTime, bool outgoingLoop,
            ProfessionNativeAnimationChannels incoming, float incomingTime, bool incomingLoop, float weight)
            => EvaluateMixNextWithAdditionalConstraints(skeleton, outgoing, outgoingTime, outgoingLoop,
                incoming, incomingTime, incomingLoop, weight, null);

        public static void EvaluateMixNextWithAdditionalConstraints(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels outgoing, float outgoingTime, bool outgoingLoop,
            ProfessionNativeAnimationChannels incoming, float incomingTime, bool incomingLoop, float weight,
            IReadOnlyDictionary<int, Action> additional)
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
            Solve(skeleton, ik, transforms, paths, additional);
        }

        private static void Validate(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels animation, float time)
        {
            if (skeleton == null || animation == null) throw new ArgumentNullException();
            if (skeleton.Source.Manifest != "884_156692" ||
                !(skeleton.Source.Sex.StartsWith("partner-battle:", StringComparison.Ordinal)
                    || skeleton.Source.Sex.StartsWith("partner-ui:", StringComparison.Ordinal)
                    || skeleton.Source.Sex.StartsWith("battle-monster:", StringComparison.Ordinal)) ||
                !ReferenceEquals(animation.OriginalFields, skeleton.Source.GetAnimation(animation.Name)))
                throw new ArgumentException("Original full-binary battle animation identity differs.");
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
            ProfessionNativePathAnimation.State[] paths, IReadOnlyDictionary<int, Action> additional)
        {
            int[] orders = ik.Select(value => value.Order).Concat(transforms.Select(value => value.Order))
                .Concat(paths.Select(value => value.Order))
                .Concat(additional?.Keys ?? Enumerable.Empty<int>())
                .OrderBy(value => value).ToArray();
            if (orders.Distinct().Count() != orders.Length)
                throw new ArgumentException("Original partner constraint order is ambiguous.");
            foreach (int order in orders)
            {
                if (additional != null && additional.TryGetValue(order, out var action))
                { action(); continue; }
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
