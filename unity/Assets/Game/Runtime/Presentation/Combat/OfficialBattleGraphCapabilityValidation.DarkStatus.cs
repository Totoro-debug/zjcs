using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryFollowBone;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateDarkStatus => Environment.GetEnvironmentVariable("ZJCS_DARK_STATUS_LIFECYCLE_ACCEPTANCE") == "1";
        private int darkStatusOwner, darkStatusInstance;
        private float darkStatusStartClock, darkStatusEndClock = -1f;
        private bool darkStatusVisible, darkStatusRestored, darkStatusComplete;
        private readonly HashSet<int> darkStatusNativeCounts = new HashSet<int>();
        private readonly List<int> darkStatusRuleCounts = new List<int>();

        private void ConfigureDarkStatusDiagnostic(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateDarkStatus) return;
            Require(firstPromotionItemId == 10029 && Environment.GetEnvironmentVariable("ZJCS_STATUS_DARK_HIT_ACCEPTANCE") == "1",
                "The native dark-status diagnostic requires the original dark-hit route.");
            foreach (var unit in checkpoint.Units)
            {
                foreach (var prop in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                    if (prop.Id == catalog.PropertyType("Speed")) prop.Value = 1000;
                unit.Turn.Speed = 1000;
            }
            checkpoint.MaxRound = 24;
        }

        private void ObserveDarkStatusStep(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateDarkStatus || darkStatusComplete) return;
            var step = checkpoint.Receipts.Last().Event;
            foreach (var value in step.StatusViewEvents.Where(value => value.Status.ClassId == 24222))
            {
                if (value.Kind == FightBattleStatusViewEventKind.Start)
                {
                    Require(darkStatusOwner == 0 && value.Status.RoundStartRemainingCount == 3,
                        "The authored dark status did not start once with three invocations.");
                    darkStatusOwner = value.OwnerId; darkStatusInstance = value.Status.InstanceId;
                    darkStatusStartClock = Time.time + value.Delay;
                }
                if (value.Kind == FightBattleStatusViewEventKind.Update)
                    darkStatusRuleCounts.Add(value.Status.RoundStartRemainingCount);
                if (value.Kind == FightBattleStatusViewEventKind.End) darkStatusEndClock = Time.time + value.Delay;
            }
        }

        private MyFightStatus DarkStatusView() => UnityEngine.Object.FindObjectsOfType<MyFightStatus>()
            .SingleOrDefault(view => view.GetComponent<MyEntity>()?.ClassId == 24222);

        private bool ObserveDarkStatusFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateDarkStatus || darkStatusComplete || darkStatusOwner == 0) return false;
            var view = DarkStatusView();
            var body = controller.TryGetUnitTransform(darkStatusOwner);
            if (Time.time + .0001f < darkStatusStartClock)
            {
                Require(!view, "Original dark-status particles appeared before their hit.");
                return captured;
            }
            if (view)
            {
                Require(body && view.Context.Owner == body.gameObject && view.IsIconShowCustomCount
                    && view.CanShowSpriteIcon() && view.GetComponent<MyEntity>().Id.EntityId == darkStatusInstance,
                    "Original dark status lost owner, custom count, icon or entity identity.");
                var follows = UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>()
                    .Where(value => value.myTarget == body.gameObject && value.myAttach.myBoneName == "body").ToArray();
                bool rendered = follows.Any(value => value.GetComponentsInChildren<ParticleSystem>().Any(p => p.particleCount > 0)
                    && value.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible));
                if (rendered && darkStatusNativeCounts.Add(view.IconShowCustomCount))
                {
                    darkStatusVisible = true;
                    CaptureName = "dark-status-count-" + view.IconShowCustomCount;
                    return true;
                }
            }
            if (!captured) return false;
            if (darkStatusVisible && !darkStatusRestored && view && !controller.IsPresentingStep)
            {
                string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                string path = Path.Combine(output, "dark-status-native-restore.json");
                File.WriteAllText(path, JsonUtility.ToJson(controller.Checkpoint));
                var saved = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(
                    JsonUtility.FromJson<FightBattleCheckpoint>(File.ReadAllText(path))).State;
                var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                    .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                int count = view.IconShowCustomCount;
                graph.RefreshGridSnapshot(saved);
                var restored = DarkStatusView();
                Require(restored && restored != view && !restored.GetComponent<MyEntity>().myNewEntity
                    && restored.IconShowCustomCount == count,
                    "Native dark-status entity/count did not restore from the Unity disk checkpoint: present="
                    + (bool)restored + "; fresh=" + (restored ? restored.GetComponent<MyEntity>().myNewEntity.ToString() : "missing")
                    + "; before=" + count + "; after=" + (restored ? restored.IconShowCustomCount.ToString() : "missing"));
                darkStatusRestored = true; CaptureName = "dark-status-restored"; return true;
            }
            if (darkStatusEndClock >= 0f && Time.time >= darkStatusEndClock + 1f)
            {
                Require(!view && darkStatusVisible && darkStatusRestored && darkStatusRuleCounts.SequenceEqual(new[] { 2, 1, 0 })
                    && darkStatusNativeCounts.IsSupersetOf(new[] { 3, 2, 1 }),
                    "Original dark status did not finish native start/restore/countdown/end: "
                    + string.Join(",", darkStatusRuleCounts) + "; native=" + string.Join(",", darkStatusNativeCounts)
                    + "; restored=" + darkStatusRestored);
                darkStatusComplete = true; CaptureName = "dark-status-ended";
                Debug.Log("[Dark status] Original 24222 body particles, three-turn counter, native disk rebind and end passed.");
                return true;
            }
            return true; // Finish the authored status before the isolated finisher is equipped.
        }

        private void VerifyDarkStatusDiagnostic()
        { if (ValidateDarkStatus) Require(darkStatusComplete, "The original dark-status native lifecycle did not finish."); }
    }
}
