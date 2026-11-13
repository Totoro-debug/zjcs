using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleShieldExpiryHarness
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static FightBattleUnitState Unit(FightOfficialCombatCatalog catalog, int id, int side, int x)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, X = x, Y = 5,
            Hp = 500000000, MaxHp = 1000000000, LastMaxHp = 1000000000,
            Level = 150, SubRank = 1, CharacterType = 1 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 1;
        foreach (var property in new[] { new FightPropertyValue { Id = 1, Value = 10001 },
            new FightPropertyValue { Id = 2, Value = 0 }, new FightPropertyValue { Id = 3, Value = unit.MaxHp },
            new FightPropertyValue { Id = 4, Value = unit.Turn.Speed } }) unit.Properties.Add(property);
        foreach (string name in new[] { "BaseEffectRate", "BaseEffectDodge", "BaseCureAdd", "BaseBeCureAdd" })
            unit.Properties.Add(new FightPropertyValue { Id = catalog.PropertyType(name), Value = 10000 });
        return unit;
    }

    static FightBattleCheckpoint Checkpoint(FightOfficialCombatCatalog c)
    {
        var cp = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 73, MaxRound = 20,
            Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 0 } };
        cp.Units.Add(Unit(c, 1, 1, 5)); cp.Units.Add(Unit(c, 2, 2, 6));
        return cp;
    }

    static FightBattleStatusState Shield(FightOfficialCombatCatalog c, FightBattleUnitState owner,
        int instanceId, long remaining)
    {
        var status = c.CreateStatus(24435, owner.Id, owner.Id, instanceId, 0, 10576, 2, 20, 1,
            customShieldValue: 100000);
        status.ShieldRemainValue = remaining;
        status.ShieldInitialized = true;
        owner.Statuses.Add(status);
        return status;
    }

    public static void Run(FightOfficialCombatCatalog c, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        // Tick the original owner clock, then persist the boundary between removal and its skill.
        foreach (int filter in new[] { 0, 24223, 25136 })
        foreach (long remaining in new long[] { 0, 23457, 100000 })
        {
            var cp = Checkpoint(c); var owner = cp.Units[0]; var status = Shield(c, owner, 101, remaining);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), c, items);
            Check(status.DurationRound == 2 && status.RoundTarget == 2 && status.RoundUpdateTiming == 1
                && status.Components.ShieldExpiry.Supported && status.Components.ShieldExpiry.Skill.EntityId == 12750,
                "Original duration or expiry definition differs");
            var copied = status.Components.Copy(); copied.ShieldExpiry.Skill.EntityId = 99;
            Check(status.Components.ShieldExpiry.Skill.EntityId == 12750
                && !ReferenceEquals(copied.ShieldExpiry.Skill.TargetWithoutStatusIds, status.Components.ShieldExpiry.Skill.TargetWithoutStatusIds),
                "Copied shield expiry shares its trigger configuration");
            Check(FightStatusLifecycle.TickTurn(owner, cp.Units, 1, FightStatusLifecycle.TimingEnd) == 0,
                "Shield expired before its original two owner rounds");
            Check(FightStatusLifecycle.TickTurn(owner, cp.Units, 2, FightStatusLifecycle.TimingStart) == 0,
                "Shield expired at round start");
            Check(FightStatusLifecycle.TickTurn(owner, cp.Units, 2, FightStatusLifecycle.TimingEnd) == 1,
                "Shield did not expire at the original owner round end");
            Check(!FightStatusLifecycle.End(owner, status) && owner.EndedShieldExpiryStatuses.Count == (remaining > 0 ? 1 : 0),
                "Duplicate end queued another cure");
            if (filter != 0) owner.Statuses.Add(c.CreateStatus(filter, 1, 1, 102, 0, 0, 2, 20, 1));
            foreach (var pending in owner.EndedShieldExpiryStatuses) pending.Components.ShieldExpiry = null;
            cp = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            owner = cp.Units[0]; long before = owner.Hp;
            executor = FightBattleExecutor.Restore(cp, formula, new FightDamageSettlement(), c, items);
            var step = executor.Step(0, 1).Event;
            var cures = step.DamageEvents.Where(e => e.SkillEntityId == 12750 && e.IsCure).ToArray();
            Check(cures.Length == (remaining > 0 ? 1 : 0) && owner.EndedShieldExpiryStatuses.Count == 0,
                "Pending checkpoint lost or duplicated the original cure");
            if (remaining > 0)
            {
                long expected = remaining;
                if (filter == 25136)
                {
                    var control = Checkpoint(c); var source = control.Units[0];
                    source.Statuses.Add(c.CreateStatus(25136, 1, 1, 401, 0, 0, 2, 20, 1));
                    var original = c.CreateSkill(12750, 0, 10576, 2, 20, 1,
                        c.CalculateSkillProps(12750, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
                    original.HasCustomCure = true; original.CustomCure = remaining; original.LimitedTimes = 1;
                    source.Skills.Add(original); source.SkillState.Slots.Add(c.CreateSkillSlot(original, 0));
                    var direct = FightBattleExecutor.Begin(control, formula, new FightDamageSettlement(), c, items).Step(0, 1).Event;
                    expected = direct.DamageEvents.Where(e => e.SkillEntityId == 12750 && e.IsCure).Sum(e => e.AfterHp - e.BeforeHp);
                    Check(expected > remaining, "The original 25136 healing modifier control did not exercise its buff");
                }
                Check(cures[0].SourceId == 1 && cures[0].TargetId == 1
                    && cures[0].AfterHp - cures[0].BeforeHp == (filter == 24223 ? 0 : expected),
                    "Remaining shield, self target or cure interception differs: " + filter + "/" + remaining
                        + "/actual=" + (cures[0].AfterHp - cures[0].BeforeHp));
                var cast = step.SkillCasts.Single(e => e.SkillEntityId == 12750);
                Check(cast.Slot == -1 && cast.SkillItemId == 10576,
                    "Expiry cure occupied an active skill slot or lost its original status source");
            }
            Check(!owner.Statuses.Any(s => s.ClassId == 24435), "12750 incorrectly fed back into cure-to-shield");
            string settled = FightBattleCheckpointSerializer.Save(cp);
            executor.Step(0, 1); Check(FightBattleCheckpointSerializer.Save(cp) == settled, "Receipt replay changed settled state");
            cp = FightBattleCheckpointSerializer.Load(settled);
            executor = FightBattleExecutor.Restore(cp, formula, new FightDamageSettlement(), c, items);
            Check(!executor.Step(cp.Sequence, cp.Counter + 1).Event.SkillCasts.Any(e => e.SkillEntityId == 12750), "Settled checkpoint replayed expiry cure");
        }

        foreach (bool dead in new[] { false, true })
        {
            var cp = Checkpoint(c); var owner = cp.Units[0]; var status = Shield(c, owner, 201, 12345);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), c, items);
            if (dead) { FightStatusLifecycle.TickTurn(owner, cp.Units, 2, FightStatusLifecycle.TimingEnd); owner.Hp = 0; owner.Turn.Hp = 0; }
            else Check(FightStatusLifecycle.End(owner, status), "Forced removal failed");
            var step = executor.Step(0, 1).Event;
            Check(!step.SkillCasts.Any(e => e.SkillEntityId == 12750), "Forced removal or dead owner received expiry healing");
        }

        // Real executor round-end integration, parallel shields, legacy definition restoration and full-HP clipping.
        foreach (bool full in new[] { false, true })
        {
            var cp = Checkpoint(c); var owner = cp.Units[0];
            for (int i = 0; i < 6; i++) Shield(c, owner, 301 + i, 1000 + i);
            if (full) owner.Hp = owner.Turn.Hp = owner.MaxHp;
            FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), c, items);
            foreach (var status in owner.Statuses) status.Components.ShieldExpiry = null;
            string legacy = FightBattleCheckpointSerializer.Save(cp)
                .Replace("\"EndedShieldExpiryStatuses\":[],", "").Replace("\"ShieldExpiry\":null,", "");
            cp = FightBattleCheckpointSerializer.Load(legacy);
            var executor = FightBattleExecutor.Restore(cp, formula, new FightDamageSettlement(), c, items);
            Check(cp.Units[0].Statuses.All(s => s.Components.ShieldExpiry.Supported), "Legacy definition did not restore");
            int casts = 0; long healed = 0;
            for (int i = 0; i < 12 && !cp.Finished; i++)
            {
                var step = executor.Step(cp.Sequence, cp.Counter + 1).Event;
                casts += step.SkillCasts.Count(e => e.SkillEntityId == 12750);
                healed += step.DamageEvents.Where(e => e.SkillEntityId == 12750).Sum(e => e.AfterHp - e.BeforeHp);
            }
            Check(casts == 6 && healed == (full ? 0 : 6015), "Executor expiry, parallel shields or HP clipping failed: " + casts + "/" + healed);
        }

        {
            var cp = Checkpoint(c); var owner = cp.Units[0]; Shield(c, owner, 501, 1234);
            var original = c.CreateSkill(10500, 0, 10000, 2, 20, 1,
                c.CalculateSkillProps(10500, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            owner.Skills.Add(original); owner.SkillState.Slots.Add(c.CreateSkillSlot(original, 0));
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), c, items);
            bool observed = false;
            for (int i = 0; i < 8 && !cp.Finished; i++)
            {
                var step = executor.Step(cp.Sequence, cp.Counter + 1).Event;
                var cure = step.SkillCasts.SingleOrDefault(cast => cast.SkillEntityId == 12750);
                if (cure == null) continue;
                var active = step.SkillCasts.Single(cast => cast.ActorId == 1 && cast.Slot == 0);
                Check(active.Presentation.ActionDuration > 0
                    && Math.Abs(cure.Delay - (active.Delay + active.Presentation.ActionDuration)) < .0001f,
                    "Expiry cure did not retain the original active action-end boundary");
                observed = true; break;
            }
            Check(observed, "Original active action and shield expiry never coincided");
        }
    }
}
