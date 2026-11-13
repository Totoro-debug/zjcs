using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private string ShieldExpiryMode => Environment.GetEnvironmentVariable("ZJCS_SHIELD_EXPIRY");
        private bool ValidateShieldExpiry => !string.IsNullOrEmpty(ShieldExpiryMode);
        private int[] expiryShieldIds = Array.Empty<int>();
        private int expiryAbsorptionCount, expiryNativeCures, expiryRuleCures;
        private long expiryRemaining, expiryHealed;
        private bool expiryRestored, expiryComplete;
        private float expiryDeadline, expiryNativeClock;

        public sealed class ShieldColdSceneSnapshot
        {
            internal MyFightStatus[] Views;
            internal Transform Body;
            public string Checkpoint { get; internal set; }
        }

        [Serializable] private sealed class ShieldExpiryEvidence
        {
            public bool passed;
            public string Mode, Checkpoint;
            public long Remaining, Healed;
            public int NativeCures, RuleCures, AbsorptionCount;
        }

        private MyFightStatus[] ShieldExpiryViews() => UnityEngine.Object.FindObjectsOfType<MyFightStatus>()
            .Where(view => expiryShieldIds.Contains(view.GetComponent<MyEntity>()?.Id.EntityId ?? -1)).ToArray();

        private void ObserveShieldExpiryStep(OfficialBattleRuntimeController controller)
        {
            if (!ValidateShieldExpiry || finisherScheduled) return;
            var cp = controller.Checkpoint; var step = cp.Receipts.Last().Event;
            var owner = cp.Units.Single(unit => unit.Id == 1);
            if (expiryShieldIds.Length == 0 && step.SkillCasts.Any(cast => cast.SkillEntityId == skillId))
            {
                Require(firstPromotionItemId == 10029 && (ShieldExpiryMode == "natural" || ShieldExpiryMode == "blocked"),
                    "Shield expiry requires the isolated original 10029 absorption-to-shield chain.");
                var shields = owner.Statuses.Where(status => status.ClassId == 24435).ToArray();
                Require(shields.Length > 0 && shields.All(status => status.DurationRound == 2 && status.ShieldRemainValue > 0),
                    "Original per-hit healing did not create its two-round custom shield.");
                expiryShieldIds = shields.Select(status => status.InstanceId).ToArray();
                expiryRemaining = shields.Sum(status => status.ShieldRemainValue);
                expiryAbsorptionCount = owner.Statuses.Single(status => status.ClassId == 25023).Components.HitCustomCure.Filter.InvokeCount;
                expiryDeadline = Time.realtimeSinceStartup + 180f;
            }
            foreach (var cure in step.DamageEvents.Where(hit => hit.SkillEntityId == 12750 && hit.IsCure))
            {
                Require(expiryRestored && cure.SourceId == 1 && cure.TargetId == 1,
                    "Expiry cure fired before the full scene rebuild or used another source/target.");
                expiryRuleCures++; expiryHealed += cure.AfterHp - cure.BeforeHp;
            }
        }

        private void ObserveNativeShieldExpiry(FightBattleSkillCastEvent cast)
        {
            if (!ValidateShieldExpiry || cast.SkillEntityId != 12750 || finisherScheduled) return;
            Require(expiryRestored && cast.ActorId == 1 && cast.Slot == -1 && cast.TargetIds.SequenceEqual(new[] { 1 }),
                "Native 12750 did not bind the original non-equipped self-cure graph.");
            expiryNativeCures++;
            expiryNativeClock = Time.time;
            Debug.Log("[Shield expiry] Original 12750 native graph dispatched: " + expiryNativeCures);
        }

        public bool ReadyForShieldColdScene(OfficialBattleRuntimeController controller) => ValidateShieldExpiry
            && captured && !expiryRestored && !controller.IsPresentingStep
            && expiryShieldIds.Length > 0 && ShieldExpiryViews().Length == expiryShieldIds.Length;

        public ShieldColdSceneSnapshot CaptureShieldColdScene(OfficialBattleRuntimeController controller)
        {
            Require(ReadyForShieldColdScene(controller), "The original shield is not ready for a cold scene.");
            return new ShieldColdSceneSnapshot { Views = ShieldExpiryViews(), Body = controller.TryGetUnitTransform(1),
                Checkpoint = FightBattleCheckpointSerializer.Save(controller.Checkpoint) };
        }

        public void BindShieldColdScene(OfficialBattleRuntimeController controller, ShieldColdSceneSnapshot saved)
        {
            Require(!saved.Body && saved.Views.All(view => !view), "Old shield views or model survived host destruction.");
            Require(FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Shield cold resume changed the checkpoint or replayed the absorption skill.");
            var body = controller.TryGetUnitTransform(1); var views = ShieldExpiryViews();
            Require(body && views.Length == expiryShieldIds.Length && views.All(view =>
                view.Context.Owner == body.gameObject && !view.GetComponent<MyEntity>().myNewEntity),
                "Restored original shield lost its instance, owner or non-fresh binding.");
            expiryRestored = true; BindPresentation(controller);
        }

        public bool ShieldColdSceneVisible(OfficialBattleRuntimeController controller)
        {
            var body = controller.TryGetUnitTransform(1);
            return body && body.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.enabled && renderer.isVisible)
                && ShieldExpiryViews().Length == expiryShieldIds.Length;
        }

        public void ArmShieldExpiryFilter(OfficialBattleRuntimeController controller)
        {
            if (ShieldExpiryMode != "blocked") return;
            var owner = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            var blocked = catalog.CreateStatus(24223, 1, 1, 990024223, owner.SkillState.Round, 0, 2, 20, owner.SubRank);
            // Isolate interception at the expiry boundary without changing the shield's original duration.
            blocked.DurationRound = -1;
            owner.Statuses.Add(blocked);
        }

        private bool ObserveShieldExpiryFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateShieldExpiry || !captured || expiryComplete) return false;
            Require(Time.realtimeSinceStartup < expiryDeadline, "Restored original shield expiry did not finish: native="
                + expiryNativeCures + "; rule=" + expiryRuleCures + "; views=" + ShieldExpiryViews().Length);
            // Empty subsequent turns may overlap every sampled frame. Observe the
            // actual original cure dispatch and status retirement independently.
            if (!expiryRestored || expiryNativeCures < expiryShieldIds.Length
                || Time.time - expiryNativeClock < .3f || ShieldExpiryViews().Length > 0) return true;
            var owner = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            Require(expiryNativeCures == expiryShieldIds.Length && expiryRuleCures == expiryShieldIds.Length
                && !owner.Statuses.Any(status => expiryShieldIds.Contains(status.InstanceId)) && ShieldExpiryViews().Length == 0
                && owner.EndedShieldExpiryStatuses.Count == 0,
                "Original shield did not retire or its expiry cure was lost/duplicated.");
            Require(ShieldExpiryMode == "blocked" ? expiryHealed == 0 : expiryHealed > 0,
                "Original expiry healing did not pass through the cure interception chain.");
            Require(owner.Statuses.Single(status => status.ClassId == 25023).Components.HitCustomCure.Filter.InvokeCount == expiryAbsorptionCount
                && owner.Statuses.Any(status => status.ClassId == 25136) && !owner.Statuses.Any(status => status.ClassId == 24435),
                "Resume replayed the passive or 12750 incorrectly reconverted healing to another shield.");
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "shield-expiry.json"),
                JsonUtility.ToJson(new ShieldExpiryEvidence { passed = true, Mode = ShieldExpiryMode,
                    Remaining = expiryRemaining, Healed = expiryHealed, NativeCures = expiryNativeCures,
                    RuleCures = expiryRuleCures, AbsorptionCount = expiryAbsorptionCount,
                    Checkpoint = FightBattleCheckpointSerializer.Save(controller.Checkpoint) }, true));
            expiryComplete = true; CaptureName = "shield-expiry-" + ShieldExpiryMode;
            Debug.Log("[Shield expiry] Original 24435 cold scene, owner-round expiry, 12750 native self-cure, interception and retirement passed: " + ShieldExpiryMode);
            return true;
        }

        private void VerifyShieldExpiry()
        {
            if (ValidateShieldExpiry) Require(expiryComplete && expiryRestored, "Original shield cold scene and expiry were not observed.");
        }
    }
}
