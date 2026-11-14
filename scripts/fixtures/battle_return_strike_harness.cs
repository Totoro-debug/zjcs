using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleReturnStrikeHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Return strike: " + message); }

    private static FightBattleUnitState Unit(int id, int x)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
            Profession = 2, Level = 50, SubRank = 5, X = x, Y = 5,
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
        FightBattleUnitState unit, int rank, int level, int subRank)
        => catalog.CreateStatus(25003, unit.Id, unit.Id, 75000 + unit.Id, 25003, 10509,
            rank, level, subRank, unit.Properties);

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int seed in new[] { 1, 17, 50000, 100000 })
        foreach (int holders in new[] { 1, 2, 3 }) // target, attacker, both
        foreach (string mode in new[] { "normal", "full-shield", "dodge", "blinding", "immunity",
            "lethal", "forbidden", "empty-stack", "child-denied", "child-whitelist", "cure", "pure-status" })
        {
            string context = mode + "/" + holders + "/" + seed;
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20,
                RandomSeed = seed, Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var actor = Unit(1, 5); var target = Unit(2, 6);
            checkpoint.Units.AddRange(new[] { actor, target });
            var skill = Slash(catalog);
            if (mode == "child-denied" || mode == "child-whitelist") skill.CanTriggerChild = false;
            if (mode == "child-whitelist") skill.CanTriggerStatusEntityIds.Add(25003);
            if (mode == "cure" || mode == "pure-status")
                foreach (var hit in skill.HitGraphEntities.Where(value => value.HasFixedDamage))
                    hit.ActionType = mode == "cure" ? 5 : 0;
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            if ((holders & 1) != 0)
            {
                var secret = Secret(catalog, target, 7, 30, 8);
                if (mode == "forbidden") secret.ForbiddenType = 1;
                if (mode == "empty-stack") secret.StackCount = 0;
                target.Statuses.Add(secret);
            }
            if ((holders & 2) != 0) actor.Statuses.Add(Secret(catalog, actor, 3, 21, 2));
            if (mode == "full-shield")
            {
                var shield = catalog.CreateStatus(24435, target.Id, target.Id, 744435);
                shield.ShieldUsesCustomValue = true; shield.CustomShieldValue = 1000000;
                target.Statuses.Add(shield);
            }
            if (mode == "dodge") target.Properties.Add(new FightPropertyValue {
                Id = formula.PropertyId("DodgePercent"), Value = 10000 });
            if (mode == "blinding") actor.Properties.Add(new FightPropertyValue {
                Id = formula.PropertyId("BlindingPercent"), Value = 10000 });
            if (mode == "immunity") target.Statuses.Add(catalog.CreateStatus(24007, 2, 2, 724007));
            if (mode == "lethal") { target.Hp = 1; target.Turn.Hp = 1; }
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var resume = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resume.Step(restored.Sequence, restored.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restored),
                "initial checkpoint restore diverged: " + context);
            var damage = step.DamageEvents.Single(value => value.SkillEntityId == 10600);
            bool eligible = (holders & 1) != 0 && (mode == "normal" || mode == "full-shield" || mode == "child-whitelist");
            var buffs = target.Statuses.Where(value => value.ClassId == 24268).ToArray();
            Check(buffs.Length == (eligible ? 1 : 0), "received-damage gate differs: " + context);
            Check(!actor.Statuses.Any(value => value.ClassId == 24268), "attacker received a defensive stack: " + context);
            if (eligible)
            {
                var buff = buffs.Single();
                Check(buff.OwnerId == 2 && buff.CreatorId == 2 && buff.ApplyingSkillItemId == 10509
                    && buff.RootSkillId == 25003 && buff.SourceSkillRank == 7 && buff.SourceSkillLevel == 30
                    && buff.SourceSubRank == 8 && buff.StackCount == 1
                    && buff.Components.HitDmgAddPer.DurationSkillCount == 1,
                    "stack inherited the incoming attack/other holder instead of its secret: " + context);
            }
            if (mode == "full-shield") Check(damage.ShieldDamage > 0 && damage.Damage == 0
                && target.Hp == 1000000, "full absorption was not actually exercised");
            if (mode == "lethal") Check(target.Hp == 0, "lethal input did not actually kill");
            if (mode == "cure") Check(damage.IsCure, "healing input did not execute cure settlement");
            if (mode == "pure-status") Check(!damage.HasDamageValue && !damage.HasAbsoluteDamage,
                "status-only input acquired a damage result");
            string settled = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == settled, "replayed receipt repeated a stack");
            cases++;
        }
        int sequences = StackAndCast(catalog, formula, items);
        return "Return strike passed: " + cases + " received-damage/source/checkpoint/replay cases; "
            + sequences + " twelve-hit/cap/growth/next-cast sequences.";
    }

    private static int StackAndCast(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int sequences = 0;
        foreach (int seed in new[] { 1, 17, 50000 })
        foreach (int[] growth in new[] { new[] { 1, 1, 1 }, new[] { 3, 20, 2 },
            new[] { 7, 30, 8 }, new[] { 10, 50, 12 } })
        {
            string context = seed + "/" + string.Join("/", growth);
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 100,
                RandomSeed = seed, Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var actor = Unit(1, 5); var target = Unit(2, 6);
            checkpoint.Units.AddRange(new[] { actor, target });
            var skill = Slash(catalog);
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            target.Statuses.Add(Secret(catalog, target, growth[0], growth[1], growth[2]));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            int received = 0;
            for (int steps = 0; steps < 150 && received < 12; steps++)
            {
                var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                received += step.DamageEvents.Count(value => value.SkillEntityId == 10600);
                if (received == 0) continue;
                var buff = target.Statuses.Single(value => value.ClassId == 24268);
                Check(buff.StackCount == Math.Min(received, 10)
                    && buff.Components.HitDmgAddPer.DurationSkillCount == 1,
                    "stack cap or empty-plan preservation failed: " + context + "/" + received);
                if (received == 6)
                {
                    string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                    checkpoint = FightBattleCheckpointSerializer.Load(saved);
                    executor = FightBattleExecutor.Restore(checkpoint, formula, new FightDamageSettlement(), catalog, items);
                    actor = checkpoint.Units.Single(value => value.Id == 1);
                    target = checkpoint.Units.Single(value => value.Id == 2);
                    Check(FightBattleCheckpointSerializer.Save(checkpoint) == saved,
                        "mid-stack restore changed state: " + context);
                }
            }
            Check(received == 12, "twelve real incoming hits did not execute: " + context);
            var stacked = target.Statuses.Single(value => value.ClassId == 24268);
            var expected = catalog.CalculateSkillProps(24268, growth[0], growth[1], growth[2]);
            var response = Slash(catalog);
            var originalProperties = target.Properties.Select(value => value.Id + ":" + value.Value).ToArray();
            var cast = new FightConditionalHitProperties(target, response, items);
            foreach (var hit in FightBattleHitCollector.CollectAll(response))
            {
                var temporary = new Dictionary<int, long>();
                cast.Merge(actor, hit, new FightDamageTypeData(), false, null, temporary);
                foreach (int id in new[] { 231, 235 })
                    Check(temporary.TryGetValue(id, out long value) && value == expected[id] * 10,
                        "ten-layer original increment differs: " + context + "/" + id
                            + " expected=" + expected[id] * 10 + " actual=" + value
                            + " hit=" + hit.EntityId + " raw=" + string.Join(",", stacked.StatusProperties.Select(p => p.Id + ":" + p.Value)));
                Check(target.Statuses.Contains(stacked)
                    && stacked.Components.HitDmgAddPer.DurationSkillCount == 1,
                    "a damage segment consumed the cast before its end: " + context);
            }
            Check(originalProperties.SequenceEqual(target.Properties.Select(value => value.Id + ":" + value.Value)),
                "temporary damage increments changed permanent properties: " + context);
            foreach (int id in new[] { 231, 235 })
                Check(stacked.StatusProperties.Single(value => value.Id == id).Value == expected[id],
                    "stack multiplication leaked into the unscaled status dictionary: " + context);

            // Install the response only after the holder has accumulated ten real layers.
            target.Skills.Add(response); target.SkillState.Slots.Add(catalog.CreateSkillSlot(response, 0));
            string ready = FightBattleCheckpointSerializer.Save(checkpoint);
            foreach (string durationCase in new[] { "miss", "no-hit", "cure-type", "other-action", "forbidden" })
            {
                var isolated = FightBattleCheckpointSerializer.Load(ready);
                var holder = isolated.Units.Single(value => value.Id == 2);
                var held = holder.Statuses.Single(value => value.ClassId == 24268);
                var attempted = Slash(catalog);
                if (durationCase == "cure-type") attempted.SkillType = 3;
                if (durationCase == "other-action") attempted.ActionType = 99;
                if (durationCase == "forbidden") held.ForbiddenType = 1;
                var invocation = new FightConditionalHitProperties(holder, attempted, items);
                if (durationCase == "miss")
                {
                    var properties = new Dictionary<int, long>();
                    invocation.Merge(isolated.Units.Single(value => value.Id == 1),
                        FightBattleHitCollector.CollectAll(attempted).First(),
                        new FightDamageTypeData { Dodge = true }, false, null, properties);
                    Check(properties.Count == 0, "missed response received conditional damage properties");
                }
                invocation.EndCast();
                bool consumes = durationCase == "miss" || durationCase == "no-hit";
                Check(holder.Statuses.Contains(held) != consumes,
                    "configured cast-duration gate differs: " + context + "/" + durationCase);
            }
            var resumed = FightBattleCheckpointSerializer.Load(ready);
            var control = FightBattleCheckpointSerializer.Load(ready);
            control.Units.Single(value => value.Id == 2).Statuses.RemoveAll(value => value.ClassId == 24268);
            var resumedExecutor = FightBattleExecutor.Restore(resumed, formula, new FightDamageSettlement(), catalog, items);
            var controlExecutor = FightBattleExecutor.Restore(control, formula, new FightDamageSettlement(), catalog, items);
            var next = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resumedExecutor.Step(resumed.Sequence, resumed.Counter + 1);
            var plain = controlExecutor.Step(control.Sequence, control.Counter + 1).Event;
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(resumed),
                "next-cast checkpoint restore diverged: " + context);
            Check(next.DamageEvents.Count == 1 && plain.DamageEvents.Count == 1
                && Math.Abs(next.DamageEvents[0].Damage) > Math.Abs(plain.DamageEvents[0].Damage),
                "next real cast did not receive the accumulated damage increase: " + context);
            Check(!target.Statuses.Any(value => value.ClassId == 24268),
                "next real cast did not consume the complete buff: " + context);
            string settled = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == settled,
                "response replay repeated its damage or consumption: " + context);
            sequences++;
        }
        return sequences;
    }
}
