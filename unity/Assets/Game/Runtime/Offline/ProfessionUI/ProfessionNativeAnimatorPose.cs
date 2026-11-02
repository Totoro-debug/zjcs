using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Unity owns the graph, speed and clock. This adapter consumes one unmixed
    // original motion; the supplied solver owns constraints and physics.
    public sealed class ProfessionNativeAnimatorPose
    {
        public readonly struct Frame
        {
            public readonly AnimationClip Clip;
            public readonly ProfessionNativeAnimationChannels Animation;
            public readonly float Time;
            public readonly float MotionTime;
            public readonly bool Loop;
            public readonly bool PlaysBackward;
            internal Frame(AnimationClip clip, ProfessionNativeAnimationChannels animation, float time,
                float motionTime, bool loop, bool playsBackward)
            { Clip = clip; Animation = animation; Time = time; MotionTime = motionTime; Loop = loop; PlaysBackward = playsBackward; }
        }

        private readonly Animator animator;
        private readonly Action<ProfessionNativeAppearanceSkeleton, ProfessionNativeAnimationChannels, float, bool> solve;
        private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>(1);
        private readonly Dictionary<string, ProfessionNativeAnimationChannels> channels = new Dictionary<string, ProfessionNativeAnimationChannels>(StringComparer.Ordinal);
        private ProfessionNativeSkeletonData source;

        public ProfessionNativeAnimatorPose(Animator animator,
            Action<ProfessionNativeAppearanceSkeleton, ProfessionNativeAnimationChannels, float, bool> solve)
        {
            this.animator = animator ? animator : throw new ArgumentNullException(nameof(animator));
            this.solve = solve ?? throw new ArgumentNullException(nameof(solve));
        }

        public Frame ApplyUnmixed(ProfessionNativeAppearanceSkeleton skeleton)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            // Reject before touching the destination, rather than silently
            // selecting one leaf of a transition or a blend tree.
            if (!animator || !animator.runtimeAnimatorController || animator.layerCount != 1)
                throw new NotSupportedException("Bind a single original Animator layer before sampling native poses.");
            // GetNextAnimatorClipInfo can retain the previous transition's list
            // after Rebind. It is not evidence of an active second motion when
            // Unity reports no transition and the current motion has weight one.
            if (animator.IsInTransition(0))
                throw new NotSupportedException("Native animation transition mixing remains unbound.");
            animator.GetCurrentAnimatorClipInfo(0, clips);
            if (clips.Count != 1 || !clips[0].clip || Mathf.Abs(clips[0].weight - 1f) > .00001f)
                throw new NotSupportedException("Native animation sampling requires one full-weight original motion.");
            var clip = clips[0].clip;
            if (!ReferenceEquals(source, skeleton.Source))
            { channels.Clear(); source = skeleton.Source; }
            if (!channels.TryGetValue(clip.name, out var animation))
            {
                animation = new ProfessionNativeAnimationChannels(source, clip.name);
                channels.Add(clip.name, animation);
            }
            var state = animator.GetCurrentAnimatorStateInfo(0);
            float time = state.normalizedTime * clip.length;
            if (float.IsNaN(time) || float.IsInfinity(time)) throw new InvalidOperationException("Animator motion time is not finite.");
            float motionTime = time;
            // Original name-carrier clips retain their animation duration. A
            // mismatched binding must be restored, never stretched to fit.
            if (Mathf.Abs(clip.length - animation.Duration) > 1f / 30f + .00001f)
                throw new InvalidOperationException("Original Animator and skeleton motion durations differ: " + clip.name);
            time = state.loop && animation.Duration > 0
                ? time - Mathf.Floor(time / animation.Duration) * animation.Duration
                : Mathf.Clamp(time, 0, animation.Duration);
            solve(skeleton, animation, time, state.loop);
            return new Frame(clip, animation, time, motionTime, state.loop,
                animator.speed * state.speed * state.speedMultiplier < 0);
        }
    }
}
