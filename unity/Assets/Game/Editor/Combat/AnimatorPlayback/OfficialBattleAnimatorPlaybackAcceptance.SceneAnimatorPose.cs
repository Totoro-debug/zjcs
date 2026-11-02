using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneAnimatorPose()
        {
            RunScenePlayerAnimator();
            SessionState.SetBool(Key + ".sceneAnimatorPose", true);
        }

        private static void CheckSceneAnimatorPose(PlayerAnimatorAppearance player)
        {
            var renderer = player.GetComponent<ProfessionNativeSceneRenderer>();
            var parts = player.GetComponent<AppearanceRendererPartHandler>();
            var weaponRoot = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
            var weapon = weaponRoot.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            var animator = player.Animator;
            var original = animator.runtimeAnimatorController;
            var sampler = new ProfessionNativeAnimatorPose(animator, ProfessionNativeConstraintPose.EvaluateBeforePhysics);
            string label = "animator-clock-" + renderer.State.Source.Sex + "-" + original.name + (player.AnimController ? "-controller" : "-direct");
            void Draw()
            {
                renderer.LateUpdateMesh();
                weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate();
                weapon.UpdateAnimationAndMesh();
            }
            Matrix4x4[] Pose() => Enumerable.Range(0, renderer.State.Pose.Count).Select(renderer.State.Pose.World).ToArray();
            void ResetGraph()
            {
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
                animator.runtimeAnimatorController = original; animator.Rebind(); animator.Update(0);
            }
            try
            {
                ResetGraph();
                animator.Play("Idle", 0, 0); animator.Update(0);
                Require(!animator.IsInTransition(0), "reset original graph is stable before native sampling");
                var start = sampler.ApplyUnmixed(renderer.State); Draw();
                CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label + "-visibility"); Draw();
                var bounds = SceneCharacterBounds(renderer, weapon);
                var before = Pose();
                var baseline = CaptureSceneRendererCamera(bounds, label + "-idle-start");
                animator.Update(.4f);
                var advanced = sampler.ApplyUnmixed(renderer.State); Draw();
                Require(advanced.Clip == start.Clip && advanced.Time > start.Time + .3f
                    && before.Where((matrix, i) => matrix != renderer.State.Pose.World(i)).Any(),
                    "real Animator clock changes actual native bone matrices");
                Require(!baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-idle-advanced")),
                    "actual Animator time changes rendered current player pixels");
                animator.Play("Idle", 0, 0); animator.Update(0); sampler.ApplyUnmixed(renderer.State); Draw();
                Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-idle-restored")),
                    "rewinding the actual graph restores exact original native pixels");
                animator.Play("Idle", 0, 1.25f); animator.Update(0);
                var looped = sampler.ApplyUnmixed(renderer.State);
                Require(looped.Loop && Mathf.Abs(looped.Time - looped.Clip.length * .25f) < .002f,
                    "actual Animator loop count is removed once at the original motion duration");
                animator.Play("Idle", 0, 0); animator.Update(0);
                animator.speed = 2; animator.Update(.1f);
                var fast = sampler.ApplyUnmixed(renderer.State);
                Require(Mathf.Abs(fast.Time - .2f) < .002f, "Animator speed is consumed once without an independent native clock");
                animator.speed = 1;
                var hit = original.animationClips.Single(clip => clip.name == start.Clip.name.Replace("idle_01", "hit_01"));
                var cue = new OfficialBattleSkillPlayableCue { kind = 1, origin_clip_name = "Skill", trigger_param = "skill",
                    my_duration = 0, end_resume_param = false, speed = 1 };
                using (var playback = new OfficialBattleAnimatorPlayback(animator, player.transform))
                {
                    playback.Play(cue, hit); animator.Update(.02f); animator.Update(.02f);
                    var hitStart = sampler.ApplyUnmixed(renderer.State); Draw();
                    Require(hitStart.Clip == hit && animator.GetCurrentAnimatorStateInfo(0).IsName("Skill"),
                        "actual playable override selects the original native hit motion by clip identity");
                    before = Pose();
                    CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label + "-override-hit-start");
                    animator.Update(.1f); var hitNext = sampler.ApplyUnmixed(renderer.State); Draw();
                    Require(hitNext.Clip == hit && hitNext.Time > hitStart.Time
                        && before.Where((matrix, i) => matrix != renderer.State.Pose.World(i)).Any(),
                        "overridden graph advances the actual native motion time and bone matrices");
                    CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label + "-override-hit-advanced");
                }
                ResetGraph(); sampler.ApplyUnmixed(renderer.State);
                animator.CrossFade("Skill", .5f, 0, 0); animator.Update(.01f);
                Require(animator.IsInTransition(0), "actual Unity transition exists for rejection probe");
                before = Pose(); bool rejected = false;
                try { sampler.ApplyUnmixed(renderer.State); } catch (NotSupportedException) { rejected = true; }
                Require(rejected && before.SequenceEqual(Pose()), "unbound mixing rejects without changing the native skeleton");
            }
            finally
            {
                animator.speed = 1; ResetGraph();
                // Existing profile lifecycle fixture still declares its explicit
                // pre-physics .4-second diagnostic baseline.
                var clip = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
                ProfessionNativeConstraintPose.EvaluateBeforePhysics(renderer.State,
                    new ProfessionNativeAnimationChannels(renderer.State.Source, clip.name), .4f, false);
                Draw();
            }
            Debug.Log("[Native Animator pose] " + label + ": real clock, native matrices/pixels, rewind, speed and transition rejection passed; solver is explicitly pre-physics.");
        }
    }
}
