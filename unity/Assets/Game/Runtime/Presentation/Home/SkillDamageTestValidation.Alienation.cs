using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static bool HasAlienationCompanion(int active)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_PASSIVE_SAVE");
            if (active != 10028 || string.IsNullOrWhiteSpace(path)) return false;
            var save = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(path));
            return save.Player.Skills.Plans[save.Player.Skills.SelectedPlan].PassiveSkills[0] == 10579;
        }

        private static void VerifyAcquiredAlienation(FightBattleCheckpoint checkpoint, string output)
        {
            string receipt = Path.Combine(output, "companion-alienation-checkpoint.json");
            if (File.Exists(receipt)) return;
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25048
                && status.ApplyingSkillItemId == 10579 && status.SourceSkillLevel == 2);
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = assets.Load();
            var formula = assets.LoadDamageFormula();
            var properties = catalog.CalculateSkillProps(25048, listener.SourceSkillRank,
                listener.SourceSkillLevel, listener.SourceSubRank).ToDictionary(pair => pair.Key, pair => pair.Value);
            double fixedReduce = formula.ReadProperty(properties, formula.PropertyId("FixedStatusDmgReduce"));
            double percentReduce = formula.ReadProperty(properties, formula.PropertyId("StatusDmgReducePer"));
            foreach (var source in checkpoint.Units.Where(unit => unit.Side != actor.Side && unit.Hp > 0))
            foreach (var skill in source.Skills.Where(value => value.TargetType == 1))
            foreach (var hit in FightBattleHitCollector.CollectAll(skill).Where(value => value.DamageType == 1 && !value.IsSummon))
            {
                var reduced = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var plain = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var reference = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var reducedSource = reduced.Units.Single(unit => unit.Id == source.Id);
                var plainSource = plain.Units.Single(unit => unit.Id == source.Id);
                var referenceSource = reference.Units.Single(unit => unit.Id == source.Id);
                reducedSource.Statuses.Add(catalog.CreateStatus(24040, source.Id, actor.Id, 724040));
                plainSource.Statuses.Add(catalog.CreateStatus(24040, source.Id, actor.Id, 724040));
                referenceSource.Statuses.Add(catalog.CreateStatus(24040, source.Id, actor.Id, 724040));
                var reducedTarget = reduced.Units.Single(unit => unit.Id == actor.Id);
                var plainTarget = plain.Units.Single(unit => unit.Id == actor.Id);
                var referenceTarget = reference.Units.Single(unit => unit.Id == actor.Id);
                plainTarget.Statuses.RemoveAll(status => status.ClassId == 25048);
                referenceTarget.Statuses.RemoveAll(status => status.ClassId == 25048);
                referenceTarget.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("StatusDmgReducePer"), Value = properties[232] });
                referenceTarget.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("FixedStatusDmgReduce"), Value = properties[236] });
                var resolver = new FightBattleHitExecutor(formula, new FightDamageSettlement());
                var changed = resolver.Apply(new FightRandomStream(91), 1, reducedSource, reducedTarget, skill, hit, 1, false);
                var baseline = resolver.Apply(new FightRandomStream(91), 1, plainSource, plainTarget, skill, hit, 1, false);
                var expectedHit = resolver.Apply(new FightRandomStream(91), 1, referenceSource, referenceTarget, skill, hit, 1, false);
                long original = -baseline.Settlement.RealDamageValue + baseline.ShieldDamage;
                if (original <= fixedReduce) continue;
                long expected = -expectedHit.Settlement.RealDamageValue + expectedHit.ShieldDamage;
                Require(expected > 0 && expected < original
                    && -changed.Settlement.RealDamageValue + changed.ShieldDamage == expected,
                    "The cultivated alienation did not reduce the afflicted attacker's hit: base=" + original
                    + ", actual=" + (-changed.Settlement.RealDamageValue + changed.ShieldDamage)
                    + ", expected=" + expected + ", fixed=" + fixedReduce + ", percent=" + percentReduce
                    + ", skill=" + skill.EntityId + ", hit=" + hit.EntityId);
                File.WriteAllText(receipt, FightBattleCheckpointSerializer.Save(checkpoint));
                Debug.Log("[Acquired passive combat] 10579 cultivated afflicted attacker passed.");
                return;
            }
            throw new InvalidOperationException("The real enemy roster has no suitable configured hit for alienation.");
        }
    }
}
