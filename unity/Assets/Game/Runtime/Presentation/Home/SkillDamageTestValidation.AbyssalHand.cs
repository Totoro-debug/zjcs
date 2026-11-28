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
        private sealed class AcquiredAbyssalHand
        {
            [Serializable] private sealed class Receipt
            {
                public int casts, hits, slowStarts, slowEnds, affectedTargets;
                public bool restored, captured;
            }
            private readonly Receipt receipt = new Receipt();
            private readonly HashSet<int> affected = new HashSet<int>();
            private readonly string output;
            private float maximumDeltaTime;

            internal AcquiredAbyssalHand(string output) { this.output = output; }

            internal void Prepare(FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                SetAcquiredBattleProperty(actor, "EffectRate", 1000000);
                maximumDeltaTime = Time.maximumDeltaTime;
                Time.maximumDeltaTime = Mathf.Min(maximumDeltaTime, 1f / 60f);
            }

            internal void Observe(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                receipt.slowStarts += step.StatusViewEvents.Count(view => view.Status.ClassId == 24150
                    && view.Status.CreatorId == actor.Id && view.Status.ApplyingSkillItemId == 10040
                    && view.Kind == FightBattleStatusViewEventKind.Start);
                receipt.slowEnds += step.StatusViewEvents.Count(view => view.Status.ClassId == 24150
                    && view.Status.CreatorId == actor.Id && view.Status.ApplyingSkillItemId == 10040
                    && view.Kind == FightBattleStatusViewEventKind.End);
                foreach (var unit in checkpoint.Units.Where(unit => unit.Side != actor.Side))
                {
                    var slow = unit.Statuses.SingleOrDefault(status => status.ClassId == 24150);
                    if (slow == null) continue;
                    Require(slow.CreatorId == actor.Id && slow.ApplyingSkillItemId == 10040
                        && slow.SourceSkillLevel == 2 && slow.SourceSubRank == 5 && slow.DurationRound == 2
                        && slow.RoundTarget == 2 && slow.RoundUpdateTiming == 1 && slow.CanDispersed
                        && !slow.IsOpenStack && slow.StatusProperties.Single(property => property.Id == 253).Value == -1500,
                        "The actual slow lost its source, cultivation, two target turns or original 15% speed scale.");
                    long speed = unit.Properties.Single(property => property.Id == 4).Value;
                    var control = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                    var plain = control.Units.Single(value => value.Id == unit.Id);
                    plain.Statuses.RemoveAll(status => status.InstanceId == slow.InstanceId);
                    var assets = OfficialCombatAssetCatalog.LoadDefault();
                    FightStatusPropertyRuntime.RebuildAll(control.Units, assets.Load(), assets.LoadDamageFormula());
                    Require(speed < plain.Properties.Single(property => property.Id == 4).Value,
                        "Removing the actual original slow did not restore the target's final speed.");
                    affected.Add(unit.Id);
                }
                foreach (var cast in step.SkillCasts.Where(value => value.ActorId == actor.Id
                    && value.SkillItemId == 10040 && value.SkillEntityId == 10950 && value.Slot == 0))
                {
                    var hits = step.DamageEvents.Where(hit => cast.DamageEventIds.Contains(hit.DamageId)).ToArray();
                    Require(hits.Length > 0 && cast.HitSelections.Count == 1
                        && cast.HitSelections[0].HitEntityId == 10951
                        && hits.All(hit => hit.SourceId == actor.Id && hit.TargetId != actor.Id
                            && hit.SkillItemId == 10040 && hit.HitEntityId == 10951 && hit.DamageEntityId == 10952
                            && hit.DamageType == 1 && hit.ElementType == 104 && !hit.TrueDamage && !hit.IsCure
                            && hit.Damage < 0 && hit.AfterHp == (checkpoint.DamageTest.EnableMonsterUnlimitHp
                                ? hit.BeforeHp : hit.BeforeHp + hit.Damage)
                            && hit.HasDamagePosition && Math.Abs(hit.Delay - cast.Delay - 1f) < .0001f),
                        "The real cultivated area attack lost its authored impact time, dark damage or private status-bearing damage entity.");
                    receipt.casts++; receipt.hits += hits.Length;
                }
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                    .Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring Abyssal Hand repeated the attack or changed the remaining slow duration.");
                receipt.restored = true;
                File.WriteAllText(Path.Combine(output, "acquired-abyssal-hand-checkpoint.json"), saved);
            }

            internal void Complete(bool captured)
            {
                Time.maximumDeltaTime = maximumDeltaTime;
                receipt.captured = captured; receipt.affectedTargets = affected.Count;
                File.WriteAllText(Path.Combine(output, "acquired-abyssal-hand-receipt.json"), JsonUtility.ToJson(receipt, true));
                Require(receipt.casts > 0 && receipt.hits > 0 && receipt.slowStarts > 0 && receipt.slowEnds > 0
                    && receipt.affectedTargets > 0 && receipt.restored && receipt.captured,
                    "Abyssal Hand must render its real area hit, apply and expire the original slow, and restore settled combat.");
                Debug.Log("[Acquired active combat] 10040 cultivated dark area damage, real speed reduction, target-turn expiry, native performance and restoration passed.");
            }
        }
    }
}
