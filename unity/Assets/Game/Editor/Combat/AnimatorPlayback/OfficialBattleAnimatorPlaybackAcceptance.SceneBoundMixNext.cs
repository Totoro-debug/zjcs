using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneBoundMixNext()
        {
            RunSceneMixNext(); SessionState.SetBool(Key + ".sceneBoundMixNext", true);
        }

        private readonly struct SceneMixNotice
        {
            internal readonly object Animation;
            internal readonly string Name;
            internal readonly float Weight, Time, Previous;
            internal readonly int Frame;
            internal SceneMixNotice(object animation, string name, float weight, float time, float previous)
            { Animation = animation; Name = name; Weight = weight; Time = time; Previous = previous; Frame = UnityEngine.Time.frameCount; }
        }

        private static void BindSceneMixNext(ProfessionNativeSceneRenderer renderer, Animator animator) =>
            renderer.BindAnimatorPose(new ProfessionNativeAnimatorPose(animator,
                (poseSkeleton, poseChannels, poseTime, poseLoop) =>
                    ProfessionNativeConstraintPose.EvaluateBeforePhysics(poseSkeleton, poseChannels, poseTime, poseLoop), SolveSceneMixNext));

        private static void ResetSceneMixAnimator(Animator animator)
        {
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
            animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0);
        }

        // Only actual renderer LateUpdate drives the pose between the trigger
        // and the observation. The camera below reads already drawn geometry.
        private static IEnumerator ObserveSceneBoundMixNext(ProfessionNativeSceneRenderer renderer,
            Animator animator, string suffix)
        {
            float speed = animator.speed;
            var callerTransform = renderer.transform.localToWorldMatrix;
            var notices = new List<SceneMixNotice>();
            var parts = renderer.GetComponent<AppearanceRendererPartHandler>();
            var weaponRoot = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
            var weapon = weaponRoot.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            string label = "bound-mix-live-" + renderer.State.Source.Sex + "-" + animator.runtimeAnimatorController.name + suffix;
            ProfessionPartClipApplied applied = (animation, name, layer, weight, time, previous, backward) =>
            {
                Require(layer == 0 && !backward && animation is ProfessionNativeAnimationChannels channels && channels.Name == name,
                    "live transition notification retains native identity and original motion name");
                notices.Add(new SceneMixNotice(animation, name, weight, time, previous));
            };
            try
            {
                BindSceneMixNext(renderer, animator); ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh();
                CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label + "-visibility");
                weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh();
                var before = Enumerable.Range(0, renderer.State.Pose.Count).Select(renderer.State.Pose.World).ToArray();
                renderer.OnClipApplied += applied;
                animator.speed = .1f;
                animator.CrossFadeInFixedTime("Damage", .2f, 0, 0);
                int[] Incoming() => Enumerable.Range(0, notices.Count)
                    .Where(i => notices[i].Name.EndsWith("/hit_01", StringComparison.Ordinal) && notices[i].Weight > 0 && notices[i].Weight < 1).ToArray();
                for (int frame = 0; Incoming().Length < 2 && frame < 180; frame++) yield return null;
                var targets = Incoming();
                Require(targets.Length >= 2, "actual Play Mode LateUpdate consumes at least two live mixed frames");
                var first = notices[targets[0]]; var last = notices[targets[targets.Length - 1]];
                foreach (int index in targets)
                {
                    Require(index > 0 && notices[index - 1].Name.EndsWith("/idle_01", StringComparison.Ordinal)
                        && notices[index - 1].Weight == 1 && notices[index - 1].Frame == notices[index].Frame,
                        "actual LateUpdate emits fully applied outgoing then weighted incoming motion in the same frame");
                }
                Require(last.Frame > first.Frame && last.Weight > first.Weight && last.Time > first.Time
                    && last.Previous == notices[targets[targets.Length - 2]].Time,
                    "live transition advances actual target clock and weight with per-motion input history");
                Require(before.Where((matrix, i) => matrix != renderer.State.Pose.World(i)).Any(),
                    "live renderer changes original body matrices without a manual pose or mesh call");
                weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh();
                AssertSceneMixPortrait(CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label + "-mixed"));
                Require(renderer.transform.localToWorldMatrix == callerTransform,
                    "live transition preserves caller model scale and blood-bar anchor transform");
                Debug.Log("[Native bound MixNext live] " + label + ": actual renderer LateUpdate, ordered source/target notifications, advancing times/weights and complete portrait passed.");
            }
            finally
            {
                renderer.OnClipApplied -= applied; animator.speed = speed;
                ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh();
            }
        }

        private static void CheckSceneBoundMixNext(PlayerAnimatorAppearance player)
        {
            var renderer = player.GetComponent<ProfessionNativeSceneRenderer>(); var animator = player.Animator;
            var parts = player.GetComponent<AppearanceMecanimPartHandler>();
            var original = parts.LoadedSkeletonDict[SkinType.pWeapon];
            var observed = new SceneObservedPart(original.Part);
            var weapon = original.Part.GameObject.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            var notices = new List<SceneMixNotice>();
            var callerTransform = renderer.transform.localToWorldMatrix;
            string label = "bound-mix-entry-" + renderer.State.Source.Sex + "-" + animator.runtimeAnimatorController.name
                + (player.AnimController ? "-controller" : "-direct");
            ProfessionPartClipApplied applied = (animation, name, layer, weight, time, previous, backward) =>
            {
                Require(layer == 0 && !backward, "explicit mixed entry uses the actual original base layer");
                notices.Add(new SceneMixNotice(animation, name, weight, time, previous));
            };
            void DrawParts()
            {
                original.Part.GameObject.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh();
            }
            Matrix4x4[] Pose() => Enumerable.Range(0, renderer.State.Pose.Count).Select(renderer.State.Pose.World).ToArray();
            void CheckPair(ProfessionNativeAnimatorPose.AppliedFrame frame)
            {
                Require(frame.Incoming.HasValue && notices.Count == 2 && notices[0].Weight == 1
                    && notices[1].Weight == frame.IncomingWeight && ReferenceEquals(notices[0].Animation, frame.Current.Animation)
                    && ReferenceEquals(notices[1].Animation, frame.Incoming.Value.Animation)
                    && notices[0].Time == frame.Current.MotionTime && notices[1].Time == frame.Incoming.Value.MotionTime,
                    "single drawing interface returns and notifies both actually applied original motions in order");
            }
            try
            {
                BindSceneMixNext(renderer, animator); ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh();
                parts.LoadedSkeletonDict[SkinType.pWeapon] = new AppearanceSkeletonPartEntry(observed, original.PoolEntry);
                parts.SetSkins(player.AppearanceRuntimeData.SkinMap);
                renderer.OnClipApplied += applied;
                animator.CrossFadeInFixedTime("Damage", .2f, 0, 0); animator.Update(.03f);
                var first = renderer.ApplyAnimatorPose(); CheckPair(first); renderer.LateUpdateMesh(); DrawParts();
                Require(notices[1].Previous == -1 && observed.Calls == 1 && observed.Last == first.Incoming.Value.Animation.Name,
                    "first incoming native identity reaches actual original part handler and part exactly once");
                CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label + "-visibility"); DrawParts();
                var bounds = SceneCharacterBounds(renderer, weapon); bounds.Expand(bounds.size * .3f);
                var early = CaptureSceneRendererCamera(bounds, label + "-early"); AssertSceneMixPortrait(early);
                var firstPose = Pose();
                notices.Clear(); renderer.UpdateAnimationAndMesh();
                Require(notices.Count == 2 && observed.Calls == 3 && firstPose.SequenceEqual(Pose())
                    && notices[0].Previous == first.Current.MotionTime && notices[1].Previous == first.Incoming.Value.MotionTime,
                    "repeated mixed drawing retains pose/history and preserves original alternating-identity part notifications");
                notices.Clear(); animator.Update(.05f); renderer.UpdateAnimationAndMesh(); DrawParts();
                Require(notices.Count == 2 && notices[1].Weight > first.IncomingWeight && observed.Calls == 5
                    && notices[0].Time > first.Current.MotionTime && notices[1].Time > first.Incoming.Value.MotionTime
                    && notices[0].Previous == first.Current.MotionTime && notices[1].Previous == first.Incoming.Value.MotionTime
                    && !firstPose.SequenceEqual(Pose()), "animation-and-mesh consumes both advancing clocks and updates the original part handler");
                var middle = CaptureSceneRendererCamera(bounds, label + "-middle"); AssertSceneMixPortrait(middle);
                Require(!early.SequenceEqual(middle), "unified mixed drawing changes actual original GPU body pixels");

                float lastHitTime = notices[1].Time;
                // Complete the actual .2-second transition. Play followed by a
                // zero-time Update during a transition can expose stale clip
                // weights, which the strict pose producer must still reject.
                notices.Clear(); animator.Update(.13f); animator.Update(0);
                Require(!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("Damage"),
                    "actual original transition has completed into its unmixed target state");
                var unmixed = renderer.ApplyAnimatorPose();
                Require(!unmixed.Incoming.HasValue && notices.Count == 1 && notices[0].Weight == 1
                    && notices[0].Previous == lastHitTime && observed.Calls == 5,
                    "one interface returns to unmixed drawing and retains the actual motion history without stale next clips");
                parts.enabled = false; ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh();
                Require(observed.Calls == 5, "disabled original part handler unsubscribes during native drawing");
                parts.enabled = true; renderer.UpdateAnimationAndMesh(); renderer.UpdateAnimationAndMesh();
                Require(observed.Calls == 6, "original handler re-subscribes once and deduplicates an unchanged unmixed identity");

                renderer.BindAnimatorPose(new ProfessionNativeAnimatorPose(animator, (poseSkeleton, poseChannels, poseTime, poseLoop) =>
                    ProfessionNativeConstraintPose.EvaluateBeforePhysics(poseSkeleton, poseChannels, poseTime, poseLoop)));
                animator.CrossFadeInFixedTime("Damage", .2f, 0, 0); animator.Update(.03f);
                notices.Clear(); var saved = Pose(); bool rejected = false;
                try { renderer.ApplyAnimatorPose(); } catch (NotSupportedException) { rejected = true; }
                Require(rejected && notices.Count == 0 && saved.SequenceEqual(Pose()),
                    "unbound mixed solver rejects through the unified interface before pose mutation or part notification");
                BindSceneMixNext(renderer, animator); var rebound = renderer.ApplyAnimatorPose(); CheckPair(rebound);
                Require(renderer.transform.localToWorldMatrix == callerTransform,
                    "mixed and unmixed drawing preserve caller model scale and blood-bar anchor transform");
                Debug.Log("[Native bound MixNext entry] " + label + ": unified mixed/unmixed drawing, original part ordering/deduplication/lifecycle, per-motion history, explicit solver and original GPU pixels passed. No Game-to-weapon animation mapping was invented.");
            }
            finally
            {
                renderer.OnClipApplied -= applied; parts.enabled = true;
                parts.LoadedSkeletonDict[SkinType.pWeapon] = original; parts.SetSkins(player.AppearanceRuntimeData.SkinMap);
                BindSceneMixNext(renderer, animator); ResetSceneMixAnimator(animator); renderer.UpdateAnimationAndMesh(); DrawParts();
            }
        }

        private static void AssertSceneMixPortrait(byte[] pixels)
        {
            Require(pixels.Length == 675 * 1463 * 4, "complete original portrait render dimensions");
            for (int x = 0; x < 675; x++)
                Require(pixels[x * 4 + 3] == 0 && pixels[((1463 - 1) * 675 + x) * 4 + 3] == 0,
                    "body and weapon fit complete portrait vertical edges");
            for (int y = 0; y < 1463; y++)
                Require(pixels[(y * 675) * 4 + 3] == 0 && pixels[(y * 675 + 674) * 4 + 3] == 0,
                    "body and weapon fit complete portrait horizontal edges");
        }
    }
}
