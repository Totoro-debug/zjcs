using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStarBurstHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Star burst: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0, normalApplied = 0, normalMissed = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (bool large in new[] { false, true })
        foreach (var dir in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        foreach (int rate in new[] { 0, 100000, -10000 })
        {
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8,
                RandomSeed = rank * 100 + level * 10 + dir[0] * 3 + dir[1],
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            state.Units.Add(Unit(1, 1, 8, 8, formula));
            state.Units.Add(Unit(2, 2, 8 + 4 * dir[0], 8 + 4 * dir[1], formula));
            state.Units.Add(Unit(3, 2, 8 + 6 * dir[0], 8 + 6 * dir[1], formula));
            state.Units.Add(Unit(4, 2, 8 + 7 * dir[0], 8 + 7 * dir[1], formula));
            state.Units.Add(Unit(5, 1, 8 + 4 * dir[0] - dir[1], 8 + 4 * dir[1] + dir[0], formula));
            state.Units[0].Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRate"), Value = rate });
            if (large) state.Units[1].BodyOffsets.Add(new FightGridPoint(-dir[0], -dir[1]));
            var skill = Skill(catalog, 11450, 10002, rank, level);
            state.Units[0].Skills.Add(skill); state.Units[0].SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4 }); state.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(state.Sequence, state.Counter + 1).Event;
            resumed.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "restoration changed light damage, scope or status rolls");
            var casts = step.SkillCasts.Where(cast => cast.SkillEntityId == 11450).ToArray();
            var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 11450).ToArray();
            Check(casts.Length == 1 && casts[0].Presentation.TargetId == 2
                && casts[0].Presentation.TargetX == state.Units[1].X && casts[0].Presentation.TargetY == state.Units[1].Y,
                "the five-cell point selection lost its actual cast position: casts=" + casts.Length
                    + (casts.Length == 0 ? "" : ", target=" + casts[0].Presentation.TargetId + ":"
                        + casts[0].Presentation.TargetX + "," + casts[0].Presentation.TargetY)
                    + ", primary=" + state.Units[1].X + "," + state.Units[1].Y);
            Check(hits.Length == (large ? 3 : 2) && hits.Count(hit => hit.TargetId == 2) == (large ? 2 : 1)
                && hits.Count(hit => hit.TargetId == 3) == 1,
                "the two-cell area omitted a body/secondary cell or hit an excluded target");
            var props = catalog.CalculateSkillProps(11450, rank, level, 8);
            long expected = (long)((1000m * props[200] / 10000m + props[190]) * 1000m / 1010m);
            Check(hits.All(hit => hit.SourceId == 1 && hit.SkillItemId == 10002 && hit.HitEntityId == 11451
                && hit.DamageEntityId == 11452 && hit.ElementType == 103 && hit.DamageType == 1
                && !hit.TrueDamage && !hit.IsCure && hit.Damage == -expected && Math.Abs(hit.Delay - .5f) < .0001f),
                "cultivated fixed/percentage light damage or original hit timing changed: " + rank + "/" + level
                    + " seed=" + state.RandomSeed + " expected=" + expected + " hits="
                    + string.Join(";", hits.Select(hit => hit.Damage + ":" + hit.Delay + ":crit=" + hit.Critical
                        + ":block=" + hit.Block + ":dodge=" + hit.Dodge)));
            foreach (var target in state.Units.Where(unit => unit.Id == 2 || unit.Id == 3))
            {
                var blind = target.Statuses.SingleOrDefault(status => status.ClassId == 24234);
                if (rate == 100000) Check(blind != null, "increased effect hit did not affect the 30% baseline roll");
                if (rate == -10000) Check(blind == null, "zero effect hit still applied blinding");
                if (rate == 0) { if (blind == null) normalMissed++; else normalApplied++; }
                if (blind != null) Check(blind.CreatorId == 1 && blind.ApplyingSkillItemId == 10002
                    && blind.SourceSkillRank == rank && blind.SourceSkillLevel == level && blind.SourceSubRank == 8
                    && blind.DurationRound == -1 && blind.ActionType == 63,
                    "blinding lost its original cultivated source or action duration");
            }
            Check(state.Units[3].Hp == 1000000 && state.Units[4].Hp == 1000000,
                "the blast hit a distant or friendly unit");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(state) == saved, "receipt replay applied the blast twice");
            cases++;
        }
        Check(normalApplied > 0 && normalMissed > 0, "the original 30% chance became unconditional: "
            + normalApplied + "/" + normalMissed);
        VerifyBlinding(catalog, formula, items);
        return "Star burst passed: " + cases + " point/range, body cells, cultivated light damage, timing, effect-hit probability, attribution and restore cases.";
    }

    private static void VerifyBlinding(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int blinded = 0, connected = 0;
        for (int seed = 1; seed <= 32; seed++)
        {
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed * 7919 + 37,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var target = Unit(1, 1, 8, 8, formula);
            var source = Unit(2, 2, 8, 9, formula); source.Profession = 4; source.Turn.Speed = 2000;
            source.Properties.Single(prop => prop.Id == formula.PropertyId("Speed")).Value = 2000;
            var skill = Skill(catalog, 10600, 10008, 2, 2);
            source.Skills.Add(skill); source.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            source.Statuses.Add(catalog.CreateStatus(24234, 2, 1, 724234, 11450, 10002, 4, 2, 8));
            state.Units.AddRange(new[] { target, source });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            int hits = 0;
            while (hits < 2 && !state.Finished && state.Counter < 12)
            {
                var step = executor.Step(state.Sequence, state.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                    "restoration changed blinding's next-skill miss or consumption");
                foreach (var hit in step.DamageEvents.Where(hit => hit.SourceId == 2 && hit.SkillEntityId == 10600))
                {
                    if (hits == 0)
                    {
                        if (hit.Blinding) blinded++; else connected++;
                        Check(hit.Blinding ? hit.Damage == 0 && hit.BeforeHp == hit.AfterHp : hit.Damage < 0,
                            "the next damaging skill did not respect its blinding outcome");
                    }
                    else Check(!hit.Blinding && hit.Damage < 0, "blinding survived its next damaging skill");
                    Check(!source.Statuses.Any(status => status.ClassId == 24234)
                        && !source.Properties.Any(prop => prop.Id == formula.PropertyId("BlindingPercent") && prop.Value != 0),
                        "the consumed status or its temporary property remained on the attacker");
                    hits++;
                }
            }
            Check(hits == 2, "the controlled attacker did not release both original slashes");
        }
        Check(blinded > 0 && connected > 0, "the original 50% miss chance became unconditional: " + blinded + "/" + connected);
    }

    private static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog catalog, int entity, int item, int rank, int level)
        => catalog.CreateSkill(entity, 0, item, rank, level, 8, catalog.CalculateSkillProps(entity, rank, level, 8)
            .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 13,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 100;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 },
            { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 },
            { "BaseElementMaster", 10000 }, { "BaseElementResistance", 10000 },
            { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
