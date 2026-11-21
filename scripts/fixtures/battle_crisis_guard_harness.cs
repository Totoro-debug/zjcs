using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleCrisisGuardHarness
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Crisis Guard: " + message);
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y,
        long hp, long maxHp, long defence, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 3,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = hp, MaxHp = maxHp,
            LastMaxHp = maxHp, MoveMask = 5
        };
        unit.Turn.Id = id;
        unit.Turn.Hp = hp;
        unit.Turn.Speed = id == 1 ? 1000 : 10;
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = defence });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = maxHp });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int[] growth in new[] { new[] { 2, 1, 1 }, new[] { 5, 20, 5 }, new[] { 7, 30, 8 } })
        foreach (long defence in new[] { 10L, 1000L, 5000L })
        foreach (bool protectSelf in new[] { false, true })
        {
            int rank = growth[0], level = growth[1], subRank = growth[2];
            var actor = Unit(1, 1, 5, 5, protectSelf ? 1 : 9000, 10000, defence, formula);
            var lowNumber = Unit(2, 1, 6, 5, 100, 200, 3, formula);
            var lowPercent = Unit(3, 1, 5, 8, 200, 10000, 7, formula);
            var outOfRange = Unit(4, 1, 9, 5, 1, 50000, 9, formula);
            var enemy = Unit(5, 2, 4, 5, 1, 50000, 1, formula);
            var target = protectSelf ? actor : lowPercent;
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 8, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 }
            };
            checkpoint.Units.AddRange(new[] { actor, lowNumber, lowPercent, outOfRange, enemy });
            var wave = new FightBattleWaveState();
            wave.UnitIds.Add(enemy.Id);
            checkpoint.Waves.Add(wave);
            var skill = catalog.CreateSkill(11170, 0, 10032, rank, level, subRank,
                catalog.CalculateSkillProps(11170, rank, level, subRank)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            actor.Skills.Add(skill);
            actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            var cast = step.SkillCasts.Single(value => value.ActorId == actor.Id && value.SkillEntityId == 11170);
            var shielded = checkpoint.Units.Single(unit => unit.Statuses.Any(value => value.ClassId == 24180));
            Check(shielded.Id == target.Id,
                "expected the lowest-percentage ally in range " + target.Id + ", got " + shielded.Id);
            var applied = step.StatusViewEvents.Single(value => value.Status.ClassId == 24180);
            Check(Math.Abs(applied.Delay - cast.Delay - 1.2f) < 0.0001f,
                "shield did not arrive at the authored hit delay");
            var shield = target.Statuses.Single(value => value.ClassId == 24180);
            var raw = catalog.CalculateSkillProps(24180, rank, level, subRank)
                .ToDictionary(value => value.Key, value => value.Value);
            long expected = (long)formula.ReadProperty(raw, formula.PropertyId("SkillFixedShield"))
                + (long)(defence * formula.ReadProperty(raw, formula.PropertyId("ShieldByDefence")));
            Check(expected > 0 && shield.ShieldValue == expected && shield.ShieldRemainValue == expected,
                "shield amount differs from the official fixed and caster-defence terms");
            Check(shield.CreatorId == actor.Id && shield.OwnerId == target.Id && shield.DurationRound == 3
                && shield.SourceSkillRank == rank && shield.SourceSkillLevel == level && shield.SourceSubRank == subRank,
                "shield lost its source, target, duration or cultivation");
            Check(step.DamageEvents.All(value => value.Damage == 0 && !value.IsCure),
                "applying the shield must not damage or heal a target");
            var loaded = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var restored = FightBattleExecutor.Restore(loaded, formula, new FightDamageSettlement(), catalog, items);
            executor.Step(checkpoint.Sequence, checkpoint.Counter + 1);
            restored.Step(loaded.Sequence, loaded.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(loaded),
                "continuing from the shield checkpoint changed settlement");
            cases++;
        }
        return "Crisis Guard passed: " + cases + " targeting, formula, duration, and battle-resume cases.";
    }
}
