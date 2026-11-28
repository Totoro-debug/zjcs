using System;
using System.Linq;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleAbyssalHandHarness
{
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        Check(items.Required(10040).EntityId == 10950 && formula.PropertyId("SpeedScale") == 253,
            "Abyssal Hand lost its original item or slow property binding.");
        int cases = 0;
        foreach (int rank in new[] { 1, 3, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int effectRate in new[] { -10000, 0, 10000 })
        foreach (bool large in new[] { false, true })
        foreach (var facing in new[] { new FightGridPoint(0, 1), new FightGridPoint(1, 0),
            new FightGridPoint(0, -1), new FightGridPoint(-1, 0) })
        {
            var actor = Unit(1, 1, 7, 7, formula);
            var target = Unit(2, 2, 7 + 3 * facing.X, 7 + 3 * facing.Y, formula);
            actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRate"), Value = effectRate });
            if (large) target.BodyOffsets.Add(new FightGridPoint(facing.Y == 0 ? 0 : 1, facing.X == 0 ? 0 : 1));
            var props = catalog.CalculateSkillProps(10950, rank, level, 5);
            var skill = catalog.CreateSkill(10950, 0, 10040, rank, level, 5,
                props.Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            Check(skill.ElementType == 104 && skill.Range.Count == 25
                && skill.Range.All(p => Math.Abs(p.X) + Math.Abs(p.Y) <= 3), "Original cast radius changed.");
            var definition = FightBattleHitCollector.CollectAll(skill).Single();
            Check(definition.EntityId == 10951 && definition.DamageId == 10952 && definition.Scope.Count == 13
                && definition.Scope.All(p => Math.Abs(p.X) + Math.Abs(p.Y) <= 2)
                && definition.AppliedStatuses.Count == 1 && definition.AppliedStatuses[0].StatusId == 24150
                && Math.Abs(definition.AppliedStatuses[0].Chance - .5f) < .00001f
                && definition.AppliedStatuses[0].AffectedByProp, "Original area or affected 50% slow changed.");
            skill.ResetCooldownAtStart = true; skill.LimitedTimes = 1;
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 104729, MaxRound = 20,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            cp.Units.AddRange(new[] { actor, target });
            var clone = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var run = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var resumed = FightBattleExecutor.Begin(clone, formula, new FightDamageSettlement(), catalog, items);
            var step = run.Step(cp.Sequence, cp.Counter + 1).Event;
            resumed.Step(clone.Sequence, clone.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(clone),
                "Abyssal Hand changed damage or slow on a cold continuation.");
            var cast = step.SkillCasts.Single(c => c.SkillEntityId == 10950);
            var hits = step.DamageEvents.Where(h => h.SourceId == 1).ToArray();
            long damage = -(long)((1000d * props[200] / 10000d + props[190]) * 1000d / 1010d);
            Check(hits.Length == (large ? 2 : 1) && hits.All(h => h.TargetId == 2 && h.SkillItemId == 10040
                && h.SkillEntityId == 10950 && h.HitEntityId == 10951 && h.DamageEntityId == 10952
                && h.DamageType == 1 && h.ElementType == 104 && !h.TrueDamage && !h.IsCure
                && h.Damage == damage && Math.Abs(h.Delay - cast.Delay - 1f) < .00001f),
                "Abyssal Hand lost cultivated dark damage, body cells or authored impact time.");
            var slow = target.Statuses.SingleOrDefault(s => s.ClassId == 24150);
            Check(effectRate != -10000 || slow == null, "Zero effective application rate applied slow.");
            Check(effectRate != 10000 || slow != null, "Guaranteed effective application rate lost slow.");
            if (slow != null)
            {
                Check(slow.CreatorId == 1 && slow.ApplyingSkillItemId == 10040 && slow.SourceSkillRank == rank
                    && slow.SourceSkillLevel == level && slow.SourceSubRank == 5 && slow.DurationRound == 2
                    && slow.CurDurationRound == 2 && slow.CanDispersed && slow.StackCount == 1
                    && slow.StatusProperties.Single(p => p.Id == 253).Value == -1500
                    && target.Properties.Single(p => p.Id == formula.PropertyId("Speed")).Value == 850,
                    "Slow lost its cultivated source, two rounds, dispel policy or original 15% speed reduction.");
                int ownerTurns = 0;
                for (int i = 0; i < 20 && target.Statuses.Any(s => s.ClassId == 24150); i++)
                {
                    var next = run.Step(cp.Sequence, cp.Counter + 1).Event;
                    resumed.Step(clone.Sequence, clone.Counter + 1);
                    if (next.ActorId == target.Id) ownerTurns++;
                    Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(clone),
                        "The slowed turn clock or expiry changed after continuation.");
                }
                Check(ownerTurns == 2 && target.Statuses.All(s => s.ClassId != 24150)
                    && target.Properties.Single(p => p.Id == formula.PropertyId("Speed")).Value == 1000,
                    "Slow did not expire after two target turns and restore original speed.");
            }
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(run.Step(cp.Sequence, cp.Counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Replaying Abyssal Hand repeated a hit or slow clock.");
            cases++;
        }
        cases += CheckScope(catalog, formula);
        return "Abyssal Hand passed: " + cases + " cultivated four-facing, body, dark damage, effect-rate, slow speed/expiry, range and continuation cases.";
    }

    static int CheckScope(FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        int cases = 0;
        var actor = Unit(1, 1, 4, 7, formula);
        var primary = Unit(2, 2, 7, 7, formula);
        var skill = catalog.CreateSkill(10950, 0, 10040, 1, 1, 5,
            catalog.CalculateSkillProps(10950, 1, 1, 5)
                .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
        var original = FightBattleHitCollector.CollectAll(skill).Single();
        var selector = new FightBattleHitDefinition { Anchor = original.Anchor, Scope = original.Scope,
            ScopeTransform = original.ScopeTransform, TargetMode = 1 };
        foreach (int dx in Enumerable.Range(-3, 7))
        foreach (int dy in Enumerable.Range(-3, 7))
        foreach (bool large in new[] { false, true })
        foreach (string mode in new[] { "enemy", "ally", "dead", "hidden" })
        {
            var probe = Unit(3, mode == "ally" ? 1 : 2, 7 + dx, 7 + dy, formula);
            if (large) probe.BodyOffsets.Add(new FightGridPoint(1, 0));
            if (mode == "dead") probe.Hp = 0;
            if (mode == "hidden") probe.Targetable = false;
            var result = FightBattleTargeting.SelectScopeTargets(actor, new[] { actor, probe }, skill, primary, selector);
            bool inRange = probe.BodyOffsets.Any(p => Math.Abs(dx + p.X) + Math.Abs(dy + p.Y) <= 2);
            Check(result.Count == (mode == "enemy" && inRange ? 1 : 0),
                "The original radius-two area changed full-body inclusion, target side, life or selectability.");
            cases++;
        }
        return cases;
    }

    static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, X = x, Y = y, CharacterType = 1,
            Profession = 16, Level = 50, SubRank = 5, Hp = 1000000000, MaxHp = 1000000000,
            LastMaxHp = 1000000000, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = 1000;
        var values = new Dictionary<string, long> { { "MaxHp", unit.MaxHp }, { "Attack", 1000 }, { "Defence", 10 },
            { "Speed", 1000 }, { "CritAvoidPercent", 10000 } };
        foreach (string name in new[] { "BaseCritRatePercentValue", "BaseCritAvoidPercentValue", "BaseBlockPercentValue",
            "BaseBlockAvoidPercentValue", "BaseElementAdd", "BaseElementReduce", "BaseElementMaster",
            "BaseElementResistance", "BaseKongFuMaster", "BaseKongFuResistance", "BaseEffectRate", "BaseEffectDodge" })
            values[name] = 10000;
        foreach (var p in values) unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(p.Key), Value = p.Value });
        return unit;
    }
}
