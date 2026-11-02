using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static MoveAnimatorProbe ConfigureMoveAnimatorProbe(OfficialBattleRuntimeController controller)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_MOVE_ANIMATORS_ACCEPTANCE") != "1") return null;
            VerifyMoveAnimatorOwners();
            return new MoveAnimatorProbe(controller);
        }

        private static void VerifyMoveAnimatorOwners()
        {
            var catalog = new OfficialBattleMovementAnimationCatalog();
            var step = new FightBattleStepEvent();
            var forward = new FightBattleMovementEvent
                { UnitId = 1, MoveSkillEntityId = 15300, MoveBehaviourId = 15301, MoveDuration = .3f, Delay = .2f };
            forward.Path.AddRange(new[] { new FightGridPoint(0, 0), new FightGridPoint(1, 0), new FightGridPoint(1, 1) });
            step.Movements.Add(forward);
            Func<FightGridPoint, Vector3> world = p => new Vector3(p.X * 2f, p.Y * 3f, 0f);
            var forwardCues = catalog.Build(step, world)[forward];
            Require(forwardCues.Count == 1 && forwardCues[0].SkillPlayable.trigger_param == "run"
                && Mathf.Approximately(forwardCues[0].Delay, .2f) && forwardCues[0].StopAfter < 0f,
                "Original skill-level forward trigger was replaced or repeated per cell.");
            forward.MoveSkillEntityId = 15310;
            forward.MoveBehaviourId = 15311;
            var backCues = catalog.Build(step, world)[forward];
            Require(backCues.Count == 1 && backCues[0].SkillPlayable.trigger_param == "houtui",
                "Retreat still plays the generic forward run alias.");

            forward.MoveSkillEntityId = 19810;
            forward.MoveBehaviourId = 19811;
            forward.MoveDuration = -1f;
            forward.MoveSpeed = 5f;
            var cellCues = catalog.Build(step, world)[forward];
            Require(cellCues.Count == 2 && cellCues.All(c => c.SkillPlayable.my_bool_param == "run"
                    && c.SkillPlayable.my_bool_value && c.SkillPlayable.end_resume_param)
                && Mathf.Approximately(cellCues[0].Delay, .2f) && Mathf.Approximately(cellCues[0].StopAfter, .4f)
                && Mathf.Approximately(cellCues[1].Delay, .6f) && Mathf.Approximately(cellCues[1].StopAfter, .6f),
                "Per-cell animation lifetime does not use actual world-space path distance.");
            var next = new FightBattleMovementEvent
                { UnitId = 1, MoveSkillEntityId = 15300, MoveBehaviourId = 15301, MoveDuration = .3f };
            next.Path.AddRange(new[] { new FightGridPoint(1, 1), new FightGridPoint(2, 1) });
            step.Movements.Add(next);
            Require(Mathf.Approximately(catalog.Build(step, world)[next][0].Delay, 1.2f),
                "A second movement overlaps the preceding path rather than preserving scene order.");

            var root = new GameObject("OriginalMovementAnimatorOwners");
            try
            {
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(
                    "Combat/GameAnimators/Anim/Male_Zhanshi_Controller");
                Require(animator.runtimeAnimatorController != null, "Original player game controller is missing.");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
                using (var playback = new OfficialBattleAnimatorPlayback(animator, root.transform))
                {
                    // This original movement action owns an infinite bool playable.
                    forward.MoveSkillEntityId = 15320;
                    forward.MoveBehaviourId = 15321;
                    OfficialBattleSkillPlayableCue running = catalog.Build(step, world)[forward][0].SkillPlayable;
                    Require(OfficialBattleAnimatorPlayback.Supports(animator, running),
                        "Original fixed-speed movement parameters do not bind to the original controller.");
                    playback.Play(running, null, .2f);
                    Require(animator.GetBool("run_03"), "The original action bool was not applied.");
                    playback.Tick(0f);
                    playback.Tick(.19f);
                    Require(animator.GetBool("run_03"), "A paused or incomplete cell stopped its action early.");
                    playback.Tick(.02f);
                    Require(!animator.GetBool("run_03"), "An infinite cell action leaked its run bool past the boundary.");
                    playback.Play(running, null, .4f);
                    Require(animator.GetBool("run_03"), "The next cell did not restart its original action.");
                    playback.Stop();
                    Require(!animator.GetBool("run_03"), "Cancellation failed to restore the original parameter.");
                    foreach (var cue in new[] { forwardCues[0], backCues[0] })
                    {
                        Require(OfficialBattleAnimatorPlayback.Supports(animator, cue.SkillPlayable),
                            "Original forward/back trigger is unsupported by its game controller.");
                        playback.Play(cue.SkillPlayable, null);
                        animator.Update(.05f);
                        playback.Tick(.05f);
                    }
                }
            }
            finally { UnityEngine.Object.Destroy(root); }
            Debug.Log("[Move animators] original forward/back triggers, per-cell owners, world distance, pause, restart and parameter restoration passed. Visible body motion is a separate pending binding.");
        }

        private sealed class MoveAnimatorProbe
        {
            private readonly OfficialBattleRuntimeController controller;
            private int cues;
            private readonly HashSet<string> triggers = new HashSet<string>();
            public MoveAnimatorProbe(OfficialBattleRuntimeController controller) { this.controller = controller; }
            public void Observe()
            {
                var scene = typeof(OfficialBattleRuntimeController).GetField("sceneView", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                var pending = (IEnumerable)typeof(OfficialBattleSceneView).GetField("pendingUnitAnimations", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(scene);
                foreach (object value in pending)
                {
                    var cue = (OfficialBattleAnimationCue)value.GetType().GetField("Cue").GetValue(value);
                    if (cue.SkillPlayable == null) continue;
                    string trigger = cue.SkillPlayable.trigger_param;
                    if (trigger != "run" && trigger != "houtui") continue;
                    Require(cue.Movement == null && string.IsNullOrEmpty(cue.Animation),
                        "Actual movement was replaced by a run alias after original cue creation.");
                    cues++;
                    triggers.Add(trigger);
                }
            }
            public void Verify(string host)
            {
                Require(cues > 0 && triggers.Contains("run"), host + " never queued an original player movement trigger.");
                Debug.Log("[Move animators] " + host + " actual queued original triggers=" + cues
                    + " kinds=" + string.Join(",", triggers.OrderBy(x => x)));
            }
        }
    }
}
