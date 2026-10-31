using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryPlayables;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Binds extracted battle cues to the migrated official Animator playables.</summary>
    public sealed class OfficialBattleAnimatorPlayback : IDisposable
    {
        private readonly Animator animator;
        private readonly RuntimeAnimatorController originalController;
        private readonly RuntimeAnimatorController ownedController;
        private readonly GameObject root;
        private readonly AnimationController controller;
        private readonly MyPlayableCenter center = new MyPlayableCenter();
        private readonly List<MyPlayableAnimator> active = new List<MyPlayableAnimator>();
        private bool disposed;

        public static bool Supports(Animator animator, OfficialBattleSkillPlayableCue cue)
        {
            if (animator == null || animator.runtimeAnimatorController == null || cue == null) return false;
            if (!HasParameter(animator, cue.my_bool_param, AnimatorControllerParameterType.Bool)
                || !HasParameter(animator, cue.my_float_param, AnimatorControllerParameterType.Float)
                || !HasParameter(animator, cue.int_param, AnimatorControllerParameterType.Int)
                || !HasParameter(animator, cue.trigger_param, AnimatorControllerParameterType.Trigger)) return false;
            if (!string.IsNullOrEmpty(cue.layer_name) && animator.GetLayerIndex(cue.layer_name) < 0) return false;
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

        private static bool HasParameter(Animator animator, string name, AnimatorControllerParameterType type)
        {
            if (string.IsNullOrEmpty(name)) return true;
            foreach (var parameter in animator.parameters)
                if (parameter.name == name && parameter.type == type) return true;
            return false;
        }

        public OfficialBattleAnimatorPlayback(Animator animator, Transform parent)
        {
            this.animator = animator != null ? animator : throw new ArgumentNullException(nameof(animator));
            originalController = animator.runtimeAnimatorController;
            root = new GameObject("OfficialBattleAnimatorPlayables");
            root.transform.SetParent(parent, false);
            controller = root.AddComponent<AnimationController>();
            controller.myAnimator = animator;
            controller.NotCreateGraph = true;
            controller.CreateOverrideController = true;
            controller.Init();
            ownedController = controller.RuntimeAnimatorController;
        }

        public void Play(OfficialBattleSkillPlayableCue cue, AnimationClip clip)
        {
            if (disposed || !Supports(animator, cue)) return;
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
            playable.TryPlay();
        }

        public void Tick(float deltaTime)
        {
            if (disposed || deltaTime <= 0f) return;
            center.Update(deltaTime);
            center.LateUpdate();
            for (int index = active.Count - 1; index >= 0; index--)
                if (active[index] == null || active[index].myStatus == MyPlayable.PlayableStatus.Stop)
                {
                    if (active[index] != null) Destroy(active[index].gameObject);
                    active.RemoveAt(index);
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
            center.RemoveAll();
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            if (animator != null && animator.runtimeAnimatorController == ownedController)
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
