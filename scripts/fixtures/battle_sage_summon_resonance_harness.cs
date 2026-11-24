using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSageSummonResonanceHarness
{
    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        SpawnLifecycle(catalog, formula, items);
        foreach (var cultivation in new[] { (Rank: 2, Level: 1), (Rank: 4, Level: 9) })
        {
            var owner = Unit(1, 1, 1, 0, 5, 5, 1000);
            var substitute = Unit(2, 1, 8, 1, 8, 5);
            var otherSummon = Unit(3, 1, 3, 8, 8, 6);
            var enemy = Unit(4, 2, 2, 0, 10, 6);
            var bindings = catalog.CreatePassiveStatusBindings(owner, items, new[] {
                new FightOfficialPassiveSkillBinding {
                    ItemId = 10591, Rank = cultivation.Rank, Level = cultivation.Level, SubRank = owner.SubRank
                }
            });
            catalog.AssembleUnit(owner, null, bindings);
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20, RandomSeed = 41,
                Grid = new FightBattleGrid { Width = 16, Height = 16 }, ResultRules = new FightBattleResultRules() };
            checkpoint.Units.AddRange(new[] { owner, substitute, otherSummon, enemy });
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            executor.Step(0, 1);
            Check(!owner.Statuses.Any(status => status.ClassId == 24133), "A substitute or another owner's summon enabled resonance.");
            long baseAttack = Attack(owner);

            var first = Unit(5, 1, 3, 1, 7, 5);
            var second = Unit(6, 1, 3, 1, 7, 6);
            checkpoint.Units.AddRange(new[] { first, second });
            executor = FightBattleExecutor.Restore(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var started = executor.Step(0, 2).Event;
            var buff = owner.Statuses.Single(status => status.ClassId == 24133);
            var expected = catalog.CalculateSkillProps(24133, cultivation.Rank, cultivation.Level, owner.SubRank)
                .ToDictionary(property => property.Key, property => property.Value);
            long gained = (long)(baseAttack * formula.ReadProperty(expected, formula.PropertyId("AttackScale")))
                + (long)formula.ReadProperty(expected, formula.PropertyId("FixedAttack"));
            Check(gained > 0 && Attack(owner) == baseAttack + gained && buff.StackCount == 1
                && buff.CreatorId == owner.Id && buff.ApplyingSkillItemId == 10591 && !buff.CanDispersed
                && buff.SourceSkillRank == cultivation.Rank && buff.SourceSkillLevel == cultivation.Level
                && started.StatusViewEvents.Count(view => view.Status.ClassId == 24133
                    && view.OwnerId == owner.Id && view.Kind == FightBattleStatusViewEventKind.Start) == 1,
                "Two summons did not apply exactly one original cultivated attack increase.");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            checkpoint = FightBattleCheckpointSerializer.Load(saved);
            executor = FightBattleExecutor.Restore(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == saved,
                "The resonance source, amount or native event changed on restore.");
            owner = checkpoint.Units.Single(unit => unit.Id == 1);
            first = checkpoint.Units.Single(unit => unit.Id == 5);
            second = checkpoint.Units.Single(unit => unit.Id == 6);
            first.Hp = first.Turn.Hp = 0;
            executor.Step(0, 3);
            Check(Attack(owner) == baseAttack + gained, "The first removed summon ended resonance early.");
            second.Hp = second.Turn.Hp = 0;
            var ended = executor.Step(0, 4).Event;
            Check(Attack(owner) == baseAttack && owner.Statuses.All(status => status.ClassId != 24133)
                && ended.StatusViewEvents.Count(view => view.Status.ClassId == 24133
                    && view.OwnerId == owner.Id && view.Kind == FightBattleStatusViewEventKind.End) == 1,
                "The last removed summon did not return attack to its ordinary value.");
        }
    }

    private static void SpawnLifecycle(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var owner = Unit(1, 1, 1, 0, 5, 5, 1000);
        var enemy = Unit(2, 2, 2, 0, 6, 5);
        var bindings = catalog.CreatePassiveStatusBindings(owner, items, new[] {
            new FightOfficialPassiveSkillBinding { ItemId = 10591, Rank = 4, Level = 9, SubRank = owner.SubRank }
        });
        catalog.AssembleUnit(owner, null, bindings);
        var skill = catalog.CreateSkill(12140, 0, 10095, 4, 9, owner.SubRank, null);
        owner.Skills.Add(skill);
        owner.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20, RandomSeed = 41,
            Grid = new FightBattleGrid { Width = 16, Height = 16 }, ResultRules = new FightBattleResultRules() };
        checkpoint.Units.AddRange(new[] { owner, enemy });
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        long baseline = Attack(owner);
        var spawn = executor.Step(0, 1).Event;
        var rock = checkpoint.Units.Single(unit => unit.MonsterTemplateId == 3196);
        Check(rock.Hp > 0 && owner.Statuses.Count(status => status.ClassId == 24133) == 1
            && Attack(owner) > baseline && spawn.StatusViewEvents.Count(view => view.Status.ClassId == 24133
                && view.Kind == FightBattleStatusViewEventKind.Start) == 1,
            "A real summon did not apply resonance in its spawn step.");
        // Expire the real summon through its original lifetime status while its
        // creator takes the next turn, rather than changing a saved unit list.
        var lifetime = rock.Statuses.Single(status => status.ClassId == 12142);
        lifetime.DurationRound = 1;
        lifetime.CurDurationRound = 1;
        lifetime.StartRound = owner.SkillState.Round;
        bool removed = false;
        for (int i = 0; i < 10 && !checkpoint.Finished; i++)
        {
            var step = executor.Step(0, checkpoint.Counter + 1).Event;
            if (!rock.Ended) continue;
            Check(owner.Statuses.All(status => status.ClassId != 24133) && Attack(owner) == baseline
                && step.StatusViewEvents.Count(view => view.Status.ClassId == 24133
                    && view.Kind == FightBattleStatusViewEventKind.End) == 1,
                "The real last summon did not remove resonance in its expiry step.");
            removed = true;
            break;
        }
        Check(removed, "The real resonance summon did not reach lifespan expiry.");
    }

    private static FightBattleUnitState Unit(int id, int side, int role, int master, int x, int y, int speed = 1)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = role, MasterId = master,
            Hp = 100000, MaxHp = 100000, LastMaxHp = 100000, Level = 100, SubRank = 8, X = x, Y = y, MoveMask = 5,
            Turn = new FightTurnUnit { Id = id, MasterId = master, Hp = 100000, Speed = speed } };
        unit.Properties.Add(new FightPropertyValue { Id = 1, Value = 10001 });
        unit.Properties.Add(new FightPropertyValue { Id = 3, Value = 100000 });
        unit.Properties.Add(new FightPropertyValue { Id = 4, Value = speed });
        return unit;
    }

    private static long Attack(FightBattleUnitState unit) => unit.Properties.Single(property => property.Id == 1).Value;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
