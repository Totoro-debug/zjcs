using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattlePursuitHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Pursuit: " + message); }

    private static FightBattleUnitState Unit(int id)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
            Profession = 2, Level = 50, SubRank = 5, X = id + 4, Y = 5,
            Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var pair in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 },
            { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = pair.Key, Value = pair.Value });
        return unit;
    }

    private static FightBattleSkillDefinition Slash(FightOfficialCombatCatalog catalog)
        => catalog.CreateSkill(10600, 0, 10008, 7, 20, 5,
            catalog.CalculateSkillProps(10600, 7, 20, 5)
                .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());

    private static FightBattleStatusState Secret(FightOfficialCombatCatalog catalog,
        FightBattleUnitState owner, int[] growth)
        => catalog.CreateStatus(25008, owner.Id, owner.Id, 75008, 25008, 10516,
            growth[0], growth[1], growth[2], owner.Properties);

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int gates = 0, battles = 0;
        foreach (int[] growth in new[] { new[] { 1, 1, 1 }, new[] { 3, 20, 2 },
            new[] { 7, 30, 8 }, new[] { 10, 50, 12 } })
        foreach (string mode in new[] { "none", "positive", "negative", "abnormal", "both", "stacked",
            "own-only", "dodge", "blinding", "immunity", "cure", "child-denied", "secret-forbidden", "target-forbidden" })
        {
            var actor = Unit(1); var target = Unit(2); var skill = Slash(catalog);
            var secret = Secret(catalog, actor, growth); actor.Statuses.Add(secret);
            if (mode == "secret-forbidden") secret.ForbiddenType = 1;
            if (mode == "positive") target.Statuses.Add(catalog.CreateStatus(60421, 2, 2, 60421));
            if (mode == "abnormal" || mode == "both")
                target.Statuses.Add(catalog.CreateStatus(24020, 2, 1, 24020));
            if (mode != "none" && mode != "positive" && mode != "abnormal" && mode != "own-only")
            {
                var negative = catalog.CreateStatus(24009, 2, 1, 24009);
                if (mode == "stacked") negative.StackCount = 10;
                if (mode == "target-forbidden") negative.ForbiddenType = 1;
                target.Statuses.Add(negative);
            }
            if (mode == "own-only") actor.Statuses.Add(catalog.CreateStatus(24009, 1, 2, 24009));
            if (mode == "child-denied") skill.CanTriggerChild = false;
            FightStatusPropertyRuntime.RebuildAll(new[] { actor, target }, catalog, formula);
            var type = new FightDamageTypeData { Dodge = mode == "dodge", Blinding = mode == "blinding",
                Immunity = mode == "immunity" };
            var cast = new FightConditionalHitProperties(actor, skill, items);
            var original = catalog.CalculateSkillProps(25008, growth[0], growth[1], growth[2]);
            bool qualifies = new[] { "negative", "abnormal", "both", "stacked", "child-denied", "target-forbidden" }.Contains(mode);
            foreach (var hit in FightBattleHitCollector.CollectAll(skill))
            {
                var props = new Dictionary<int, long>();
                cast.Merge(target, hit, type, mode == "cure", null, props);
                Check(props.Count == (qualifies ? original.Count : 0), "condition mismatch: " + mode);
                if (qualifies) Check(original.All(pair => props.TryGetValue(pair.Key, out long value) && value == pair.Value),
                    "original increment scaled by debuff count/layers or lost cultivation: " + mode);
            }
            target.Statuses.Clear();
            var cleared = new Dictionary<int, long>();
            cast.Merge(target, FightBattleHitCollector.CollectAll(skill).First(), new FightDamageTypeData(), false, null, cleared);
            Check(cleared.Count == 0, "removed target condition retained temporary damage");
            cast.EndCast();
            Check(actor.Statuses.Contains(secret) && secret.Components.HitDmgAddPer.DurationSkillCount == -1,
                "permanent secret was consumed by a cast");
            Check(!actor.Properties.Any(pair => original.ContainsKey(pair.Id)), "conditional props leaked into permanent stats");
            gates++;
        }

        foreach (int seed in new[] { 1, 17, 50000 })
        foreach (int debuff in new[] { 24009, 24020 })
        foreach (bool forbidden in new[] { false, true })
        {
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10,
                RandomSeed = seed, Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var actor = Unit(1); var target = Unit(2);
            checkpoint.Units.AddRange(new[] { actor, target });
            actor.Statuses.Add(Secret(catalog, actor, new[] { 7, 30, 5 }));
            target.Statuses.Add(catalog.CreateStatus(debuff, 2, 1, debuff));
            if (forbidden) target.Statuses.Last().ForbiddenType = 1;
            var skill = Slash(catalog); actor.Skills.Add(skill);
            actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            string initial = FightBattleCheckpointSerializer.Save(checkpoint);
            var control = FightBattleCheckpointSerializer.Load(initial);
            control.Units[0].Statuses.RemoveAll(status => status.ClassId == 25008);
            var controlExecutor = FightBattleExecutor.Restore(control, formula, new FightDamageSettlement(), catalog, items);
            var first = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            var plain = controlExecutor.Step(control.Sequence, control.Counter + 1).Event;
            Check(first.DamageEvents.Sum(hit => -hit.Damage) > plain.DamageEvents.Sum(hit => -hit.Damage),
                "original secret did not increase real damage to a conditioned target");
            for (int index = 0; index < 6 && target.Statuses.Any(status => status.ClassId == debuff); index++)
                executor.Step(checkpoint.Sequence, checkpoint.Counter + 1);
            Check(!target.Statuses.Any(status => status.ClassId == debuff), "original one-round condition did not expire");
            string ready = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(ready);
            var resume = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            control = FightBattleCheckpointSerializer.Load(ready);
            control.Units[0].Statuses.RemoveAll(status => status.ClassId == 25008);
            controlExecutor = FightBattleExecutor.Restore(control, formula, new FightDamageSettlement(), catalog, items);
            var next = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resume.Step(restored.Sequence, restored.Counter + 1);
            var normal = controlExecutor.Step(control.Sequence, control.Counter + 1).Event;
            Check(next.DamageEvents.Select(hit => hit.Damage).SequenceEqual(normal.DamageEvents.Select(hit => hit.Damage)),
                "expired condition still increased real damage");
            string settled = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(settled == FightBattleCheckpointSerializer.Save(restored), "restore diverged after condition expiry");
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && settled == FightBattleCheckpointSerializer.Save(checkpoint), "replay advanced conditional counters");
            battles++;
        }
        return "Pursuit passed: " + gates + " original condition/cultivation cases; " + battles
            + " actual damage/expiry/restoration/replay battles including forbidden target states.";
    }
}
