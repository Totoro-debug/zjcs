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
        private readonly Dictionary<string, float> durations = new Dictionary<string, float>(StringComparer.Ordinal);
        private string animation;
        private bool loop;
        private float duration, time;

        public void Play(ProfessionNativeAppearanceSkeleton target, string name, bool repeat)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            // This sampler verifies the actual weapon skin has no active
            // constraints and that its original animation contains bones only.
            target.EvaluateUnmixedWeaponAnimation(name, 0);
            if (!ReferenceEquals(state?.Source, target.Source)) durations.Clear();
            if (!durations.TryGetValue(name, out float length))
            {
                length = new ProfessionNativeAnimationChannels(target.Source, name).Duration;
                durations.Add(name, length);
            }
            state = target; animation = name; duration = length; loop = repeat; time = 0;
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
        }

        public void Apply(ProfessionNativeAppearanceSkeleton target)
        {
            if (state == null || !ReferenceEquals(state, target))
                throw new InvalidOperationException("Play the original weapon animation on this skeleton before applying it.");
            target.EvaluateUnmixedWeaponAnimation(animation, time);
        }
    }
}
