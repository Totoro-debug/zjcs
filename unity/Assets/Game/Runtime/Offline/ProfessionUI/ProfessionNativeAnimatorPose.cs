using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Unity owns the graph, speed and clocks. Explicit unmixed/MixNext paths
    // consume original motions; the supplied solver owns constraints and physics.
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

        public readonly struct MixNextFrame
        {
            public readonly Frame Outgoing, Incoming;
            public readonly float Weight;
            internal MixNextFrame(Frame outgoing, Frame incoming, float weight)
            { Outgoing = outgoing; Incoming = incoming; Weight = weight; }
        }

        public readonly struct AppliedFrame
        {
            public readonly Frame Current;
            public readonly Frame? Incoming;
            public readonly float IncomingWeight;
            internal AppliedFrame(Frame current, Frame? incoming, float incomingWeight)
            { Current = current; Incoming = incoming; IncomingWeight = incomingWeight; }
        }

        private readonly Animator animator;
        private readonly Action<ProfessionNativeAppearanceSkeleton, ProfessionNativeAnimationChannels, float, bool> solve;
        private readonly Action<ProfessionNativeAppearanceSkeleton, MixNextFrame> mix;
        private readonly bool allowPartnerBlendTrees;
        private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>(1);
        private readonly List<AnimatorClipInfo> nextClips = new List<AnimatorClipInfo>(1);
        private readonly Dictionary<string, ProfessionNativeAnimationChannels> channels = new Dictionary<string, ProfessionNativeAnimationChannels>(StringComparer.Ordinal);
        private ProfessionNativeSkeletonData source;

        public ProfessionNativeAnimatorPose(Animator animator,
            Action<ProfessionNativeAppearanceSkeleton, ProfessionNativeAnimationChannels, float, bool> solve,
            Action<ProfessionNativeAppearanceSkeleton, MixNextFrame> mixNext = null,
            bool allowPartnerBlendTrees = false)
        {
            this.animator = animator ? animator : throw new ArgumentNullException(nameof(animator));
            this.solve = solve ?? throw new ArgumentNullException(nameof(solve));
            mix = mixNext;
            this.allowPartnerBlendTrees = allowPartnerBlendTrees;
        }

        // Callers consume one completed pose without inspecting the Animator
        // graph themselves. An unbound mixer retains the strict unmixed domain.
        public AppliedFrame Apply(ProfessionNativeAppearanceSkeleton skeleton)
        {
            if (allowPartnerBlendTrees && animator && animator.runtimeAnimatorController && animator.layerCount == 1
                && !animator.IsInTransition(0))
            {
                ReadPartnerClips(skeleton);
                if (clips.Count == 2)
                {
                    if (mix == null) throw new InvalidOperationException("Bind the original partner MixNext solver before blending motions.");
                    float first = clips[0].weight, second = clips[1].weight;
                    if (!clips[0].clip || !clips[1].clip || !float.IsFinite(first) || !float.IsFinite(second)
                        || first <= 0 || second <= 0 || first > 1 || second > 1 || Mathf.Abs(first + second - 1) > .0001f)
                        throw new NotSupportedException("Original partner blend tree requires two complementary motion weights.");
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    var frame = new MixNextFrame(Read(skeleton, clips[0].clip, state), Read(skeleton, clips[1].clip, state), second);
                    mix(skeleton, frame);
                    return new AppliedFrame(frame.Outgoing, frame.Incoming, frame.Weight);
                }
            }
            if (mix != null && animator && animator.runtimeAnimatorController && animator.layerCount == 1
                && animator.IsInTransition(0))
            {
                var transition = ApplyMixNext(skeleton);
                return new AppliedFrame(transition.Outgoing, transition.Incoming, transition.Weight);
            }
            return new AppliedFrame(ApplyUnmixed(skeleton), null, 0);
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
            if (allowPartnerBlendTrees) ReadPartnerClips(skeleton);
            else animator.GetCurrentAnimatorClipInfo(0, clips);
            if (clips.Count != 1 || !clips[0].clip || Mathf.Abs(clips[0].weight - 1f) > .00001f)
                throw new NotSupportedException("Native animation sampling requires one full-weight original motion.");
            var frame = Read(skeleton, clips[0].clip, animator.GetCurrentAnimatorStateInfo(0));
            solve(skeleton, frame.Animation, frame.Time, frame.Loop);
            return frame;
        }

        // The audited 11004/11014 controllers have two-leaf 1D idle trees and
        // auto-reset MixNext/Replace. Unity supplies both synchronized clocks
        // and ordered weights; blend local channels before world constraints.
        private void ReadPartnerClips(ProfessionNativeAppearanceSkeleton skeleton)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            if (skeleton.Source.Manifest != "884_156692" ||
                !skeleton.Source.Sex.StartsWith("partner-battle:", StringComparison.Ordinal))
                throw new NotSupportedException("Verify the original partner battle blend-tree source before sampling.");
            animator.GetCurrentAnimatorClipInfo(0, clips);
            // Unity retains zero-weight leaves at both threshold endpoints.
            // They do not contribute a pose or reset an unkeyed channel.
            clips.RemoveAll(value => value.weight == 0);
        }

        // This explicit path matches the original Game prefab's auto-reset,
        // custom MixNext/Replace base layer. It does not guess blend-tree,
        // additive, multi-layer or interrupted-transition semantics.
        public MixNextFrame ApplyMixNext(ProfessionNativeAppearanceSkeleton skeleton)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            if (mix == null) throw new InvalidOperationException("Bind an explicit native MixNext solver.");
            if (!animator || !animator.runtimeAnimatorController || animator.layerCount != 1 || !animator.IsInTransition(0))
                throw new NotSupportedException("MixNext requires an active single-layer original Animator transition.");
            animator.GetCurrentAnimatorClipInfo(0, clips); animator.GetNextAnimatorClipInfo(0, nextClips);
            if (clips.Count != 1 || nextClips.Count != 1 || !clips[0].clip || !nextClips[0].clip)
                throw new NotSupportedException("MixNext requires one outgoing and one incoming original motion.");
            float weight = nextClips[0].weight, previous = clips[0].weight;
            if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0 || weight > 1 ||
                float.IsNaN(previous) || float.IsInfinity(previous) || previous < 0 || previous > 1 ||
                Mathf.Abs(previous + weight - 1) > .0001f)
                throw new NotSupportedException("Original two-motion Animator weights are not complementary.");
            var frame = new MixNextFrame(Read(skeleton, clips[0].clip, animator.GetCurrentAnimatorStateInfo(0)),
                Read(skeleton, nextClips[0].clip, animator.GetNextAnimatorStateInfo(0)), weight);
            mix(skeleton, frame);
            return frame;
        }

        private Frame Read(ProfessionNativeAppearanceSkeleton skeleton, AnimationClip clip, AnimatorStateInfo state)
        {
            if (!ReferenceEquals(source, skeleton.Source))
            { channels.Clear(); source = skeleton.Source; }
            if (!channels.TryGetValue(clip.name, out var animation))
            {
                animation = new ProfessionNativeAnimationChannels(source, clip.name);
                channels.Add(clip.name, animation);
            }
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
            return new Frame(clip, animation, time, motionTime, state.loop,
                animator.speed * state.speed * state.speedMultiplier < 0);
        }
    }
}
