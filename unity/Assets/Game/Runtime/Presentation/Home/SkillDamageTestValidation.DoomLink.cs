using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private sealed class AcquiredDoomLink
        {
            [Serializable] private sealed class Receipt
            {
                public int primaryApplications, extraApplications, starts, invokes, ends, children, hits;
                public bool restored, statusCaptured, childCaptured;
            }
            private readonly Receipt receipt = new Receipt();
            private readonly Dictionary<int, int> remaining = new Dictionary<int, int>();
            private readonly Dictionary<int, int> primaryRemaining = new Dictionary<int, int>();
            private readonly string output;
            internal AcquiredDoomLink(string output) { this.output = output; }
            internal bool NeedsStatusCapture => receipt.extraApplications > 0 && !receipt.statusCaptured;
            internal bool NeedsChildCapture => receipt.children > 0 && !receipt.childCaptured;
            internal void CapturedStatus() { receipt.statusCaptured = true; }
            internal void CapturedChild() { receipt.childCaptured = true; }

            internal void Observe(FightBattleStepEvent step, FightBattleCheckpoint state)
            {
                var actor = state.Units.Single(unit => unit.Id == 1);
                var listener = actor.Statuses.Single(status => status.ClassId == 25073
                    && status.ApplyingSkillItemId == 10585);
                var views = step.StatusViewEvents.Where(view => view.Status.ClassId == 24472
                    && view.Status.CreatorId == actor.Id && view.Status.ApplyingSkillItemId == 10585).ToArray();
                int primary = CountApplications(step.StatusViewEvents.Where(view => view.Status.ClassId == 24474
                    && view.Status.CreatorId == actor.Id && view.Status.ApplyingSkillItemId == 10064), primaryRemaining);
                int extra = CountApplications(views, remaining);
                Require(extra <= primary, "The real additional erosion exceeded one reaction per successful primary application.");
                receipt.primaryApplications += primary;
                receipt.extraApplications += extra;
                foreach (var view in views)
                {
                    if (view.Kind == FightBattleStatusViewEventKind.Start)
                        receipt.starts++;
                    else if (view.Kind == FightBattleStatusViewEventKind.Invoke)
                        receipt.invokes++;
                    else if (view.Kind == FightBattleStatusViewEventKind.End && view.Status.RoundStartRemainingCount == 0)
                        receipt.ends++;
                }
                foreach (var target in state.Units.Where(unit => unit.Side != actor.Side))
                    foreach (var status in target.Statuses.Where(status => status.ClassId == 24472))
                        Require(status.CreatorId == actor.Id && status.ApplyingSkillItemId == 10585
                            && status.RootSkillId == 25073 && status.SourceSkillRank == listener.SourceSkillRank
                            && status.SourceSkillLevel == 2 && status.SourceSubRank == 5 && status.DurationRound == 3
                            && status.RoundTarget == 2 && status.RoundUpdateTiming == 1 && status.CanDispersed
                            && !status.IsOpenStack && status.IsAddDurationRound && status.RoundStartMaxCount == 3
                            && status.RoundStartMergeMaxCountOnStackRound
                            && remaining.TryGetValue(target.Id, out int budget)
                            && FightStatusLifecycle.RoundStartRemainingCount(status) == budget,
                            "The real Doom Link lost its passive cultivation, target clock or merged erosion budget.");
                foreach (var cast in step.SkillCasts.Where(cast => cast.ActorId == actor.Id
                    && cast.SkillItemId == 10585 && cast.SkillEntityId == 12540))
                {
                    var hits = step.DamageEvents.Where(hit => cast.DamageEventIds.Contains(hit.DamageId)).ToArray();
                    Require(cast.Slot < 0 && hits.Length == 1 && hits.All(hit => hit.SourceId == actor.Id
                        && hit.TargetId == step.ActorId && hit.SkillItemId == 10585 && hit.HitEntityId == 12541
                        && hit.DamageEntityId == 10027 && hit.ElementType == 104 && hit.DamageType == 1
                        && !hit.TrueDamage && !hit.IsCure && hit.Damage < 0
                        && hit.AfterHp == (state.DamageTest.EnableMonsterUnlimitHp ? hit.BeforeHp : hit.BeforeHp + hit.Damage)),
                        "The actual Doom Link child lost its source, target-turn dark damage or HP settlement.");
                    receipt.children++; receipt.hits += hits.Length;
                }
                Require(receipt.children == receipt.invokes, "The actual additional erosion duplicated or omitted a periodic child.");
                string saved = FightBattleCheckpointSerializer.Save(state);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                    .Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring Doom Link repeated an application, probability roll or periodic child.");
                receipt.restored = true;
                File.WriteAllText(Path.Combine(output, "companion-doom-link-checkpoint.json"), saved);
            }

            private static int CountApplications(IEnumerable<FightBattleStatusViewEvent> views,
                Dictionary<int, int> budgets)
            {
                int applications = 0;
                foreach (var view in views)
                {
                    budgets.TryGetValue(view.OwnerId, out int previous);
                    int current = view.Status.RoundStartRemainingCount;
                    if (view.Kind == FightBattleStatusViewEventKind.Start)
                    {
                        Require(current == 3, "The real erosion did not start with three invocations.");
                        applications++;
                    }
                    else if (view.Kind == FightBattleStatusViewEventKind.Update && current > previous)
                    {
                        Require(current - previous == 3, "A real erosion refresh did not add exactly three invocations.");
                        applications++;
                    }
                    // Tick/forbidden updates are view changes, not new applications.
                    if (view.Kind == FightBattleStatusViewEventKind.End) budgets.Remove(view.OwnerId);
                    else budgets[view.OwnerId] = current;
                }
                return applications;
            }

            internal void Complete()
            {
                File.WriteAllText(Path.Combine(output, "companion-doom-link-receipt.json"), JsonUtility.ToJson(receipt, true));
                Require(receipt.primaryApplications > 0 && receipt.extraApplications > 0 && receipt.starts > 0
                    && receipt.children > 0 && receipt.hits > 0 && receipt.ends > 0 && receipt.restored
                    && receipt.statusCaptured && receipt.childCaptured,
                    "Doom Link did not render, exhaust and restore its actual 60% additional erosion.");
                Debug.Log("[Acquired passive combat] 10585 cultivated additional erosion, original probability, recursion limit, dark child and restoration passed.");
            }
        }
    }
}
