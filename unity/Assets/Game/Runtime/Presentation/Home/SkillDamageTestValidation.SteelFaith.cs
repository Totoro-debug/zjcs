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
        private static void VerifyAcquiredSteelFaith(FightBattleCheckpoint checkpoint, string output)
        {
            string receipt = Path.Combine(output, "companion-steel-faith-checkpoint.json");
            if (File.Exists(receipt)) return;
            var source = checkpoint.Units.FirstOrDefault(unit => unit.Side != 1 && unit.Hp > 0
                && unit.Statuses.Any(status => status.ActionType == 58 && status.ForbiddenType == 0));
            if (source == null) return;
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25108
                && status.ApplyingSkillItemId == 10592 && status.SourceSkillLevel == 2);
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = assets.Load();
            var formula = assets.LoadDamageFormula();
            var properties = catalog.CalculateSkillProps(25108, listener.SourceSkillRank,
                listener.SourceSkillLevel, listener.SourceSubRank).ToDictionary(pair => pair.Key, pair => pair.Value);
            double fixedReduce = formula.ReadProperty(properties, formula.PropertyId("FixedStatusDmgReduce"));
            double percentReduce = formula.ReadProperty(properties, formula.PropertyId("StatusDmgReducePer"));
            foreach (var skill in source.Skills)
            foreach (var hit in FightBattleHitCollector.CollectAll(skill).Where(hit => hit.DamageType == 1 && !hit.IsSummon))
            {
                // The live dummy roster cannot select fresh attacks while taunted.
                // Replay a real configured hit against copies of its actual cultivated
                // units to verify the conditional settlement in the running Player.
                var reduced = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var plain = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var reducedTarget = reduced.Units.Single(unit => unit.Id == 1);
                var plainTarget = plain.Units.Single(unit => unit.Id == 1);
                plainTarget.Statuses.RemoveAll(status => status.ClassId == 25108);
                var resolver = new FightBattleHitExecutor(formula, new FightDamageSettlement());
                var reducedResult = resolver.Apply(new FightRandomStream(91), 1,
                    reduced.Units.Single(unit => unit.Id == source.Id), reducedTarget, skill, hit, 1, false);
                var plainResult = resolver.Apply(new FightRandomStream(91), 1,
                    plain.Units.Single(unit => unit.Id == source.Id), plainTarget, skill, hit, 1, false);
                long original = -plainResult.Settlement.RealDamageValue + plainResult.ShieldDamage;
                if (original <= fixedReduce) continue;
                long expected = (long)(Math.Max(0, original - fixedReduce) / (1.0 + percentReduce));
                Require(fixedReduce > 0 && percentReduce > 0 && expected > 0 && expected < original
                    && -reducedResult.Settlement.RealDamageValue + reducedResult.ShieldDamage == expected,
                    "The cultivated steel faith did not apply fixed then percent reduction before shields.");
                File.WriteAllText(receipt, FightBattleCheckpointSerializer.Save(checkpoint));
                Debug.Log("[Acquired passive combat] 10592 actual cultivated units/taunt, original hit="
                    + skill.EntityId + "/" + hit.EntityId + ", base=" + original + ", reduced=" + expected + " passed.");
                return;
            }
            throw new InvalidOperationException("The actual taunted roster has no positive original physical hit for steel faith.");
        }
    }
}
