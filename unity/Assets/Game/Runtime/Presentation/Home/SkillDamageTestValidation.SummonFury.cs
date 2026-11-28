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
        private sealed class AcquiredSummonFury
        {
            private readonly string output;
            private string previous;
            private readonly SummonFuryReceipt receipt = new SummonFuryReceipt();

            internal AcquiredSummonFury(string output) { this.output = output; }

            internal void Prepare(FightBattleCheckpoint state)
            { previous = FightBattleCheckpointSerializer.Save(state); }

            internal void Observe(FightBattleStepEvent step, FightBattleCheckpoint state)
            {
                var assets = OfficialCombatAssetCatalog.LoadDefault();
                var catalog = assets.Load();
                var formula = assets.LoadDamageFormula();
                var actor = state.Units.Single(unit => unit.Id == 1);
                var listener = actor.Statuses.Single(status => status.ClassId == 25012
                    && status.ApplyingSkillItemId == 10527);
                var summons = state.Units.Where(unit => unit.MasterId == actor.Id).ToArray();
                foreach (var summon in summons.Where(unit => unit.Hp > 0))
                {
                    var buff = summon.Statuses.Single(status => status.ClassId == 24070);
                    Require(buff.CreatorId == actor.Id && buff.OwnerId == summon.Id
                        && buff.ApplyingSkillItemId == 10527 && buff.RootSkillId == 25012
                        && buff.SourceSkillRank == listener.SourceSkillRank
                        && buff.SourceSkillLevel == 2 && buff.SourceSubRank == actor.SubRank
                        && buff.DurationRound == -1 && !buff.CanDispersed && !buff.IsOpenStack,
                        "The actual summon fury lost its cultivated owner or permanent attack status.");
                    var values = catalog.CalculateSkillProps(24070, listener.SourceSkillRank, 2, actor.SubRank);
                    long attack = summon.BaseProperties.Where(value => value.Id == 1).Sum(value => value.Value);
                    long baseAttack = summon.BaseProperties.Where(value => value.Id == formula.PropertyId("BaseAttack"))
                        .Sum(value => value.Value);
                    long expected = attack + (long)((attack + baseAttack) * (values[251] / 10000.0)) + values[130];
                    Require(summon.Properties.Single(value => value.Id == 1).Value == expected,
                        "The actual summon omitted or duplicated the original attack percentage/fixed bonus.");
                }
                Require(!actor.Statuses.Any(status => status.ClassId == 24070),
                    "The summon-only attack status was applied to its summoner.");
                receipt.summons = Math.Max(receipt.summons, summons.Length);
                var hits = step.DamageEvents.Where(hit => summons.Any(unit => unit.Id == hit.SourceId)
                    && hit.Damage < 0 && !hit.IsCure && !hit.TrueDamage && !hit.Dodge
                    && !hit.Immunity && !hit.Blinding).ToArray();
                if (hits.Length > 0)
                {
                    var control = FightBattleCheckpointSerializer.Load(previous);
                    foreach (var unit in control.Units)
                        unit.Statuses.RemoveAll(status => status.ApplyingSkillItemId == 10527);
                    FightStatusPropertyRuntime.RebuildAll(control.Units, catalog, formula);
                    var plain = assets.RestoreBattle(control).Step(control.Sequence, control.Counter + 1).Event
                        .DamageEvents.Where(hit => summons.Any(unit => unit.Id == hit.SourceId)
                            && hit.Damage < 0 && !hit.IsCure && !hit.TrueDamage && !hit.Dodge
                            && !hit.Immunity && !hit.Blinding).ToArray();
                    Require(plain.Length == hits.Length,
                        "Removing summon fury changed the original summon hit count.");
                    for (int index = 0; index < hits.Length; index++)
                    {
                        var hit = hits[index]; var unbuffed = plain[index];
                        Require(hit.SourceId == unbuffed.SourceId && hit.TargetId == unbuffed.TargetId
                            && hit.SkillEntityId == unbuffed.SkillEntityId && hit.HitEntityId == unbuffed.HitEntityId
                            && hit.DamageEntityId == unbuffed.DamageEntityId && hit.Delay == unbuffed.Delay
                            && hit.DamageValue < unbuffed.DamageValue,
                            "The cultivated summon attack bonus did not increase the same original damage hit.");
                    }
                    receipt.comparisons += hits.Length;
                    string saved = FightBattleCheckpointSerializer.Save(state);
                    var restored = FightBattleCheckpointSerializer.Load(saved);
                    Require(assets.RestoreBattle(restored).Step(restored.Sequence, restored.Counter).Replayed
                        && FightBattleCheckpointSerializer.Save(restored) == saved,
                        "Restoring summon fury repeated its creation or damage.");
                    receipt.restored = true;
                    File.WriteAllText(Path.Combine(output, "companion-summon-fury-checkpoint.json"), saved);
                }
                previous = FightBattleCheckpointSerializer.Save(state);
            }

            internal void Complete(bool captured)
            {
                Require(receipt.summons > 0 && receipt.comparisons > 0 && receipt.restored && captured,
                    "Summon fury did not render and verify its actual cultivated summon attacks.");
                receipt.captured = captured;
                File.WriteAllText(Path.Combine(output, "companion-summon-fury-receipt.json"), JsonUtility.ToJson(receipt, true));
                Debug.Log("[Acquired passive combat] 10527 cultivated summon attack, original physical hits and restore replay passed.");
            }
        }

        [Serializable]
        private sealed class SummonFuryReceipt
        { public int summons, comparisons; public bool restored, captured; }
    }
}
