using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Unity evaluates the original Animator before renderer LateUpdate. Only
    // that lifecycle call supplies elapsed time; explicit redraws use Pose.
    internal sealed class ProfessionNativeScenePlayerPlayback
    {
        private ProfessionNativeSkeletonData source;
        private ProfessionNativePlayerPhysicsPose physics;
        internal readonly ProfessionNativeAnimatorPose Pose;
        internal float DeltaTime;
        internal float PhysicsTime => physics?.PhysicsTime ?? 0;

        internal ProfessionNativeScenePlayerPlayback(Animator animator)
        {
            Pose = new ProfessionNativeAnimatorPose(animator,
                Solve, SolveMix, originalBezierSegments: true);
        }

        private void Solve(ProfessionNativeAppearanceSkeleton state, ProfessionNativeAnimationChannels motion, float time, bool loop)
        {
            float delta = DeltaTime; DeltaTime = 0;
            Physics(state).Apply(state, motion, time, loop, delta, updatePhysics: delta != 0);
        }

        private void SolveMix(ProfessionNativeAppearanceSkeleton state, ProfessionNativeAnimatorPose.MixNextFrame mix)
        {
            float delta = DeltaTime; DeltaTime = 0;
            Physics(state).ApplyMixNext(state,
                mix.Outgoing.Animation, mix.Outgoing.Time, mix.Outgoing.Loop,
                mix.Incoming.Animation, mix.Incoming.Time, mix.Incoming.Loop,
                mix.Weight, delta, updatePhysics: delta != 0);
        }

        private ProfessionNativePlayerPhysicsPose Physics(ProfessionNativeAppearanceSkeleton state)
        {
            if (!ReferenceEquals(source, state.Source))
            {
                source = state.Source;
                physics = new ProfessionNativePlayerPhysicsPose(source);
            }
            return physics;
        }
    }
}
