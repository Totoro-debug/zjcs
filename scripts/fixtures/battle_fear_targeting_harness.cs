using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFearTargetingHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Fear targeting: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog)
    {
        int cases = 0;
        foreach (bool large in new[] { false, true })
        foreach (bool lowOther in new[] { false, true })
        foreach (bool forbidden in new[] { false, true })
        foreach (bool available in new[] { false, true })
        {
            var owner = Unit(1, 1, 5, 5);
            var source = Unit(2, 2, 6, 5);
            var other = Unit(3, 2, 5, 6);
            if (large) source.BodyOffsets.Add(new FightGridPoint(1, 0));
            if (lowOther) other.Hp = 100;
            other.Ended = !available;
            var units = new List<FightBattleUnitState> { owner, source, other, Unit(4, 1, 4, 5) };
            var skill = catalog.CreateSkill(10630, 0, 10011, 2, 20, 1, null);
            int baseline = FightBattleTargeting.SelectInSkillRange(owner, units, skill).Single().Id;
            var fear = catalog.CreateStatus(24194, owner.Id, source.Id, 100);
            fear.ForbiddenType = forbidden ? 1 : 0;
            owner.Statuses.Add(fear);
            int expected = available && !forbidden ? other.Id : baseline;
            Check(FightBattleTargeting.SelectInSkillRange(owner, units, skill).Single().Id == expected,
                "the applicator kept priority over another legal enemy: " + large + "/" + lowOther + "/" + forbidden + "/" + available);
            if (!forbidden && available)
                Check(FightBattleTargeting.Select(owner, units, 1, skill: skill).Single().Id == other.Id,
                    "ordinary target selection ignored fear");
            other.X = other.Y = 15;
            Check(FightBattleTargeting.SelectInSkillRange(owner, units, skill).Single().Id == source.Id,
                "fear illegally prohibited the only enemy within range");
            owner.Statuses.Remove(fear); other.X = 5; other.Y = 6;
            Check(FightBattleTargeting.SelectInSkillRange(owner, units, skill).Single().Id == baseline,
                "removing fear did not restore the ordinary selection priority");
            cases++;
        }
        return "Fear targeting passed: " + cases + " large-body, HP priority, forbidden, alternative availability, range fallback and removal cases.";
    }

    public static string RunBattle(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var caster = Unit(1, 1, 6, 5);
        var victim = Unit(2, 2, 5, 5);
        var ally = Unit(3, 1, 5, 6);
        foreach (var unit in new[] { caster, victim, ally })
        {
            unit.Hp = unit.MaxHp = unit.LastMaxHp = 1000000;
            unit.Level = 70; unit.SubRank = 8; unit.Profession = 8;
            unit.Turn.Id = unit.Id; unit.Turn.Speed = unit.Id == 1 ? 1000 : unit.Id == 2 ? 500 : 100;
            foreach (var property in new[] { new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 },
                new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 },
                new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp },
                new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed },
                new FightPropertyValue { Id = formula.PropertyId("BaseEffectRate"), Value = 1000 },
                new FightPropertyValue { Id = formula.PropertyId("BaseEffectDodge"), Value = 1000 } }) unit.Properties.Add(property);
        }
        caster.Hp /= 2;
        foreach (var unit in new[] { caster, victim, ally }) unit.Turn.Hp = unit.Hp;
        victim.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectDodgeValue"), Value = -100000 });
        var fearSkill = catalog.CreateSkill(12130, 0, 10104, 4, 2, 8,
            catalog.CalculateSkillProps(12130, 4, 2, 8).Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        fearSkill.LimitedTimes = 1;
        caster.Skills.Add(fearSkill); caster.SkillState.Slots.Add(catalog.CreateSkillSlot(fearSkill, 0));
        var attack = catalog.CreateSkill(10630, 0, 10011, 4, 2, 8,
            catalog.CalculateSkillProps(10630, 4, 2, 8).Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        victim.Skills.Add(attack); victim.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 6, RandomSeed = 91,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        state.Units.AddRange(new[] { caster, victim, ally });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
        var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        int feared = 0, recovered = 0;
        while (!state.Finished && state.Counter < 60)
        {
            bool active = victim.Statuses.Any(status => status.ClassId == 24194);
            var step = executor.Step(state.Sequence, state.Counter + 1).Event;
            resumed.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "restoration changed fear's lifetime, AI attack targets or random progress");
            foreach (var cast in step.SkillCasts.Where(value => value.ActorId == victim.Id && value.SkillEntityId == 10630))
            {
                Check(cast.TargetIds.Count == 1 && cast.TargetIds[0] == (active ? ally.Id : caster.Id),
                    "actual battle AI did not change its target during fear and recover after expiry");
                if (active) feared++; else recovered++;
            }
        }
        Check(feared > 0 && recovered > 0 && victim.Statuses.All(status => status.ClassId != 24194),
            "the actual battle did not exercise both fear and its two-turn expiry");
        return "Fear battle passed: actual hostile skill changed targets during fear, recovered after expiry and matched saved restoration.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y) => new FightBattleUnitState {
        Id = id, Side = side, X = x, Y = y, Hp = 1000, MaxHp = 1000, LastMaxHp = 1000, CharacterType = 1
    };
}
