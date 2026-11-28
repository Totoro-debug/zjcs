using System;
using System.Linq;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleManaBurstHarness
{
    static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        Need(items.Required(10064).EntityId == 11390, "Mana Burst's original item binding changed.");
        int cases = 0;
        foreach (int rank in new[] { 1, 3, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int rate in new[] { -10000, 0, 10000 })
        foreach (bool large in new[] { false, true })
        foreach (var facing in new[] { new FightGridPoint(0, 1), new FightGridPoint(1, 0),
            new FightGridPoint(0, -1), new FightGridPoint(-1, 0) })
        {
            var actor = Unit(1, 1, 7, 7, formula);
            var target = Unit(2, 2, 7 + 3 * facing.X, 7 + 3 * facing.Y, formula);
            actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRate"), Value = rate });
            if (large) target.BodyOffsets.Add(new FightGridPoint(facing.Y == 0 ? 0 : 1, facing.X == 0 ? 0 : 1));
            var props = catalog.CalculateSkillProps(11390, rank, level, 5);
            var skill = catalog.CreateSkill(11390, 0, 10064, rank, level, 5,
                props.Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            var definition = FightBattleHitCollector.CollectAll(skill).Single();
            Need(skill.ElementType == 104 && skill.Range.Count == 25 && definition.EntityId == 11391
                && definition.DamageId == 11392 && definition.Scope.Count == 13
                && definition.AppliedStatuses.Count == 1 && definition.AppliedStatuses[0].StatusId == 24474
                && Math.Abs(definition.AppliedStatuses[0].Chance - .5f) < .00001f
                && definition.AppliedStatuses[0].AffectedByProp,
                "Mana Burst's original dark area or affected 50% erosion changed.");
            skill.ResetCooldownAtStart = true; skill.LimitedTimes = 1;
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 104729, MaxRound = 30,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            checkpoint.Units.AddRange(new[] { actor, target });
            var clone = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var run = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var resumed = FightBattleExecutor.Begin(clone, formula, new FightDamageSettlement(), catalog, items);
            var step = run.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resumed.Step(clone.Sequence, clone.Counter + 1);
            Same(checkpoint, clone);
            var cast = step.SkillCasts.Single(value => value.SkillEntityId == 11390);
            var hits = step.DamageEvents.Where(hit => hit.SourceId == actor.Id).ToArray();
            long expected = Damage(props);
            Need(hits.Length == (large ? 2 : 1) && hits.All(hit => hit.TargetId == target.Id
                && hit.SkillItemId == 10064 && hit.HitEntityId == 11391 && hit.DamageEntityId == 11392
                && hit.DamageType == 1 && hit.ElementType == 104 && !hit.TrueDamage && !hit.IsCure
                && hit.HasHitGridPosition && hit.Damage == expected),
                "Mana Burst lost its cultivated ordinary dark damage, full body occurrences or private damage carrier.");
            foreach (var hit in hits)
            {
                int distance = Math.Abs(hit.HitGridX - cast.Presentation.TargetX)
                    + Math.Abs(hit.HitGridY - cast.Presentation.TargetY);
                float impact = distance == 0 ? 1.23f : distance == 1 ? 1.28f : 1.33f;
                Need(Math.Abs(hit.Delay - cast.Delay - impact) < .00001f,
                    "The area impact lost its authored center-to-outer-cell timing.");
            }
            var erosion = target.Statuses.SingleOrDefault(status => status.ClassId == 24474);
            Need(rate != -10000 || erosion == null, "Zero application rate applied erosion.");
            Need(rate != 10000 || erosion != null, "Guaranteed application rate lost erosion.");
            if (erosion != null)
            {
                int expectedTicks = rate == 10000 && large ? 6 : 3;
                if (rate == 0) expectedTicks = FightStatusLifecycle.RoundStartRemainingCount(erosion);
                Need(erosion.CreatorId == actor.Id && erosion.ApplyingSkillItemId == 10064
                    && erosion.SourceSkillRank == rank && erosion.SourceSkillLevel == level && erosion.SourceSubRank == 5
                    && erosion.DurationRound == 3 && erosion.RoundTarget == 2 && erosion.RoundUpdateTiming == 1
                    && erosion.CanDispersed && !erosion.IsOpenStack && erosion.IsAddDurationRound
                    && erosion.RoundStartMaxCount == 3 && erosion.RoundStartMergeMaxCountOnStackRound
                    && FightStatusLifecycle.RoundStartRemainingCount(erosion) == expectedTicks
                    && erosion.CurDurationRound == expectedTicks,
                    "Erosion lost source cultivation, target-turn duration, dispel policy or merged trigger budget.");
                var dotProps = catalog.CalculateSkillProps(12560, rank, level, 5);
                int ticks = 0;
                for (int index = 0; index < 40 && target.Statuses.Contains(erosion); index++)
                {
                    var next = run.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                    resumed.Step(clone.Sequence, clone.Counter + 1); Same(checkpoint, clone);
                    var children = next.SkillCasts.Where(value => value.SkillEntityId == 12560).ToArray();
                    Need(children.Length == (next.ActorId == target.Id ? 1 : 0),
                        "Erosion must invoke exactly once at its affected target's actual turn start.");
                    foreach (var child in children)
                    {
                        var damage = next.DamageEvents.Where(hit => child.DamageEventIds.Contains(hit.DamageId)).ToArray();
                        Need(child.ActorId == actor.Id && child.SkillItemId == 10064 && child.Slot < 0
                            && damage.Length == 1 && damage[0].TargetId == target.Id
                            && damage[0].HitEntityId == 12561 && damage[0].DamageEntityId == 10027
                            && damage[0].DamageType == 1 && damage[0].ElementType == 104
                            && !damage[0].TrueDamage && damage[0].Damage == Damage(dotProps),
                            "The erosion child lost its source, inherited cultivation or mitigated dark damage: rank="
                            + rank + ", level=" + level + ", source=" + child.ActorId + ", item=" + child.SkillItemId
                            + ", slot=" + child.Slot + ", expected=" + Damage(dotProps) + ", hits="
                            + string.Join(";", damage.Select(hit => hit.TargetId + "/" + hit.HitEntityId + "/"
                                + hit.DamageEntityId + "/" + hit.DamageType + "/" + hit.ElementType + "/"
                                + hit.TrueDamage + "/" + hit.Damage)));
                        ticks++;
                    }
                }
                Need(ticks == expectedTicks && !target.Statuses.Contains(erosion),
                    "Erosion did not finish its complete merged budget before removal.");
            }
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Need(run.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == saved,
                "Replaying a settled Mana Burst repeated damage, duration extension or a periodic child.");
            cases++;
        }
        return "Mana Burst passed: " + cases + " cultivated four-facing, full-body, exact impact, effect-rate, erosion child, merged duration/count and cold-continuation cases.";
    }

    static long Damage(IReadOnlyDictionary<int, long> properties) =>
        -(long)((1000d * (properties[200] / 10000d) + properties[190]) * (1000d / 1010d));

    static void Same(FightBattleCheckpoint left, FightBattleCheckpoint right) =>
        Need(FightBattleCheckpointSerializer.Save(left) == FightBattleCheckpointSerializer.Save(right),
            "Cold continuation changed Mana Burst's actual damage, erosion counters or turn clock.");

    static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, X = x, Y = y, CharacterType = 1,
            Profession = 16, Level = 50, SubRank = 5, Hp = 1000000000, MaxHp = 1000000000,
            LastMaxHp = 1000000000, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = 1000;
        var values = new Dictionary<string, long> { { "MaxHp", unit.MaxHp }, { "Attack", 1000 },
            { "Defence", 10 }, { "Speed", 1000 }, { "CritAvoidPercent", 10000 } };
        foreach (string name in new[] { "BaseCritRatePercentValue", "BaseCritAvoidPercentValue", "BaseBlockPercentValue",
            "BaseBlockAvoidPercentValue", "BaseElementAdd", "BaseElementReduce", "BaseElementMaster",
            "BaseElementResistance", "BaseKongFuMaster", "BaseKongFuResistance", "BaseEffectRate", "BaseEffectDodge" })
            values[name] = 10000;
        foreach (var property in values)
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(property.Key), Value = property.Value });
        return unit;
    }
}
