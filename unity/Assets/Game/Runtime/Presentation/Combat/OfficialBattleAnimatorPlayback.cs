using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryPlayables;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Binds extracted battle cues to the migrated official Animator playables.</summary>
    public sealed class OfficialBattleAnimatorPlayback : IDisposable
    {
        private sealed class ControllerBinding
        {
            internal readonly AnimatorControllerParameter[] Parameters;
            internal readonly HashSet<string> Layers = new HashSet<string>();
            internal ControllerBinding(Animator animator)
            {
                Parameters = animator.parameters;
                for (int index = 0; index < animator.layerCount; index++) Layers.Add(animator.GetLayerName(index));
            }
        }
        private static readonly ConditionalWeakTable<RuntimeAnimatorController, ControllerBinding> bindings
            = new ConditionalWeakTable<RuntimeAnimatorController, ControllerBinding>();

        private static ControllerBinding ReadBinding(Animator animator)
        {
            var controller = animator.runtimeAnimatorController;
            if (!controller) return null;
            if (bindings.TryGetValue(controller, out var binding)) return binding;
            var chain = new List<RuntimeAnimatorController>();
            for (var current = controller; current && !chain.Contains(current);)
            {
                chain.Add(current);
                if (bindings.TryGetValue(current, out binding)) break;
                current = current is AnimatorOverrideController overridden
                    ? overridden.runtimeAnimatorController : null;
            }
            // Original skill graphs can hide the source model. Unity then has
            // no live Animator parameters; retain the actual initialized
            // controller contract, without activating the model for inspection.
            if (binding == null)
            {
                if (!animator.gameObject.activeInHierarchy || !animator.isInitialized) return null;
                binding = new ControllerBinding(animator);
            }
            // Overrides replace clips, while parameters and layers belong to
            // the same underlying controller. Appearance refreshes can replace
            // the override instance even while a skill hides the source.
            foreach (var entry in chain)
                if (!bindings.TryGetValue(entry, out _)) bindings.Add(entry, binding);
            return binding;
        }
        private readonly Animator animator;
        private readonly RuntimeAnimatorController originalController;
        private readonly RuntimeAnimatorController ownedController;
        private readonly GameObject root;
        private readonly AnimationController controller;
        private readonly MyPlayableCenter center = new MyPlayableCenter();
        private readonly List<MyPlayableAnimator> active = new List<MyPlayableAnimator>();
        private readonly Dictionary<MyPlayableAnimator, float> ownerLifetimes = new Dictionary<MyPlayableAnimator, float>();
        private bool disposed;

        public static bool Supports(Animator animator, OfficialBattleSkillPlayableCue cue)
        {
            if (animator == null || animator.runtimeAnimatorController == null || cue == null) return false;
            var binding = ReadBinding(animator);
            if (binding == null || !HasParameter(binding, cue.my_bool_param, AnimatorControllerParameterType.Bool)
                || !HasParameter(binding, cue.my_float_param, AnimatorControllerParameterType.Float)
                || !HasParameter(binding, cue.int_param, AnimatorControllerParameterType.Int)
                || !HasParameter(binding, cue.trigger_param, AnimatorControllerParameterType.Trigger)) return false;
            if (!string.IsNullOrEmpty(cue.layer_name) && !binding.Layers.Contains(cue.layer_name)) return false;
            if (cue.kind != 1 || string.IsNullOrEmpty(cue.origin_clip_name)) return true;
            if (animator.runtimeAnimatorController is AnimatorOverrideController overridden)
            {
                var clips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overridden.GetOverrides(clips);
                foreach (var clip in clips)
                    if (clip.Key != null && clip.Key.name == cue.origin_clip_name) return true;
            }
            else
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null && clip.name == cue.origin_clip_name) return true;
            return false;
        }

        private static bool HasParameter(ControllerBinding binding, string name, AnimatorControllerParameterType type)
        {
            if (string.IsNullOrEmpty(name)) return true;
            foreach (var parameter in binding.Parameters)
                if (parameter.name == name && parameter.type == type) return true;
            return false;
        }

        public OfficialBattleAnimatorPlayback(Animator animator, Transform parent,
            AnimationController existingController = null)
        {
            this.animator = animator != null ? animator : throw new ArgumentNullException(nameof(animator));
            var binding = ReadBinding(animator);
            originalController = animator.runtimeAnimatorController;
            root = new GameObject("OfficialBattleAnimatorPlayables");
            root.transform.SetParent(parent, false);
            if (existingController != null)
            {
                if (existingController.myAnimator != animator || !existingController.myInited)
                    throw new ArgumentException("The original entity Animator controller must be initialized.", nameof(existingController));
                controller = existingController;
            }
            else
            {
                controller = root.AddComponent<AnimationController>();
                controller.myAnimator = animator;
                controller.NotCreateGraph = true;
                controller.CreateOverrideController = true;
                controller.Init();
                ownedController = controller.RuntimeAnimatorController;
                // Wrapping preserves the actual controller's parameters and
                // layers. A consecutive skill can create this wrapper while
                // the original graph still hides the source Animator, so its
                // contract must not depend on another active-frame read.
                if (binding != null && ownedController != null
                    && !bindings.TryGetValue(ownedController, out _))
                    bindings.Add(ownedController, binding);
            }
        }

        public void Play(OfficialBattleSkillPlayableCue cue, AnimationClip clip, float ownerDuration = -1f)
        {
            if (disposed || ownerDuration == 0f || !Supports(animator, cue)) return;
            var instance = new GameObject("SkillAnimatorPlayable");
            instance.transform.SetParent(root.transform, false);
            MyPlayableAnimator playable;
            if (cue.kind == 1)
            {
                var overridden = instance.AddComponent<MyPlayableOverrideAnimator>();
                overridden.OriginClipName = cue.origin_clip_name;
                overridden.OverrideClip = clip;
                playable = overridden;
            }
            else
                playable = instance.AddComponent<MyPlayableAnimator>();

            playable.MyPlayableCenter = center;
            playable.myApplyComponent = controller;
            playable.myTriggerList = new List<MyPlayableTriggerTimeCfg>();
            playable.myFadeoutTrigerList = new List<MyPlayableTrigger>();
            playable.myEndTrigerList = new List<MyPlayableTrigger>();
            playable.myBoolParam = cue.my_bool_param;
            playable.myBoolValue = cue.my_bool_value;
            playable.myFloatParam = cue.my_float_param;
            playable.myFloatValue = cue.my_float_value;
            playable.IntParam = cue.int_param;
            playable.IntValue = cue.int_value;
            playable.myTriggerParam = cue.trigger_param;
            playable.PreStateNameAllowList = cue.pre_state_name_allow_list ?? new List<string>();
            playable.myDuration = cue.my_duration;
            playable.LayerName = cue.layer_name;
            playable.LayerWeight = cue.layer_weight;
            playable.myEndResumeParam = cue.end_resume_param;
            playable.mySpeed = cue.speed;
            playable.UpdateHashMapString();
            playable.Init(animator.gameObject, animator.gameObject);
            active.Add(playable);
            if (ownerDuration > 0f) ownerLifetimes.Add(playable, ownerDuration);
            playable.TryPlay();
        }

        public void Tick(float deltaTime)
        {
            if (disposed || deltaTime <= 0f) return;
            center.Update(deltaTime);
            center.LateUpdate();
            for (int index = active.Count - 1; index >= 0; index--)
            {
                MyPlayableAnimator playable = active[index];
                if (playable != null && ownerLifetimes.TryGetValue(playable, out float remaining))
                {
                    remaining -= deltaTime;
                    ownerLifetimes[playable] = remaining;
                    if (remaining <= 0f) playable.Stop(true);
                }
                if (playable == null || playable.myStatus == MyPlayable.PlayableStatus.Stop)
                {
                    if (!ReferenceEquals(playable, null)) ownerLifetimes.Remove(playable);
                    if (playable != null) Destroy(playable.gameObject);
                    active.RemoveAt(index);
                }
            }
        }

        public void Stop()
        {
            for (int index = active.Count - 1; index >= 0; index--)
                if (active[index] != null)
                {
                    active[index].Stop(true);
                    Destroy(active[index].gameObject);
                }
            active.Clear();
            ownerLifetimes.Clear();
            center.RemoveAll();
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            if (ownedController != null && animator != null && animator.runtimeAnimatorController == ownedController)
                animator.runtimeAnimatorController = originalController;
            if (ownedController != null && ownedController != originalController) Destroy(ownedController);
            Destroy(root);
            disposed = true;
        }

        private static void Destroy(UnityEngine.Object instance)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
            else UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
