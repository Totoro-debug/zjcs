using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private sealed class AcquiredManaBurst
        {
            [Serializable] private sealed class Receipt
            {
                public int casts, hits, erosionStarts, erosionInvokes, erosionEnds, children, periodicHits;
                public bool restored, captured, statusCaptured, childCaptured;
            }
            private readonly Receipt receipt = new Receipt();
            private readonly string output;
            private float maximumDeltaTime;

            internal AcquiredManaBurst(string output) { this.output = output; }
            internal bool NeedsStatusCapture => receipt.erosionStarts > 0 && !receipt.statusCaptured;
            internal bool NeedsChildCapture => receipt.children > 0 && !receipt.childCaptured;
            internal void CapturedStatus() { receipt.statusCaptured = true; }
            internal void CapturedChild() { receipt.childCaptured = true; }

            internal void Prepare(FightBattleCheckpoint checkpoint)
            {
                SetAcquiredBattleProperty(checkpoint.Units.Single(unit => unit.Id == 1), "EffectRate", 1000000);
                // Isolated expiry diagnostic: allow the original first cast, then
                // prevent a fresh cast from extending the same target-turn status.
                checkpoint.Units.Single(unit => unit.Id == 1).SkillState.Slots
                    .Single(slot => slot.Slot == 0).LimitedTimes = 1;
                // Four authored body-grid hits merge twelve erosion invocations.
                // Keep the original status rules and observe their natural exhaustion.
                checkpoint.MaxRound = 16;
                if (checkpoint.IsPlayerRoundLimit) checkpoint.PlayerRoundLimit = 16;
                maximumDeltaTime = Time.maximumDeltaTime;
                Time.maximumDeltaTime = Mathf.Min(maximumDeltaTime, 1f / 60f);
            }

            internal void Observe(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                var active = actor.Skills.Single(skill => skill.Slot == 0);
                var views = step.StatusViewEvents.Where(view => view.Status.ClassId == 24474
                    && view.Status.CreatorId == actor.Id && view.Status.ApplyingSkillItemId == 10064).ToArray();
                receipt.erosionStarts += views.Count(view => view.Kind == FightBattleStatusViewEventKind.Start);
                receipt.erosionInvokes += views.Count(view => view.Kind == FightBattleStatusViewEventKind.Invoke);
                receipt.erosionEnds += views.Count(view => view.Kind == FightBattleStatusViewEventKind.End);
                foreach (var target in checkpoint.Units.Where(unit => unit.Side != actor.Side))
                    foreach (var status in target.Statuses.Where(status => status.ClassId == 24474))
                        Require(status.CreatorId == actor.Id && status.ApplyingSkillItemId == 10064
                            && status.SourceSkillRank == active.Rank && status.SourceSkillLevel == 2
                            && status.SourceSubRank == 5 && status.DurationRound == 3
                            && status.RoundTarget == 2 && status.RoundUpdateTiming == 1 && status.CanDispersed
                            && !status.IsOpenStack && status.IsAddDurationRound && status.RoundStartMaxCount == 3
                            && status.RoundStartMergeMaxCountOnStackRound
                            && FightStatusLifecycle.RoundStartRemainingCount(status) > 0,
                            "The real erosion lost inherited cultivation, target-turn duration or its merged invocation budget.");
                foreach (var cast in step.SkillCasts.Where(cast => cast.ActorId == actor.Id && cast.SkillItemId == 10064))
                {
                    if (cast.SkillEntityId != 11390 && cast.SkillEntityId != 12560) continue;
                    var hits = step.DamageEvents.Where(hit => cast.DamageEventIds.Contains(hit.DamageId)).ToArray();
                    bool periodic = cast.SkillEntityId == 12560;
                    Require(hits.Length > 0 && hits.All(hit => hit.SourceId == actor.Id && hit.TargetId != actor.Id
                        && hit.SkillItemId == 10064 && hit.HitEntityId == (periodic ? 12561 : 11391)
                        && hit.DamageEntityId == (periodic ? 10027 : 11392) && hit.ElementType == 104
                        && hit.DamageType == 1 && !hit.TrueDamage && !hit.IsCure && hit.Damage < 0
                        && hit.AfterHp == (checkpoint.DamageTest.EnableMonsterUnlimitHp
                            ? hit.BeforeHp : hit.BeforeHp + hit.Damage)),
                        "The actual area hit or erosion child lost its original source, dark damage or HP settlement.");
                    if (periodic)
                    {
                        Require(cast.Slot < 0 && hits.All(hit => hit.TargetId == step.ActorId),
                            "The erosion child must strike its actual round-start listener without consuming a skill slot.");
                        receipt.children++; receipt.periodicHits += hits.Length;
                    }
                    else
                    {
                        Require(cast.Slot == 0 && cast.HitSelections.Count == 1
                            && cast.HitSelections[0].HitEntityId == 11391 && hits.All(hit =>
                            {
                                int distance = Math.Abs(hit.HitGridX - cast.Presentation.TargetX)
                                    + Math.Abs(hit.HitGridY - cast.Presentation.TargetY);
                                return hit.HasHitGridPosition && distance <= 2
                                    && Math.Abs(hit.Delay - cast.Delay - (distance == 0 ? 1.23f
                                        : distance == 1 ? 1.28f : 1.33f)) < .0001f;
                            }), "The real area impact lost its authored center-to-outer-cell timing.");
                        receipt.casts++; receipt.hits += hits.Length;
                    }
                }
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                    .Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring Mana Burst repeated a periodic child, status refresh or invocation count.");
                receipt.restored = true;
                File.WriteAllText(Path.Combine(output, "acquired-mana-burst-checkpoint.json"), saved);
            }

            internal void Complete(bool captured)
            {
                Time.maximumDeltaTime = maximumDeltaTime;
                receipt.captured = captured;
                File.WriteAllText(Path.Combine(output, "acquired-mana-burst-receipt.json"), JsonUtility.ToJson(receipt, true));
                Require(receipt.casts > 0 && receipt.hits > 0 && receipt.erosionStarts > 0
                    && receipt.erosionInvokes > 0 && receipt.erosionEnds > 0 && receipt.children > 0
                    && receipt.periodicHits > 0 && receipt.restored && receipt.captured
                    && receipt.statusCaptured && receipt.childCaptured,
                    "Mana Burst must render its area hit, invoke and expire real erosion, and restore its settled state.");
                Debug.Log("[Acquired active combat] 10064 cultivated dark area impact, target-turn erosion child, merged duration/count, native performance and restoration passed.");
            }
        }
    }
}
