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
        private static void VerifyAcquiredProtectiveRunes(FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var active = actor.Skills.Single(skill => skill.Slot == 0);
            var status = actor.Statuses.Single(value => value.ClassId == 24201 && value.CreatorId == 1);
            Require(status.ApplyingSkillItemId == 10102 && status.SourceSkillRank == active.Rank
                && status.SourceSkillLevel == 2 && status.SourceSubRank == 8 && status.DurationRound == 3
                && status.CanDispersed && status.DamageReducePer.Supported
                && Math.Abs(status.DamageReducePer.NotActiveSkillScale - 1.2f) < .0001f,
                "The cultivated protective runes lost their original conditional status or three-round clock.");
            string receipt = Path.Combine(output, "acquired-protective-runes-checkpoint.json");
            if (File.Exists(receipt)) return;
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = assets.Load(); var formula = assets.LoadDamageFormula();
            var props = catalog.CalculateSkillProps(24201, active.Rank, active.Level, active.SubRank);
            Require(status.StatusProperties.Single(value => value.Id == 232).Value == props[232]
                && status.StatusProperties.Single(value => value.Id == 236).Value == props[236]
                && props[232] > 0 && props[236] > 0,
                "The actual cultivated protection omitted its percentage or fixed reduction.");
            string live = FightBattleCheckpointSerializer.Save(checkpoint);
            foreach (var source in checkpoint.Units.Where(unit => unit.Side != 1 && unit.Hp > 0))
            foreach (var skill in source.Skills.Where(skill => skill.SkillType == 0))
            foreach (var hit in FightBattleHitCollector.CollectAll(skill).Where(hit => hit.DamageType == 1 && !hit.IsSummon))
            {
                // Compare a real configured hit on copies of the cultivated units;
                // the damage-test screen's one-damage option does not mask this check.
                var reduced = FightBattleCheckpointSerializer.Load(live);
                var plain = FightBattleCheckpointSerializer.Load(live);
                var reducedTarget = reduced.Units.Single(unit => unit.Id == 1);
                var plainTarget = plain.Units.Single(unit => unit.Id == 1);
                plainTarget.Statuses.RemoveAll(value => value.ClassId == 24201);
                var resolver = new FightBattleHitExecutor(formula, new FightDamageSettlement());
                var reducedResult = resolver.Apply(new FightRandomStream(91), 1,
                    reduced.Units.Single(unit => unit.Id == source.Id), reducedTarget, skill, hit, 1, false);
                var plainResult = resolver.Apply(new FightRandomStream(91), 1,
                    plain.Units.Single(unit => unit.Id == source.Id), plainTarget, skill, hit, 1, false);
                long original = -plainResult.Formula.Damage + plainResult.ShieldDamage;
                long protectedDamage = -reducedResult.Formula.Damage + reducedResult.ShieldDamage;
                if (original <= 0) continue;
                Require(protectedDamage >= 0 && protectedDamage < original
                    && FightBattleCheckpointSerializer.Save(checkpoint) == live,
                    "The actual cultivated protection did not reduce the configured hit or changed the live battle.");
                File.WriteAllText(receipt, live);
                Debug.Log("[Acquired active combat] 10102 cultivated conditional reduction, original hit="
                    + skill.EntityId + "/" + hit.EntityId + ", base=" + original + ", reduced=" + protectedDamage + " passed.");
                return;
            }
            throw new InvalidOperationException("The actual roster has no positive original hit for protective runes.");
        }
    }
}
