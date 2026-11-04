using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleAnimationPlayables;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunStatusPause()
        {
            RunScenePlayerAnimator(); SessionState.SetBool(Key + ".statusPause", true);
        }

        private static IEnumerator CheckStatusPause(PlayerAnimatorAppearance player)
        {
            var actor = player.gameObject; var animator = player.Animator;
            var controller = player.AnimController;
            var renderer = actor.GetComponent<ProfessionNativeSceneRenderer>();
            var parts = actor.GetComponent<AppearanceRendererPartHandler>();
            var weaponRoot = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
            var weapon = weaponRoot.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            var sampler = new ProfessionNativeAnimatorPose(animator, (poseSkeleton, poseChannels, poseTime, poseLoop) =>
                ProfessionNativeConstraintPose.EvaluateBeforePhysics(poseSkeleton, poseChannels, poseTime, poseLoop));
            var root = new GameObject("Original status animation pause graph scope");
            var center = new MyPlayableCenter(); var factory = new StatusMaterialFactory(center);
            var previousFactory = StoryPlayableInstantiation.Resolve; StoryPlayableInstantiation.Resolve = _ => factory;
            var manager = new MyPlayableManager { ScopeRoot = root, MyPlayableCenter = center,
                InstanceFactory = factory, LowMemory = new Zjcs.Official.StoryPlayables.GAction() };
            var attachment = actor.AddComponent<AttachAgent>(); attachment.AddBoneNode("body", actor.transform);
            var prefab = Resources.Load<GameObject>("Combat/StatusAnimations/Prefabs/EcEntity/Fight/FightSkill/24390/playable24390");
            Require(prefab, "complete native 24390 pause graph resource");
            var config = prefab.GetComponentInChildren<MyPlayableAnimationControllerPause>(true);
            Require(config && config.Duration == -1 && config.ResumeOnDestroy, "original pause leaf serialized fields");
            string label = "status-pause-" + renderer.State.Source.Sex + "-" + animator.runtimeAnimatorController.name;
            float originalSpeed = animator.speed;
            void Draw() { renderer.LateUpdateMesh(); weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh(); }
            Matrix4x4[] Pose() => Enumerable.Range(0, renderer.State.Pose.Count).Select(renderer.State.Pose.World).ToArray();
            IEnumerator Pump() { for (int i = 0; i < 4; i++) { yield return null; center.Update(.05f); center.LateUpdate(); } }
            try
            {
                manager.Initialize();
                animator.Play("Idle", 0, 0); animator.Update(0); animator.speed = .75f; animator.Update(.2f);
                sampler.ApplyUnmixed(renderer.State); Draw();
                var bounds = SceneCharacterBounds(renderer, weapon);
                CaptureSceneRendererCamera(bounds, label + "-visibility"); Draw();
                var baseline = CaptureSceneRendererCamera(bounds, label + "-before");
                var pose = Pose(); float clock = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                var first = manager.Play(prefab, actor, actor);
                var firstLeaf = first.GetComponentInChildren<MyPlayableAnimationControllerPause>(true);
                Require(firstLeaf.myApplyComponent == controller && animator.speed == 0 && Private<int>(controller, "mPauseCounter") == 1,
                    "original ApplyBySource resolves actual controller and first pause freezes Animator");
                var second = manager.Play(prefab, actor, actor);
                Require(!ReferenceEquals(first, second) && Private<int>(controller, "mPauseCounter") == 2, "independent graph leases share the original pause counter");
                yield return Pump(); animator.Update(.4f); sampler.ApplyUnmixed(renderer.State); Draw();
                Require(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - clock) < .00001f
                    && Pose().SequenceEqual(pose), "paused actual Animator clock and current native bone matrices remain fixed");
                foreach (var particle in first.GetComponentsInChildren<ParticleSystem>()) particle.Simulate(.2f, false, true, true);
                CaptureSceneRendererCamera(bounds, label + "-frozen");
                firstLeaf.Stop();
                Require(Private<int>(controller, "mPauseCounter") == 1 && animator.speed == 0, "explicit leaf Stop resumes one lease immediately");
                firstLeaf.Stop(); Require(Private<int>(controller, "mPauseCounter") == 1, "duplicate Stop cannot resume twice");
                first.Stop(true); second.Stop(true); yield return Pump();
                Require(Private<int>(controller, "mPauseCounter") == 0 && Mathf.Approximately(animator.speed, .75f), "last pause restores the saved non-default speed");
                var replay = manager.Play(prefab, actor, actor);
                Require((ReferenceEquals(replay, first) || ReferenceEquals(replay, second)) && animator.speed == 0, "same native graph pool replay resets the resume guard");
                replay.Stop(true); yield return Pump();
                Require(Mathf.Approximately(animator.speed, .75f), "pooled replay resumes exactly once");
                var timedTemplate = Object.Instantiate(prefab, root.transform); timedTemplate.SetActive(false);
                timedTemplate.GetComponentInChildren<MyPlayableAnimationControllerPause>(true).Duration = .1f;
                var timed = manager.Play(timedTemplate, actor, actor); yield return Pump();
                Require(timed.GetComponentInChildren<MyPlayableAnimationControllerPause>(true).myStatus == MyPlayable.PlayableStatus.Stop
                    && Private<int>(controller, "mPauseCounter") == 0, "natural duration completion resumes through original StopImpl");
                timed.Stop(true); yield return Pump();
                var heldTemplate = Object.Instantiate(prefab, root.transform); heldTemplate.SetActive(false);
                heldTemplate.GetComponentInChildren<MyPlayableAnimationControllerPause>(true).ResumeOnDestroy = false;
                var held = manager.Play(heldTemplate, actor, actor); held.Stop(true); yield return Pump();
                Require(animator.speed == 0 && Private<int>(controller, "mPauseCounter") == 1, "disabled resume retains the original pause lease");
                controller.ForceResumeAllPause(); Require(Mathf.Approximately(animator.speed, .75f), "controller cleanup restores all retained pauses");
                manager.RemoveAllPlayable(true); center.RemoveAll(); Object.DestroyImmediate(root);
                // Rewind to the exact original sampled clock after live frame
                // advancement, then compare the stable native rendered pose.
                animator.Play("Idle", 0, clock); animator.Update(0); sampler.ApplyUnmixed(renderer.State); Draw();
                Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-restored")), "restored original sampled native GPU pixels match exactly");
                animator.Update(.25f); sampler.ApplyUnmixed(renderer.State); Draw();
                Require(!Pose().SequenceEqual(pose), "restored Animator advances actual native bones");
                CaptureSceneRendererCamera(bounds, label + "-resumed");
                Debug.Log("[Original status pause] " + label + "; original native graph, overlapping pause leases, exact speed restoration, pool replay, natural completion, disabled resume and actual native clock/bone/GPU checks passed.");
            }
            finally
            {
                controller.ForceResumeAllPause(); animator.speed = originalSpeed;
                manager.Dispose(); center.RemoveAll(); StoryPlayableInstantiation.Resolve = previousFactory;
                if (root) Object.DestroyImmediate(root); Object.DestroyImmediate(attachment);
                animator.Play("Idle", 0, 0); animator.Update(0);
                string profession = animator.runtimeAnimatorController.name.Contains("Fashi") ? "200" : "100";
                ProfessionNativeConstraintPose.EvaluateBeforePhysics(renderer.State,
                    new ProfessionNativeAnimationChannels(renderer.State.Source, profession + "/idle_01"), .4f, false);
                Draw();
            }
        }
    }
}
