using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStoneSkinHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Stone Skin: " + message); }

    private static FightBattleUnitState Unit(int id, int x)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
            Profession = id == 1 ? 7 : 3, Level = 50, SubRank = 8, X = x, Y = 5,
            Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var pair in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 },
            { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = pair.Key, Value = pair.Value });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (var growth in new[] { new[] { 1, 1, 1 }, new[] { 7, 30, 8 } })
        foreach (bool repeated in new[] { false, true })
        {
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20,
                RandomSeed = 17, Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var attacker = Unit(1, 5); var holder = Unit(2, 6);
            checkpoint.Units.AddRange(new[] { attacker, holder });
            var attack = catalog.CreateSkill(11440, 0, 10065, 7, 20, 8,
                catalog.CalculateSkillProps(11440, 7, 20, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            if (repeated) attack.HitGraphRoots.AddRange(attack.HitGraphRoots.ToArray());
            attacker.Skills.Add(attack); attacker.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
            holder.Statuses.Add(catalog.CreateStatus(25033, holder.Id, holder.Id, 75033,
                25033, 10542, growth[0], growth[1], growth[2], holder.Properties));
            attacker.Statuses.Add(catalog.CreateStatus(25033, attacker.Id, attacker.Id, 75034,
                25033, 10542, growth[0], growth[1], growth[2], attacker.Properties));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var control = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            control.Units.Single(value => value.Id == holder.Id).Statuses.RemoveAll(value => value.ClassId == 25033);
            control.Units.Single(value => value.Id == attacker.Id).Statuses.RemoveAll(value => value.ClassId == 25033);
            var plain = FightBattleExecutor.Restore(control, formula, new FightDamageSettlement(), catalog, items);
            var replay = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            replay.Step(restored.Sequence, restored.Counter + 1);
            var controlStep = plain.Step(control.Sequence, control.Counter + 1).Event;
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restored),
                "cold checkpoint diverged");
            int hits = step.DamageEvents.Count(value => value.SkillEntityId == 11440 && value.TargetId == holder.Id);
            var damage = step.DamageEvents.Where(value => value.SkillEntityId == 11440 && value.TargetId == holder.Id).ToArray();
            var unprotected = controlStep.DamageEvents.Where(value => value.SkillEntityId == 11440 && value.TargetId == holder.Id).ToArray();
            Check(damage.Length == unprotected.Length && damage.Length > 1
                && damage[0].Damage == unprotected[0].Damage
                && Math.Abs(damage[1].Damage) < Math.Abs(unprotected[1].Damage), "real subsequent hit was not reduced");
            var status = holder.Statuses.SingleOrDefault(value => value.ClassId == 24229);
            Check(status != null, "missing stack after hits=" + hits + " casts="
                + string.Join(",", step.SkillCasts.Select(value => value.SkillEntityId))
                + " statuses=" + string.Join(",", holder.Statuses.Select(value => value.ClassId)));
            Check(!attacker.Statuses.Any(value => value.ClassId == 24229), "attacker gained the defensive stack");
            int expectedStacks = repeated ? 9 : 6;
            Check(hits == (repeated ? 12 : 6) && status.StackCount == expectedStacks && status.DurationRound == 1
                && status.MaxStackedCount == 9 && status.CanDispersed, "hit stack or duration differs: " + hits);
            Check(status.ApplyingSkillItemId == 10542 && status.SourceSkillRank == growth[0]
                && status.SourceSkillLevel == growth[1] && status.SourceSubRank == growth[2], "source cultivation differs");
            var configured = catalog.CalculateSkillProps(24229, growth[0], growth[1], growth[2]);
            foreach (int id in new[] { 232, 236 })
                Check(holder.Properties.Single(value => value.Id == id).Value == configured[id] * expectedStacks,
                    "stacked defensive property differs: " + id);
            executor.Step(checkpoint.Sequence, checkpoint.Counter + 1);
            Check(!holder.Statuses.Any(value => value.ClassId == 24229), "one personal round did not expire the stack");
            cases++;
        }
        foreach (string mode in new[] { "normal", "full-shield", "dodge", "blinding", "immunity", "lethal", "forbidden", "child-denied", "child-whitelist" })
        {
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20,
                RandomSeed = 17, Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var attacker = Unit(1, 5); var holder = Unit(2, 6);
            checkpoint.Units.AddRange(new[] { attacker, holder });
            var attack = catalog.CreateSkill(10600, 0, 10008, 7, 20, 8,
                catalog.CalculateSkillProps(10600, 7, 20, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            if (mode == "child-denied" || mode == "child-whitelist") attack.CanTriggerChild = false;
            if (mode == "child-whitelist") attack.CanTriggerStatusEntityIds.Add(25033);
            attacker.Skills.Add(attack); attacker.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
            var listener = catalog.CreateStatus(25033, holder.Id, holder.Id, 75033,
                25033, 10542, 7, 30, 8, holder.Properties);
            if (mode == "forbidden") listener.ForbiddenType = 1;
            holder.Statuses.Add(listener);
            if (mode == "full-shield")
            {
                var shield = catalog.CreateStatus(24435, holder.Id, holder.Id, 744435);
                shield.ShieldUsesCustomValue = true; shield.CustomShieldValue = 1000000;
                holder.Statuses.Add(shield);
            }
            if (mode == "dodge") holder.Properties.Add(new FightPropertyValue {
                Id = formula.PropertyId("DodgePercent"), Value = 10000 });
            if (mode == "blinding") attacker.Properties.Add(new FightPropertyValue {
                Id = formula.PropertyId("BlindingPercent"), Value = 10000 });
            if (mode == "immunity") holder.Statuses.Add(catalog.CreateStatus(24007, 2, 2, 724007));
            if (mode == "lethal") { holder.Hp = 1; holder.Turn.Hp = 1; }
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            var damage = step.DamageEvents.Single(value => value.SkillEntityId == 10600);
            bool expected = mode == "normal" || mode == "full-shield" || mode == "child-whitelist";
            Check(holder.Statuses.Any(value => value.ClassId == 24229) == expected, "received damage gate: " + mode);
            if (mode == "full-shield") Check(damage.ShieldDamage > 0 && damage.Damage == 0, "shield input");
            if (mode == "lethal") Check(holder.Hp == 0, "lethal input");
            cases++;
        }
        return "Stone Skin: " + cases + " stack cap, duration, cultivation, defensive property and checkpoint cases passed.";
    }
}
