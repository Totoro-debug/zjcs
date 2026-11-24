using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSageSummonRestorationHarness
{
    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        Verify(catalog, formula, items, false, 3, 1, 2, 1, true);
        Verify(catalog, formula, items, true, 3, 1, 4, 9, true);
        Verify(catalog, formula, items, false, 3, 4, 2, 1, false);
        Verify(catalog, formula, items, false, 8, 1, 2, 1, false);
    }

    private static void Verify(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool expire, int role, int master, int rank, int level, bool expected)
    {
        var owner = Unit(1, 1, 1, 0, 1, 1, expire ? 1000 : 1);
        var enemy = Unit(2, 2, 2, 0, 10, 11, expire ? 1 : 1000);
        var subject = Unit(3, 1, role, master, 10, 10, 1);
        subject.MonsterId = subject.MonsterTemplateId = role == 3 ? 3196 : 4600;
        var otherOwner = Unit(4, 1, 1, 0, 5, 5, 1);
        owner.Hp = owner.Turn.Hp = owner.MaxHp - 2;
        var bindings = catalog.CreatePassiveStatusBindings(owner, items, new[] {
            new FightOfficialPassiveSkillBinding { ItemId = 10600, Rank = rank, Level = level, SubRank = owner.SubRank }
        });
        catalog.AssembleUnit(owner, null, bindings);
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10, RandomSeed = 78121,
            Grid = new FightBattleGrid { Width = 16, Height = 16 }, ResultRules = new FightBattleResultRules() };
        checkpoint.Units.AddRange(new[] { owner, enemy, subject, otherOwner });
        if (expire)
            subject.Statuses.Add(catalog.CreateStatus(12202, subject.Id, owner.Id, 12, 0, 0, 5, 20, owner.SubRank));
        else
        {
            var attack = new FightBattleSkillDefinition { EntityId = 90010, Slot = 0, TargetType = 1 };
            attack.Range.Add(new FightGridPoint(0, -1));
            attack.Hits.Add(new FightBattleHitDefinition { EntityId = 90011, DamageType = 8,
                HasCustomDamage = true, CustomDamage = -200000, Delay = .4f });
            enemy.Skills.Add(attack);
            enemy.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
        }
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        FightBattleStepEvent removal = null;
        for (int i = 0; i < 12 && removal == null; i++)
        {
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            if (subject.Hp <= 0 || subject.Ended) removal = step;
        }
        Check(removal != null, "The test did not resolve a real summon death or original lifespan expiry.");
        var cures = removal.DamageEvents.Where(value => value.SkillEntityId == 12050).ToArray();
        var casts = removal.SkillCasts.Where(value => value.SkillEntityId == 12050).ToArray();
        if (expected)
        {
            Check(casts.Length == 1 && casts[0].ActorId == owner.Id && casts[0].Slot == -1
                && casts[0].SkillItemId == 10600 && Math.Abs(casts[0].Delay - (expire ? 0f : .4f)) < .0001f,
                "The summon removal changed the cultivated heal owner, source or timing (expiry="
                    + expire + ", casts=" + casts.Length + ").");
            var cure = cures.Single();
            var target = owner.Properties.ToDictionary(value => value.Id, value => value.Value);
            var source = owner.Properties.ToDictionary(value => value.Id, value => value.Value);
            foreach (var prop in catalog.CalculateSkillProps(12050, rank, level, owner.SubRank))
                source[prop.Key] = source.TryGetValue(prop.Key, out long previous) ? previous + prop.Value : prop.Value;
            long offered = formula.CalculateCure(source, target, formula.PropertyId("SkillCureByHp"),
                formula.PropertyId("SkillFixedCure")).Cure;
            Check(cure.IsCure && cure.SourceId == owner.Id && cure.TargetId == owner.Id
                && cure.HitEntityId == 12051 && cure.DamageEntityId == 12052
                && cure.DamageValue == offered && cure.Damage == 2 && cure.AfterHp == cure.MaxHp,
                "Restoration changed the cultivated formula, self-target, cap or original child hit.");
        }
        else Check(casts.Length == 0 && cures.Length == 0, "An unrelated death or substitute healed the caster.");
        string saved = FightBattleCheckpointSerializer.Save(checkpoint);
        var restored = FightBattleCheckpointSerializer.Load(saved);
        executor = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(executor.Step(restored.Sequence, restored.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(restored) == saved,
            "Restoring a summons death changed its healing receipt.");
        if (!restored.Finished)
            Check(executor.Step(restored.Sequence, restored.Counter + 1).Event.SkillCasts.All(cast => cast.SkillEntityId != 12050),
                "Restored summon death triggered the healing twice.");
    }

    private static FightBattleUnitState Unit(int id, int side, int role, int master, int x, int y, int speed)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = role, MasterId = master,
            Hp = 100000, MaxHp = 100000, LastMaxHp = 100000, Level = 100, SubRank = 8, X = x, Y = y, MoveMask = 5,
            Turn = new FightTurnUnit { Id = id, MasterId = master, Hp = 100000, Speed = speed } };
        unit.Properties.Add(new FightPropertyValue { Id = 1, Value = 10001 });
        unit.Properties.Add(new FightPropertyValue { Id = 3, Value = 100000 });
        unit.Properties.Add(new FightPropertyValue { Id = 4, Value = speed });
        return unit;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
