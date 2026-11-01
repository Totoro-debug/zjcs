using System;
using System.Linq;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Illustration-only native stage. Additional solver branches are rejected
    // explicitly, so an unbound rig cannot silently display an incomplete pose.
    public static class PartnerNativePortraitPose
    {
        public static void Evaluate(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels animation, float time, bool loop)
        {
            if (skeleton == null || animation == null) throw new ArgumentNullException();
            if (skeleton.Source.Manifest != "884_156692" ||
                !skeleton.Source.Sex.StartsWith("partner-portrait:", StringComparison.Ordinal) ||
                !ReferenceEquals(animation.OriginalFields, skeleton.Source.GetAnimation(animation.Name)))
                throw new ArgumentException("Original partner portrait animation identity differs.");
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0)
                throw new ArgumentOutOfRangeException(nameof(time));
            if (skeleton.Source.Constraints.Any(value => value.Kind != "ik" && value.Kind != "transform"))
                throw new NotSupportedException("Restore the additional original partner portrait constraint kind.");
            float position = loop && animation.Duration > 0 ? time % animation.Duration : time;
            var ik = animation.Constraints.SampleIk(position, skeleton.SkinConstraints);
            var transforms = animation.Constraints.SampleTransforms(position, skeleton.SkinConstraints);
            if (ik.Any(value => value.Mix != 0 && (value.Softness != 0 || value.Stretch || value.Compress || value.Uniform)))
                throw new NotSupportedException("Restore the original partner portrait soft/stretch/compress IK branch.");
            skeleton.EvaluateNativeAnimation(animation, time, loop);
            int[] orders = ik.Select(value => value.Order).Concat(transforms.Select(value => value.Order)).OrderBy(value => value).ToArray();
            if (orders.Distinct().Count() != orders.Length) throw new ArgumentException("Original partner constraint order is ambiguous.");
            foreach (int order in orders)
            {
                var chain = ik.FirstOrDefault(value => value.Order == order);
                if (chain != null)
                    ProfessionNativeIdleIk.ApplyPreservingWorldConstraints(skeleton.Pose, new[] { new ProfessionNativeIdleIk.Constraint {
                        name = chain.Name, order = chain.Order, target = chain.Target, bones = chain.Bones,
                        childLength = chain.ChildLength, mix = chain.Mix, bendPositive = chain.BendPositive } });
                else ProfessionNativeIdleTransform.ApplyUnclamped(skeleton.Pose, new[] { transforms.Single(value => value.Order == order) });
            }
            skeleton.Pose.UpdateWorld();
        }
    }
}
