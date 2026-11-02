using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSceneRenderer : IProfessionAppearancePartMecanim
    {
        private ProfessionNativeAnimatorPose animatorPose;
        private ProfessionNativeAppearanceSkeleton notifiedSkeleton;
        private readonly Dictionary<ProfessionNativeAnimationChannels, float> notifiedTimes = new Dictionary<ProfessionNativeAnimationChannels, float>();
        public event ProfessionPartClipApplied OnClipApplied;

        // Bind explicitly: callers must choose a completed constraint/physics
        // solver. The renderer does not silently install a pre-physics fallback.
        public void BindAnimatorPose(ProfessionNativeAnimatorPose pose)
        {
            animatorPose = pose ?? throw new ArgumentNullException(nameof(pose));
            ClearAnimatorNotification();
        }

        public ProfessionNativeAnimatorPose.AppliedFrame ApplyAnimatorPose()
        {
            if (animatorPose == null) throw new InvalidOperationException("Bind the native Animator pose producer first.");
            Initialize(false, false);
            var frame = animatorPose.Apply(State);
            NotifyApplied(frame.Current, 1);
            if (frame.Incoming.HasValue && frame.IncomingWeight > 0)
                NotifyApplied(frame.Incoming.Value, frame.IncomingWeight);
            return frame;
        }

        private void NotifyApplied(ProfessionNativeAnimatorPose.Frame frame, float weight)
        {
            if (!ReferenceEquals(notifiedSkeleton, State))
            { notifiedTimes.Clear(); notifiedSkeleton = State; }
            float previous = notifiedTimes.TryGetValue(frame.Animation, out float time) ? time : -1;
            notifiedTimes[frame.Animation] = frame.MotionTime;
            // The original part handler compares animation identity, not the
            // Unity carrier clip. Multiple carriers of one native motion must
            // not repeatedly restart the independent part animation.
            OnClipApplied?.Invoke(frame.Animation, frame.Animation.Name, 0, weight,
                frame.MotionTime, previous, frame.PlaysBackward);
        }

        private void UpdateBoundAnimatorPose()
        {
            if (animatorPose != null) ApplyAnimatorPose();
        }

        private void ClearAnimatorNotification()
        {
            notifiedSkeleton = null; notifiedTimes.Clear();
        }
    }
}
