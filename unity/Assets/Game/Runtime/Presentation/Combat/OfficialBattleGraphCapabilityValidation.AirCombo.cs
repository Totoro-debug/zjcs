using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateAirCombo => Environment.GetEnvironmentVariable("ZJCS_AIR_COMBO_ACCEPTANCE") == "1";
        private FightBattleExecutor airComboExecutor;
        private OfficialBattleRuntimeController airComboController;
        private readonly List<string> airComboTransitions = new List<string>();
        private int airComboTarget;
        private bool airComboAnimated, airComboAdvanced, airComboPaused, airComboResumed;
        private bool airComboSaved, airComboRecoveryCaptured, airComboEndCaptured;
        private double airComboPauseClock;
        private float airComboPauseTime, airComboPauseAnimation;
        private Vector3[] airComboPose;
        private string airComboLastAnimation;
        private bool airComboFacingChecked;

        private void ConfigureAirComboDiagnostic(int itemId, FightBattleCheckpoint checkpoint)
        {
            if (!ValidateAirCombo) return;
            Require(new[] { 10006, 10009, 10052 }.Contains(itemId),
                "The airborne diagnostic requires an original first-promotion airborne skill.");
            // Allow both original status phases to finish before the fixture's
            // separate finishing attack. Original gameplay durations are unchanged.
            firstPromotionCaptureAfter = 5f;
            if (itemId != 10006)
            {
                // Keep the short-range probes on their requested axis. The real
                // ordinary AI may otherwise choose another equally valid side.
                // 10006 retains ordinary approach movement as a separate regression.
                var source = checkpoint.Units.Single(unit => unit.Id == 1);
                foreach (var prop in source.Properties.Concat(source.BaseProperties ?? source.Properties))
                    if (prop.Id == catalog.PropertyType("MoveDist") || prop.Id == catalog.PropertyType("FixedMoveDist"))
                        prop.Value = 0;
                source.MoveDist = 0;
                source.HasFlipX = true;
                source.FlipX = Environment.GetEnvironmentVariable("ZJCS_DIRECTIONAL_SKILL_FACING") != "left";
            }
        }

        private void BindAirComboDiagnostic(OfficialBattleRuntimeController controller)
        {
            if (!ValidateAirCombo) return;
            airComboController = controller;
            airComboExecutor = (FightBattleExecutor)typeof(OfficialBattleRuntimeController)
                .GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Require(airComboExecutor != null, "The airborne diagnostic requires the real session executor.");
            airComboExecutor.StatusSecondsChanged += ObserveAirComboTransition;
        }

        private void ObserveAirComboTransition(FightBattleUnitState owner, FightBattleStatusState status, bool started)
        {
            if (airComboEndCaptured || status.ClassId != 24269 && status.ClassId != 24270) return;
            Require(owner.Id != 1 && owner.MonsterId == 3331,
                "The airborne diagnostic hit an unexpected original model.");
            airComboTarget = owner.Id;
            airComboTransitions.Add(status.ClassId + ":" + started);
            var body = airComboController.TryGetUnitTransform(owner.Id);
            var agent = body ? body.GetComponent<MyFightStatusAgent>() : null;
            Require(agent, "Original airborne status agent was not bound to the actual target.");
            var views = agent.MyStatuses.Where(view => view && view.GetComponent<MyEntity>()
                .Entity.ClassId == status.ClassId).ToArray();
            Require(started ? views.Length == 1 : views.Length == 0,
                "The original status view did not follow its real start/end notification.");
            if (!started) return;
            Require(views[0].ApplyEntity == body.gameObject && !views[0].ShowSpriteAbovePlayer,
                "The original airborne prefab changed its owner or icon binding.");
            if (status.ClassId == 24269)
            {
                Require(views[0].myPlayablePrefab && views[0].InvokePlayable == views[0].myPlayablePrefab,
                    "Original airborne start/InvokePlayable references differ.");
            }
            else Require(!views[0].myPlayablePrefab && !views[0].InvokePlayable,
                "Original recovery acquired a replacement animation graph.");
        }

        private void SaveAirComboCheckpoint(FightBattleStatusState status)
        {
            Require(status.Components.Seconds.RemainingSeconds > 0d
                && status.Components.Seconds.RemainingSeconds < status.Components.Seconds.DurationSeconds,
                "The airborne checkpoint must contain a partially consumed timer.");
            var saved = JsonUtility.FromJson<FightBattleCheckpoint>(JsonUtility.ToJson(airComboExecutor.State));
            var restored = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(saved);
            restored.AdvanceStatusSeconds(0d);
            var copy = restored.State.Units.Single(unit => unit.Id == airComboTarget).Statuses
                .Single(value => value.InstanceId == status.InstanceId);
            Require(Math.Abs(copy.Components.Seconds.RemainingSeconds - status.Components.Seconds.RemainingSeconds) < 1e-7
                && restored.State.StatusSeconds.PendingHits.Count == airComboExecutor.State.StatusSeconds.PendingHits.Count,
                "Unity checkpoint restore restarted the airborne timer or changed pending hits.");
            airComboSaved = true;
        }

        private bool ObserveAirComboDiagnostic(OfficialBattleRuntimeController controller)
        {
            if (!ValidateAirCombo || airComboEndCaptured) return false;
            Require(nativeCasts == 0 || Time.time - firstPromotionCastClock < 8f,
                "Airborne observation did not finish: transitions=" + string.Join(",", airComboTransitions)
                    + ", animated=" + airComboAnimated + ", advanced=" + airComboAdvanced
                    + ", paused=" + airComboPaused + ", resumed=" + airComboResumed);
            if (!airComboFacingChecked && nativeCasts > 0 && Time.time - firstPromotionCastClock >= .1f)
            {
                var cast = controller.Checkpoint.Receipts.SelectMany(receipt => receipt.Event.SkillCasts)
                    .Single(value => value.ActorId == 1 && value.SkillItemId == firstPromotionItemId);
                var pose = cast.Presentation;
                var source = controller.TryGetUnitTransform(1);
                var grid = source ? source.GetComponent<MyGridTransform>() : null;
                Require(grid && grid.GetVisualFlipX() == pose.FlipX,
                    "The rendered caster mirror differs from its real cast snapshot.");
                Require(!pose.FlipSource || pose.SourceX == pose.TargetX
                    || pose.FlipX == (pose.TargetX < pose.SourceX),
                    "The rendered caster faces away from its real horizontal cast target.");
                airComboFacingChecked = true;
                Debug.Log("[Cast facing] Actual native mirror=" + grid.GetVisualFlipX()
                    + "; source=" + pose.SourceX + "," + pose.SourceY
                    + "; target=" + pose.TargetX + "," + pose.TargetY);
            }
            if (airComboTarget == 0) return true;
            var target = controller.Checkpoint.Units.Single(unit => unit.Id == airComboTarget);
            var body = controller.TryGetUnitTransform(target.Id);
            var renderer = body ? body.GetComponentInChildren<BattleNativeMonsterRenderer>() : null;
            Require(renderer && renderer.Animator, "Original airborne target has no complete skeleton/Animator.");
            var meshRenderer = renderer.GetComponent<MeshRenderer>();
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            Require(meshRenderer && meshRenderer.enabled && !meshRenderer.forceRenderingOff
                && meshRenderer.isVisible && mesh && mesh.vertexCount > 0,
                "The airborne diagnostic has no actual visible target mesh.");
            var vertices = mesh.vertices;
            Require(vertices.All(value => FiniteAirCombo(value.x) && FiniteAirCombo(value.y) && FiniteAirCombo(value.z)),
                "Original airborne geometry contains a non-finite vertex.");
            var airborne = target.Statuses.FirstOrDefault(status => status.ClassId == 24269);
            if (airborne != null && airComboLastAnimation != renderer.Animation)
            {
                airComboLastAnimation = renderer.Animation;
                Debug.Log("[Air combo] Active original animation=" + renderer.Animation
                    + "; remaining=" + airborne.Components.Seconds.RemainingSeconds
                    + "; transition=" + renderer.Animator.IsInTransition(0));
            }
            if (!airComboAnimated && airborne != null && renderer.Animation == "floating_01")
            {
                airComboAnimated = true; airComboPose = vertices;
                CaptureName = "air-combo-" + firstPromotionItemId + "-floating";
                return true;
            }
            if (airComboAnimated && airborne != null && !airComboAdvanced)
            {
                airComboAdvanced = vertices.Length != airComboPose.Length
                    || vertices.Where((value, index) => (value - airComboPose[index]).sqrMagnitude > 1e-10f).Any();
                if (airComboAdvanced)
                {
                    SaveAirComboCheckpoint(airborne);
                    // This is a clock API probe; the surrounding battle still
                    // enters/settles via the original simulated UI input.
                    controller.SetPaused(true);
                    Require(controller.IsPaused, "The airborne clock probe did not enter pause.");
                    airComboPaused = true; airComboPose = vertices;
                    airComboPauseClock = controller.Checkpoint.StatusSeconds.ElapsedSeconds;
                    airComboPauseTime = Time.realtimeSinceStartup;
                    airComboPauseAnimation = renderer.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    CaptureName = "air-combo-" + firstPromotionItemId + "-paused";
                }
                return true;
            }
            if (airComboPaused && !airComboResumed)
            {
                Require(controller.IsPaused && controller.Checkpoint.StatusSeconds.ElapsedSeconds == airComboPauseClock
                    && Math.Abs(renderer.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime - airComboPauseAnimation) < 1e-6f
                    && vertices.Length == airComboPose.Length
                    && vertices.Select((value, index) => (value - airComboPose[index]).sqrMagnitude).All(value => value < 1e-10f),
                    "Pause advanced the airborne clock, original Animator or actual geometry.");
                if (Time.realtimeSinceStartup - airComboPauseTime >= .15f)
                { controller.SetPaused(false); airComboResumed = true; }
                return true;
            }
            if (!airComboRecoveryCaptured && target.Statuses.Any(status => status.ClassId == 24270))
            {
                Require(airComboAnimated && airComboAdvanced && airComboResumed,
                    "The original airborne action or pause/resume was not observed before recovery: animation="
                    + airComboLastAnimation + ", animated=" + airComboAnimated + ", advanced=" + airComboAdvanced
                    + ", resumed=" + airComboResumed);
                airComboRecoveryCaptured = true;
                CaptureName = "air-combo-" + firstPromotionItemId + "-recovery";
                return true;
            }
            if (airComboRecoveryCaptured && !target.Statuses.Any(status => status.ClassId == 24269 || status.ClassId == 24270))
            {
                Require(airComboTransitions.SequenceEqual(new[] { "24269:True", "24269:False", "24270:True", "24270:False" }),
                    "The real airborne/recovery view lifecycle differs.");
                Require(!body.GetComponent<MyFightStatusAgent>().MyStatuses.Any(view => view
                    && new[] { 24269, 24270 }.Contains(view.GetComponent<MyEntity>().Entity.ClassId)),
                    "An ended original airborne view remains registered.");
                airComboEndCaptured = true;
                CaptureName = "air-combo-" + firstPromotionItemId + "-ended";
                Debug.Log("[Air combo] Original floating_01 mesh, pause clock, Unity checkpoint and both native status lifecycles passed: "
                    + firstPromotionItemId);
            }
            return true;
        }

        private static bool FiniteAirCombo(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void VerifyAirComboDiagnostic()
        {
            if (!ValidateAirCombo) return;
            if (airComboExecutor != null) airComboExecutor.StatusSecondsChanged -= ObserveAirComboTransition;
            Require(airComboAnimated && airComboAdvanced && airComboPaused && airComboResumed
                && airComboSaved && airComboRecoveryCaptured && airComboEndCaptured && airComboFacingChecked,
                "Original airborne action, pause, checkpoint or native lifecycle was not fully observed.");
        }
    }
}
