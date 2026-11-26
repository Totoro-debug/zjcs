using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleQuickCastingHarness
{
    static void Check(bool condition, string detail)
    { if (!condition) throw new InvalidOperationException("Quick casting: " + detail); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 3, 4, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int subRank in new[] { 5, 8, 26, 40 })
        foreach (int seed in new[] { 7, 37 })
        {
            var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 10, formula);
            actor.SubRank = subRank;
            var enemy = Unit(2, 2, 12, formula);
            var ally = Unit(3, 1, 14, formula);
            cp.Units.Add(actor); cp.Units.Add(enemy); cp.Units.Add(ally);
            foreach (var unit in cp.Units)
                for (int index = 0; index < 4; index++)
                {
                    var skill = catalog.CreateSkill(12070, index, 10098, rank, level, subRank,
                        catalog.CalculateSkillProps(12070, rank, level, subRank)
                            .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                    unit.Skills.Add(skill);
                    var slot = catalog.CreateSkillSlot(skill, index);
                    slot.Cooldown = index == 3 ? 0 : index == 1 ? 1 : 3;
                    slot.LastRound = index == 0 ? -4 : 0;
                    slot.HasInitializedLastRound = true;
                    unit.SkillState.Slots.Add(slot);
                }
            var status = catalog.CreateStatus(25113, 1, 1, 1, applyingSkillItemId: 10604,
                sourceSkillRank: rank, sourceSkillLevel: level, sourceSubRank: subRank);
            actor.Statuses.Add(status);
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            restored.Step(saved.Sequence, saved.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved),
                "restoring before the opener changed its effects");
            var cast = step.SkillCasts.Single(value => value.SkillEntityId == 12150);
            Check(cast.ActorId == 1 && cast.SkillItemId == 10604, "the opener lost its passive item source");
            Check(step.SkillCasts.Count == 1 && step.DamageEvents.All(value => value.Damage == 0)
                && cp.Units.All(value => value.Hp == 1000000), "the self opener dealt damage or consumed an active slot");
            Check(actor.SkillState.Round == 0 && actor.SkillState.Slots.All(value => value.ReleasedTimes == 0),
                "opening secret advanced the owner's round or consumed an active release");
            Check(actor.SkillState.Slots.Select(value => value.LastRound).SequenceEqual(new[] { -4, -1, -1, 0 }),
                "cooldowns did not decrease once, or a ready/zero-CD skill changed");
            Check(enemy.SkillState.Slots.Select(value => value.LastRound).SequenceEqual(new[] { -4, 0, 0, 0 })
                && ally.SkillState.Slots.Select(value => value.LastRound).SequenceEqual(new[] { -4, 0, 0, 0 }),
                "the self status changed an ally or opponent cooldown");
            Check(actor.Statuses.Count == 1 && actor.Statuses[0].ClassId == 25113
                && actor.Statuses[0].RoundStartTriggeredCount == 1,
                "the retained source or removed cooldown sub-status differs from official configuration");
            var action = step.StatusCustomActions.Single(value => value.UnitId == 1 && value.Action == 3);
            Check(action.HasDelay && Math.Abs(action.Delay - cast.Delay - .35f) < .0001f,
                "cooldown change lost the official 12151 hit delay");
            Check(cp.Units.All(value => value.MaxHp == 1000000)
                && enemy.Statuses.Count == 0 && ally.Statuses.Count == 0,
                "the opener added an unconfigured persistent buff or shield");
            string after = FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(cp.Sequence, cp.Counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == after,
                "a replay executed the opening cooldown reduction twice");
            var afterState = FightBattleCheckpointSerializer.Load(after);
            var afterRestore = FightBattleExecutor.Restore(afterState, formula, new FightDamageSettlement(), catalog, items);
            Check(afterRestore.Step(afterState.Sequence, afterState.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(afterState) == after,
                "restoring the opening receipt repeated its cooldown change");
            var next = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            afterRestore.Step(afterState.Sequence, afterState.Counter + 1);
            Check(next.SkillCasts.All(value => value.SkillEntityId != 12150)
                && next.StatusCustomActions.All(value => value.Action != 3)
                && FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(afterState),
                "a later round or restored source repeated the opening reduction");
            cases++;
        }
        CheckGlobalRoundBoundaries(catalog, formula, items);
        return "Quick casting passed: " + cases + " opening self cooldown, ready-slot, timing and checkpoint cases; global round invocation quotas and restore boundaries.";
    }

    static void CheckGlobalRoundBoundaries(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (bool atStart in new[] { false, true })
        foreach (int maximum in new[] { 1, 2, -1 })
        {
            var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 7,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 10, formula); cp.Units.Add(actor);
            cp.Units.Add(Unit(2, 2, 12, formula));
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var skill = catalog.CreateSkill(12070, 0, 10098, 3, 1, 8,
                catalog.CalculateSkillProps(12070, 3, 1, 8)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            actor.Skills.Add(skill);
            var slot = catalog.CreateSkillSlot(skill, 0);
            slot.Cooldown = 100; slot.LastRound = 0; slot.HasInitializedLastRound = true;
            actor.SkillState.Slots.Add(slot);
            var status = catalog.CreateStatus(24389, 1, 1, 1, applyingSkillItemId: 10604);
            // Exercise the shared scheduler with controlled quotas, independently
            // of 10604's fixed one-shot configuration tested above.
            status.DurationRound = -1;
            var info = status.Components.GlobalRoundUpdate;
            info.TryAtStart = atStart; info.MaxInvokeCount = maximum;
            actor.Statuses.Add(status);
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            for (int step = 0; step < 8 && !cp.Finished; step++)
            {
                var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
                runner.Step(cp.Sequence, cp.Counter + 1);
                restored.Step(saved.Sequence, saved.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved),
                    "global round restoration lost invocation quota or clock");
                int globalRound = FightTurnQueue.GlobalRound(cp.Units.Select(value => value.Turn).ToArray());
                int opportunities = Math.Max(0, globalRound - 1) + (atStart ? 1 : 0);
                int expected = maximum < 0 ? opportunities : Math.Min(maximum, opportunities);
                Check(info.InvokeCount == expected && slot.LastRound == -expected,
                    "global round status repeated a round or ignored its invocation limit: "
                    + atStart + "/" + maximum + "/" + globalRound + "/" + info.InvokeCount + "/" + expected);
                Check(actor.Statuses.Contains(status) == (maximum < 0 || expected < maximum),
                    "global round exhaustion did not preserve unlimited or remove exhausted status");
            }
        }
    }

    static FightBattleUnitState Unit(int id, int side, int x, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 12,
            Level = 70, SubRank = 8, X = x, Y = 10, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        foreach (var prop in new[] { new FightPropertyValue { Id = 1, Value = 1000 },
            new FightPropertyValue { Id = 2, Value = 10 }, new FightPropertyValue { Id = 3, Value = 1000000 },
            new FightPropertyValue { Id = 4, Value = 1000 } }) unit.Properties.Add(prop);
        foreach (string name in new[] { "BaseCritRatePercentValue", "BaseCritAvoidPercentValue", "CritAvoidPercent",
            "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseElementMaster", "BaseElementResistance",
            "BaseElementAdd", "BaseElementReduce" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        unit.BaseProperties = unit.Properties.Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList();
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = 1000; unit.Turn.RankSpeedScale = 10000;
        return unit;
    }
}
