using System;
using System.Collections.Generic;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent implementation of the application's track-zero, zero-mix
    // weapon requests. Advancing belongs to the renderer's Update; applying a
    // completed pose (including after a skin reset) never advances the clock.
    public sealed class ProfessionNativeSceneWeaponPlayback : IProfessionNativeSceneWeaponPoseProducer
    {
        private ProfessionNativeAppearanceSkeleton state;
        private readonly Dictionary<string, ProfessionNativeAnimationChannels> motions = new Dictionary<string, ProfessionNativeAnimationChannels>(StringComparer.Ordinal);
        private ProfessionNativeAnimationChannels animation;
        private ProfessionNativePlayerPhysicsPose physics;
        private bool loop;
        private float duration, time;
        public float PhysicsTime => physics?.PhysicsTime ?? 0;
        public event Action<ProfessionNativeAppearanceSkeleton> UpdateLocal, UpdateComplete;

        private void ApplyPose(bool updatePhysics, float delta)
            => physics.Apply(state, animation, time, false, delta, updatePhysics,
                () => UpdateLocal?.Invoke(state), () => UpdateComplete?.Invoke(state));

        public void Play(ProfessionNativeAppearanceSkeleton target, string name, bool repeat)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (target.Source.Sex != "primary-weapon")
                throw new NotSupportedException("Expected the original primary weapon model.");
            if (!ReferenceEquals(state?.Source, target.Source)) motions.Clear();
            if (!motions.TryGetValue(name, out var motion))
            {
                motion = new ProfessionNativeAnimationChannels(target.Source, name);
                motions.Add(name, motion);
            }
            // Replaying track zero restarts its animation, while the same
            // skeleton retains its physical time and constraint histories.
            if (!ReferenceEquals(state, target)) physics = new ProfessionNativePlayerPhysicsPose(target.Source);
            state = target; animation = motion; duration = motion.Duration; loop = repeat; time = 0;
            Apply(target);
        }

        public void Advance(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0)
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (state == null) return;
            float next = time + delta;
            if (float.IsInfinity(next)) throw new ArgumentOutOfRangeException(nameof(delta));
            // Keep the accumulator at the original motion's bounded time.
            // Zero-duration clips retain their frame-zero pose.
            time = duration == 0 ? 0 : loop ? next % duration : Math.Min(next, duration);
            ApplyPose(true, delta);
        }

        public void Apply(ProfessionNativeAppearanceSkeleton target)
        {
            if (state == null || !ReferenceEquals(state, target))
                throw new InvalidOperationException("Play the original weapon animation on this skeleton before applying it.");
            ApplyPose(false, 0);
        }
    }
}
