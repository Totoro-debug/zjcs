using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        [Serializable] private sealed class StatusProbeCatalog { public StatusProbeEntry[] graphs; }
        [Serializable] private sealed class StatusProbeEntry { public int id; public string resource; }
        private bool ValidateFirstPromotionStatusViews => Environment.GetEnvironmentVariable("ZJCS_FIRST_PROMOTION_STATUS_VIEWS") == "1";
        private OfficialBattleStatusPlayback statusViewProbe;
        private GameObject statusViewProbeRoot;
        private StatusProbeEntry[] statusViewProbeEntries;
        private FightBattleStatusViewState statusViewProbeState;
        private int statusViewProbeIndex, statusViewProbePhase;
        private float statusViewProbeClock;
        private bool statusViewProbeComplete;

        // Isolated view binding probe. Skill application/settlement is verified by
        // rule tests and the real dark-hit scenario before entering this probe.
        private bool ObserveFirstPromotionStatusViews(OfficialBattleRuntimeController controller)
        {
            if (!ValidateFirstPromotionStatusViews || statusViewProbeComplete) return false;
            if (statusViewProbe == null)
            {
                var entries = JsonUtility.FromJson<StatusProbeCatalog>(Resources.Load<TextAsset>("Combat/first-promotion-statuses").text).graphs;
                Require(entries.Length == 83 && entries.Select(value => value.id).Distinct().Count() == 83,
                    "Original first-promotion status graph closure changed.");
                foreach (var entry in entries)
                {
                    var prefab = Resources.Load<GameObject>(entry.resource);
                    Require(prefab && prefab.GetComponent<MyEntity>()
                        && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(value => value),
                        "Original status hierarchy contains a missing component: " + entry.id);
                }
                // The two grid statuses and active-seconds statuses retain their
                // existing grid/airborne hosts and are not character-view probes.
                statusViewProbeEntries = entries.Where(value => value.id != 11582 && value.id != 11632
                    && catalog.CreateStatus(value.id, 1, 1, value.id, 10810).Components?.Seconds?.DefinitionResolved != true).ToArray();
                var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                    .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                statusViewProbeRoot = new GameObject("Isolated original status binding probe");
                var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(
                    JsonUtility.FromJson<FightBattleCheckpoint>(JsonUtility.ToJson(controller.Checkpoint)));
                statusViewProbe = new OfficialBattleStatusPlayback(executor, statusViewProbeRoot.transform,
                    graph.Scope, controller.TryGetUnitTransform, _ => true, "Combat/first-promotion-statuses");
                statusViewProbe.Restore(new FightBattleCheckpoint());
            }
            if (statusViewProbeIndex == statusViewProbeEntries.Length)
            {
                statusViewProbe.Dispose(); UnityEngine.Object.Destroy(statusViewProbeRoot);
                statusViewProbeComplete = true;
                Debug.Log("[Status views] All 83 original prefab hierarchies bound; " + statusViewProbeEntries.Length
                    + " character statuses passed native Start/Update/End. Grid/seconds retain their dedicated hosts.");
                return false;
            }
            int id = statusViewProbeEntries[statusViewProbeIndex].id;
            if (statusViewProbePhase == 0)
            {
                var status = catalog.CreateStatus(id, darkStatusOwner, darkStatusOwner, id, 10810);
                FightStatusLifecycle.Initialize(status, 0);
                statusViewProbeState = FightBattleStatusViewState.Capture(status);
                statusViewProbeState.ShieldMaximum = statusViewProbeState.ShieldRemaining = 100;
                DispatchStatusProbe(FightBattleStatusViewEventKind.Start);
                Require(statusViewProbe.Count == 1, "Original status entity did not start: " + id);
                statusViewProbeClock = Time.time; statusViewProbePhase = 1;
            }
            float elapsed = Time.time - statusViewProbeClock;
            if (statusViewProbePhase == 1 && elapsed >= .35f)
            { CaptureName = "original-status-" + id + "-start"; statusViewProbePhase = 2; return true; }
            if (statusViewProbePhase == 2 && elapsed >= .55f)
            {
                statusViewProbeState = JsonUtility.FromJson<FightBattleStatusViewState>(JsonUtility.ToJson(statusViewProbeState));
                statusViewProbeState.ShieldRemaining = 0;
                statusViewProbeState.AbsoluteTriggerCount = 1;
                statusViewProbeState.StackCount++;
                if (statusViewProbeState.RoundStartRemainingCount > 0) statusViewProbeState.RoundStartRemainingCount--;
                DispatchStatusProbe(FightBattleStatusViewEventKind.Update); statusViewProbePhase = 3;
            }
            if (statusViewProbePhase == 3 && elapsed >= .8f)
            {
                CaptureName = "original-status-" + id + "-update";
                statusViewProbePhase = 4; return true;
            }
            if (statusViewProbePhase == 4 && elapsed >= .95f)
            {
                DispatchStatusProbe(FightBattleStatusViewEventKind.End);
                Require(statusViewProbe.Count == 0, "Original status entity remained after End: " + id);
                statusViewProbePhase = 5;
            }
            if (statusViewProbePhase == 5 && elapsed >= 1.2f)
            { statusViewProbeIndex++; statusViewProbePhase = 0; }
            return true;
        }

        private void DispatchStatusProbe(FightBattleStatusViewEventKind kind)
        {
            var step = new FightBattleStepEvent();
            step.StatusViewEvents.Add(new FightBattleStatusViewEvent {
                Kind = kind, OwnerId = darkStatusOwner, Status = statusViewProbeState });
            statusViewProbe.Enqueue(step); statusViewProbe.DispatchDue();
        }
    }
}
