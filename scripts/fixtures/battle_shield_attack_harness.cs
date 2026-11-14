using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zjcs.Combat.Official;

public static class BattleShieldAttackHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Shield attack: " + message); }

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

    private static FightBattleStatusState Secret(FightOfficialCombatCatalog catalog,
        FightBattleUnitState owner, int[] growth, int instance = 75020)
        => catalog.CreateStatus(25020, owner.Id, owner.Id, instance, 25020, 10540,
            growth[0], growth[1], growth[2], owner.BaseProperties ?? owner.Properties);

    private static FightBattleStatusState Shield(FightOfficialCombatCatalog catalog,
        FightBattleUnitState owner, long value, int instance = 74435)
    {
        var status = catalog.CreateStatus(24435, owner.Id, owner.Id, instance);
        status.ShieldUsesCustomValue = true; status.CustomShieldValue = value;
        return status;
    }

    private static FightBattleSkillDefinition Slash(FightOfficialCombatCatalog catalog)
        => catalog.CreateSkill(10600, 0, 10008, 7, 20, 5,
            catalog.CalculateSkillProps(10600, 7, 20, 5)
                .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());

    private static FightBattleCheckpoint Checkpoint(int seed)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20,
            RandomSeed = seed, Grid = new FightBattleGrid { Width = 20, Height = 20 } };
        checkpoint.Units.AddRange(new[] { Unit(1), Unit(2) });
        return checkpoint;
    }

    private static FightBattleStatusState Verify(FightBattleUnitState holder,
        FightOfficialCombatCatalog catalog, FightDamageFormula formula, int[] growth, bool expected)
    {
        var buffs = holder.Statuses.Where(status => status.ClassId == 24212).ToArray();
        Check(buffs.Length == (expected ? 1 : 0), "shield presence and bonus differ");
        long attack = holder.Properties.Single(value => value.Id == formula.PropertyId("Attack")).Value;
        Check(expected ? attack > 1000 : attack == 1000, "attack contribution was not rebuilt immediately");
        if (!expected) return null;
        var buff = buffs.Single();
        Check(buff.OwnerId == holder.Id && buff.CreatorId == holder.Id && buff.RootSkillId == 25020
            && buff.ApplyingSkillItemId == 10540 && buff.SourceSkillRank == growth[0]
            && buff.SourceSkillLevel == growth[1] && buff.SourceSubRank == growth[2]
            && buff.IsActionExistGenerated && buff.ActionExistSourceInstanceId > 0,
            "bonus lost secret cultivation or source identity");
        var original = catalog.CalculateSkillProps(24212, growth[0], growth[1], growth[2]);
        Check(original.Count == buff.StatusProperties.Count && buff.StatusProperties.All(
            property => original.TryGetValue(property.Id, out long value) && value == property.Value),
            "bonus raw properties differ from original cultivation table");
        return buff;
    }

    // Exercise the exact delegates Step attaches, and inspect state before the enclosing
    // operation can hide a stale contribution with a later full battle-property sweep.
    private static void AttachLifecycle(FightBattleExecutor executor, FightBattleUnitState holder)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var start = typeof(FightBattleExecutor).GetMethod("OnStatusStarted", flags);
        var end = typeof(FightBattleExecutor).GetMethod("OnStatusEnded", flags);
        holder.StatusStarted = (unit, status) => start.Invoke(executor, new object[] { unit, status });
        holder.StatusEnded = (unit, status) => end.Invoke(executor, new object[] { unit, status });
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int transitions = 0, battles = 0;
        foreach (int seed in new[] { 1, 17, 50000 })
        foreach (int[] growth in new[] { new[] { 1, 1, 1 }, new[] { 3, 20, 2 },
            new[] { 7, 30, 8 }, new[] { 10, 50, 12 } })
        {
            var checkpoint = Checkpoint(seed);
            var holder = checkpoint.Units[1];
            var secret = Secret(catalog, holder, growth);
            holder.Statuses.Add(secret);
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            Verify(holder, catalog, formula, growth, false);
            AttachLifecycle(executor, holder);
            var shield = FightStatusLifecycle.Apply(holder, Shield(catalog, holder, 1000000), 0, 74435);
            var first = Verify(holder, catalog, formula, growth, true);
            var unrelated = catalog.CreateStatus(60220, holder.Id, holder.Id, 160220);
            holder.Statuses.Add(unrelated);
            var extra = FightStatusLifecycle.Apply(holder, Shield(catalog, holder, 1000000, 174435), 0, 174435);
            Check(!holder.Statuses.Any(status => status.ClassId == 60221),
                "the scoped lifecycle hook prematurely ran another action-presence producer");
            FightStatusTriggerRuntime.ApplyActionExistStatuses(checkpoint.Units, catalog, new FightRandomStream(seed));
            var unrelatedBonus = holder.Statuses.Single(status => status.ClassId == 60221);
            FightStatusLifecycle.End(holder, unrelated);
            FightStatusLifecycle.End(holder, unrelatedBonus);
            FightStatusLifecycle.End(holder, extra);
            shield.ForbiddenType = 1;
            FightStatusTriggerRuntime.ApplyActionExistStatuses(checkpoint.Units, catalog, new FightRandomStream(seed));
            FightStatusPropertyRuntime.RebuildAll(checkpoint.Units, catalog, formula);
            Check(Verify(holder, catalog, formula, growth, true) == first,
                "forbidden but present shield lost/recreated the bonus");
            shield.ShieldRemainValue = 0;
            FightStatusTriggerRuntime.ApplyActionExistStatuses(checkpoint.Units, catalog, new FightRandomStream(seed));
            Check(Verify(holder, catalog, formula, growth, true) == first,
                "remaining value overrode the confirmed status-presence rule");
            FightStatusLifecycle.End(holder, shield);
            Verify(holder, catalog, formula, growth, false);
            var a = FightStatusLifecycle.Apply(holder, Shield(catalog, holder, 1000000, 74436), 0, 74436);
            var b = FightStatusLifecycle.Apply(holder, Shield(catalog, holder, 1000000, 74437), 0, 74437);
            var second = Verify(holder, catalog, formula, growth, true);
            FightStatusLifecycle.End(holder, a);
            Check(Verify(holder, catalog, formula, growth, true) == second,
                "removing one of two shields recreated the maintained bonus");
            FightStatusLifecycle.End(holder, b);
            Verify(holder, catalog, formula, growth, false);
            FightStatusLifecycle.Apply(holder, Shield(catalog, holder, 1000000, 74438), 0, 74438);
            Verify(holder, catalog, formula, growth, true);
            FightStatusLifecycle.End(holder, secret);
            Verify(holder, catalog, formula, growth, false);
            FightStatusLifecycle.Apply(holder, Secret(catalog, holder, growth, 75021), 0, 75021);
            Verify(holder, catalog, formula, growth, true);
            holder.StatusStarted = null; holder.StatusEnded = null;
            transitions++;

            foreach (string mode in new[] { "break", "expire", "other-owner" })
            {
                checkpoint = Checkpoint(seed); holder = checkpoint.Units[1];
                var actor = checkpoint.Units[0];
                holder.Statuses.Add(Secret(catalog, holder, growth));
                (mode == "other-owner" ? actor : holder).Statuses.Add(
                    Shield(catalog, mode == "other-owner" ? actor : holder, mode == "break" ? 1 : 1000000));
                if (mode == "break")
                {
                    var attack = Slash(catalog); actor.Skills.Add(attack);
                    actor.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
                }
                executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
                Verify(holder, catalog, formula, growth, mode != "other-owner");
                bool removed = false;
                for (int index = 0; index < 10; index++)
                {
                    string before = FightBattleCheckpointSerializer.Save(checkpoint);
                    var restored = FightBattleCheckpointSerializer.Load(before);
                    var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
                    var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                    resumed.Step(restored.Sequence, restored.Counter + 1);
                    string after = FightBattleCheckpointSerializer.Save(checkpoint);
                    Check(after == FightBattleCheckpointSerializer.Save(restored), "restored lifecycle diverged: " + mode);
                    bool hasShield = holder.Statuses.Any(status => status.ActionType == 60);
                    Verify(holder, catalog, formula, growth, hasShield);
                    Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                        && FightBattleCheckpointSerializer.Save(checkpoint) == after, "replay changed shield bonus");
                    if (mode != "other-owner" && !hasShield)
                    {
                        Check(step.StatusViewEvents.Any(value => value.Kind == FightBattleStatusViewEventKind.End
                            && value.Status.ClassId == 24212), "bonus removal lacks an actual view transition");
                        if (mode == "break") Check(step.DamageEvents.Any(value => value.ShieldDamage == 1),
                            "break case did not actually break the shield");
                        removed = true; break;
                    }
                    if (mode == "other-owner") { removed = true; break; }
                }
                Check(removed, "original two-round shield did not end: " + mode);
                battles++;
            }
        }
        return "Shield attack passed: " + transitions + " immediate lifecycle/cultivation sequences; "
            + battles + " real break/expiry/ownership battles with restore and replay.";
    }
}
