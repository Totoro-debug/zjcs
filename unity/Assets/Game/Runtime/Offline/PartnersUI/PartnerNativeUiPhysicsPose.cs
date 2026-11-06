using System;
using System.Collections.Generic;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Preserve the original partner-only boundary; both callers share the same
    // ordered affine/spring backend and per-skeleton physical history.
    public sealed class PartnerNativeUiPhysicsPose
    {
        private readonly ProfessionNativeOrderedPhysicsPose pose;
        public PartnerNativeUiPhysicsPose(ProfessionNativeSkeletonData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!data.Sex.StartsWith("partner-ui:", StringComparison.Ordinal))
                throw new ArgumentException("Bind the original UI skeleton data.");
            pose = new ProfessionNativeOrderedPhysicsPose(data);
        }
        public float PhysicsTime => pose.PhysicsTime;
        public void Translate(float x, float y) => pose.Translate(x, y);
        public void Rotate(float x, float y, float degrees) => pose.Rotate(x, y, degrees);
        public void Apply(ProfessionNativeAppearanceSkeleton skeleton, ProfessionNativeAnimationChannels incoming,
            float time, bool loop, float delta, ProfessionNativeAnimationChannels outgoing = null,
            float outgoingTime = 0, bool outgoingLoop = false, float weight = 1)
            => pose.Apply(skeleton, incoming, time, loop, delta, outgoing, outgoingTime, outgoingLoop, weight);
        public void Apply(ProfessionNativeAppearanceSkeleton skeleton,
            IReadOnlyList<ProfessionNativeOrderedConstraintPose.AnimationLayer> layers, float delta)
            => pose.Apply(skeleton, layers, delta);
    }
}
