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
        private sealed class AcquiredExtraHealing
        {
            private readonly string output;
            private readonly ExtraHealingReceipt receipt = new ExtraHealingReceipt();
            internal AcquiredExtraHealing(string output) { this.output = output; }
            internal bool NeedsCapture => receipt.casts > 0 && !receipt.captured;
            internal void Captured() { receipt.captured = true; }

            internal void Observe(FightBattleStepEvent step, FightBattleCheckpoint state)
            {
                var actor = state.Units.Single(unit => unit.Id == 1);
                var listener = actor.Statuses.Single(status => status.ClassId == 25039
                    && status.ApplyingSkillItemId == 10570);
                Require(Math.Abs(listener.Components.HitSkill.Rate - .3f) < .000001f
                    && listener.SourceSkillLevel == 2 && listener.CreatorId == actor.Id,
                    "The actual extra healing changed its original 30% rate or cultivated listener.");
                var children = step.SkillCasts.Where(cast => cast.ActorId == actor.Id
                    && cast.SkillEntityId == 11430 && cast.SkillItemId == 10570).ToArray();
                if (children.Length == 0) return;
                var assets = OfficialCombatAssetCatalog.LoadDefault();
                var catalog = assets.Load();
                var formula = assets.LoadDamageFormula();
                var properties = actor.Properties.ToDictionary(value => value.Id, value => value.Value);
                foreach (var pair in catalog.CalculateSkillPropsOnBattle(11430,
                    listener.SourceSkillRank, 2, actor.SubRank, state.IsPvp))
                {
                    properties.TryGetValue(pair.Key, out long previous);
                    properties[pair.Key] = previous + pair.Value;
                }
                foreach (var child in children)
                {
                    var cures = step.DamageEvents.Where(hit => child.DamageEventIds.Contains(hit.DamageId)).ToArray();
                    Require(cures.Length > 0, "The actual extra-healing child did not cure any friendly target.");
                    foreach (var cure in cures)
                    {
                        var target = state.Units.Single(unit => unit.Id == cure.TargetId);
                        long offered = formula.CalculateCure(properties,
                            target.Properties.ToDictionary(value => value.Id, value => value.Value), 210, 170).Cure;
                        Require(target.Side == actor.Side && cure.SourceId == actor.Id && cure.SkillItemId == 10570
                            && cure.HitEntityId == 11431 && cure.DamageEntityId == 11432 && cure.IsCure
                            && !cure.TrueDamage && cure.DamageValue == offered && offered > 0
                            && cure.Damage == Math.Min(offered, Math.Max(0L, target.MaxHp - cure.BeforeHp))
                            && (Math.Abs(cure.Delay - child.Delay) < .0001f
                                || Math.Abs(cure.Delay - child.Delay - .75f) < .0001f),
                            "The actual extra healing lost its cultivated HP/fixed cure, cap, friend or native hit timing.");
                    }
                    receipt.casts++;
                    receipt.cures += cures.Length;
                }
                Require(listener.Components.HitSkill.InvokeCount == receipt.casts,
                    "The extra-healing child recursively triggered its own listener.");
                string saved = FightBattleCheckpointSerializer.Save(state);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                Require(assets.RestoreBattle(restored).Step(restored.Sequence, restored.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring extra healing repeated its cure or probability counter.");
                receipt.restored = true;
                File.WriteAllText(Path.Combine(output, "companion-extra-healing-checkpoint.json"), saved);
            }

            internal void Complete()
            {
                Require(receipt.casts > 0 && receipt.cures > 0 && receipt.restored && receipt.captured,
                    "The real 30% extra healing did not execute, render and restore its original cure.");
                File.WriteAllText(Path.Combine(output, "companion-extra-healing-receipt.json"), JsonUtility.ToJson(receipt, true));
                Debug.Log("[Acquired passive combat] 10570 cultivated extra cure, original probability, native cross and restore replay passed.");
            }
        }

        [Serializable]
        private sealed class ExtraHealingReceipt
        { public int casts, cures; public bool restored, captured; }
    }
}
