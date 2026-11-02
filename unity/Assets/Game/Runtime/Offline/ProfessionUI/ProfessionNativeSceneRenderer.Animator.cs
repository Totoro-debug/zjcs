using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSceneRenderer : IProfessionAppearancePartMecanim
    {
        private ProfessionNativeAnimatorPose animatorPose;
        private ProfessionNativeAppearanceSkeleton notifiedSkeleton;
        private ProfessionNativeAnimationChannels notifiedAnimation;
        private float notifiedTime;
        public event ProfessionPartClipApplied OnClipApplied;

        // Bind explicitly: callers must choose a completed constraint/physics
        // solver. The renderer does not silently install a pre-physics fallback.
        public void BindAnimatorPose(ProfessionNativeAnimatorPose pose)
        {
            animatorPose = pose ?? throw new ArgumentNullException(nameof(pose));
            ClearAnimatorNotification();
        }

        public ProfessionNativeAnimatorPose.Frame ApplyAnimatorPose()
        {
            if (animatorPose == null) throw new InvalidOperationException("Bind the native Animator pose producer first.");
            Initialize(false, false);
            var frame = animatorPose.ApplyUnmixed(State);
            float previous = ReferenceEquals(notifiedSkeleton, State) && ReferenceEquals(notifiedAnimation, frame.Animation)
                ? notifiedTime : -1;
            notifiedSkeleton = State; notifiedAnimation = frame.Animation; notifiedTime = frame.MotionTime;
            // The original part handler compares animation identity, not the
            // Unity carrier clip. Multiple carriers of one native motion must
            // not repeatedly restart the independent part animation.
            OnClipApplied?.Invoke(frame.Animation, frame.Animation.Name, 0, 1,
                frame.MotionTime, previous, frame.PlaysBackward);
            return frame;
        }

        private void UpdateBoundAnimatorPose()
        {
            if (animatorPose != null) ApplyAnimatorPose();
        }

        private void ClearAnimatorNotification()
        {
            notifiedSkeleton = null; notifiedAnimation = null; notifiedTime = -1;
        }
    }
}
