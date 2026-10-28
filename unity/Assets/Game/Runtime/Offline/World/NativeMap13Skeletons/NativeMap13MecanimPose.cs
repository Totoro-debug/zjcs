using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    // The Animator owns every state/condition/transition and playback clock.
    // This independent adapter only samples original planar curves at that clock.
    public sealed class NativeMap13MecanimPose
    {
        private readonly Animator animator;
        private readonly NativeMap13SkeletonView view;
        private readonly Dictionary<string, NativeMap13Animation> animations;
        public NativeMap13MecanimPose(Animator animator, NativeMap13SkeletonView view,
            NativeMap13SkeletonData data, NativeMap13Renderer profile)
        {
            if (!animator || view == null || !data || profile?.translator == null)
                throw new InvalidOperationException("Original Map13 Mecanim/geometry bindings are incomplete.");
            var t = profile.translator;
            if (animator.layerCount != 1 || !t.autoReset || !t.useCustomMixMode ||
                t.layerMixModes == null || t.layerMixModes.Length == 0 || t.layerMixModes[0] != 1 ||
                t.layerBlendModes == null || t.layerBlendModes.Length == 0 || t.layerBlendModes[0] != 1)
                throw new InvalidOperationException("Additional original world animation layer/mix mode needs migration.");
            this.animator = animator; this.view = view;
            animations = data.Source.animations.ToDictionary(a => a.name, StringComparer.Ordinal);
        }
        private NativeMap13Animation Animation(AnimatorClipInfo[] clips)
        {
            if (clips.Length == 0) return null;
            if (clips.Length != 1 || !clips[0].clip || !animations.TryGetValue(clips[0].clip.name, out var result))
                throw new InvalidOperationException("Original Map13 single-leaf animation identity differs.");
            return result;
        }
        private static float Time(NativeMap13Animation animation, AnimatorStateInfo state)
        {
            if (animation == null || animation.duration <= 0) return 0;
            float time = state.normalizedTime * animation.duration;
            return state.loop ? Mathf.Repeat(time, animation.duration) : Mathf.Clamp(time, 0, animation.duration);
        }
        public void Sample()
        {
            var current = animator.GetCurrentAnimatorClipInfo(0);
            var previous = Animation(current);
            float previousTime = Time(previous, animator.GetCurrentAnimatorStateInfo(0));
            if (!animator.IsInTransition(0)) { view.ApplyPose(previous, previousTime); return; }
            var nextClips = animator.GetNextAnimatorClipInfo(0);
            var next = Animation(nextClips);
            float time = Time(next, animator.GetNextAnimatorStateInfo(0));
            view.ApplyBlend(previous, previousTime, next, time, nextClips.Length == 0 ? 0 : Mathf.Clamp01(nextClips[0].weight));
        }
    }
}
