using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSummonFuryHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Summon fury: " + message); }

    static FightBattleUnitState Unit(int id, int side, int x, int speed)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side,
            CharacterType = side == 1 ? 1 : 2, X = x, Y = 5, MoveMask = 5,
            Hp = 1000000000, MaxHp = 1000000000, LastMaxHp = 1000000000,
            Level = 150, SubRank = 1,
            Turn = new FightTurnUnit { Id = id, Hp = 1000000000, Speed = speed } };
        unit.Properties.Add(new FightPropertyValue { Id = 1, Value = 10001 });
        unit.Properties.Add(new FightPropertyValue { Id = 2, Value = 10003 });
        unit.Properties.Add(new FightPropertyValue { Id = 3, Value = 1000000000 });
        unit.Properties.Add(new FightPropertyValue { Id = 4, Value = speed });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var item = items.Required(10527);
        Check(item.EntityId == 25012 && item.PassiveStatusIds.SequenceEqual(new[] { 25012 }),
            "the official passive root changed");
        int[] subRanks = { 5, 8, 11, 14, 17, 20, 23, 26 };
        int[] ranks = { 1, 2, 4, 7, 26 };
        int[] levels = { 1, 2, 20 };
        long[,] fixedRows = { { 40, 50, 254 }, { 82, 165, 1700 },
            { 315, 630, 5778 }, { 715, 858, 3402 },
            { 21353, 21385, 22002 }, { 61006, 61074, 62361 },
            { 153507, 153660, 156450 }, { 347850, 348175, 353336 } };
        long[] fixedRanks = { 2400, 3200, 6600, 14400, 187600 };
        long[] scaleRanks = { 6300, 6300, 7700, 8900, 11800 };
        int cases = 0;
        for (int s = 0; s < subRanks.Length; s++)
        for (int r = 0; r < ranks.Length; r++)
        for (int l = 0; l < levels.Length; l++)
        {
            int rank = ranks[r], level = levels[l], subRank = subRanks[s];
            long fixedAttack = (long)((long)(fixedRows[s, l] * (fixedRanks[r] / 10000.0)) * .2);
            long attackScale = (long)((long)(10000 * (scaleRanks[r] / 10000.0)) * .15);
            var values = catalog.CalculateSkillProps(24070, rank, level, subRank);
            Check(values.Count == 2 && values[130] == fixedAttack && values[251] == attackScale,
                "official fixed/percentage cultivation or truncation changed");
            var owner = Unit(1, 1, 5, 1000);
            owner.SubRank = subRank;
            var enemy = Unit(2, 2, 6, 1);
            var passive = catalog.CreateStatus(25012, 1, 1, 1, 25012, 10527,
                rank, level, subRank, owner.Properties);
            owner.Statuses.Add(passive);
            var skill = catalog.CreateSkill(12140, 0, 10095, rank, level, subRank, null);
            owner.Skills.Add(skill);
            owner.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = skill.LimitedTimes });
            var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 13,
                Grid = new FightBattleGrid { Width = 20, Height = 20 },
                ResultRules = new FightBattleResultRules(), MaxRound = 30 };
            state.Units.Add(owner); state.Units.Add(enemy);
            var battle = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            battle.Step(0, 1);
            var summon = state.Units.Single(unit => unit.MasterId == owner.Id);
            var buff = summon.Statuses.Single(status => status.ClassId == 24070);
            Check(passive.Components.HitSummon.TriggeredCount == 1
                && !passive.CanDispersed && passive.DurationRound == -1
                && buff.DurationRound == -1 && !buff.CanDispersed && !buff.IsOpenStack
                && buff.CreatorId == owner.Id && buff.OwnerId == summon.Id
                && buff.ApplyingSkillItemId == 10527 && buff.RootSkillId == 25012
                && buff.SourceSkillRank == rank && buff.SourceSkillLevel == level
                && buff.SourceSubRank == subRank,
                "creation, ownership, cultivation or permanent status changed");
            long original = summon.BaseProperties.Where(value => value.Id == 1).Sum(value => value.Value);
            long baseAttack = summon.BaseProperties.Where(value => value.Id == formula.PropertyId("BaseAttack"))
                .Sum(value => value.Value);
            long expected = original + (long)((original + baseAttack) * (attackScale / 10000.0)) + fixedAttack;
            Check(summon.Properties.Single(value => value.Id == 1).Value == expected,
                "the summon did not receive the original attack percentage followed by fixed attack");
            Check(!owner.Statuses.Any(status => status.ClassId == 24070)
                && !enemy.Statuses.Any(status => status.ClassId == 24070), "the bonus leaked to a nonsummon");

            string saved = FightBattleCheckpointSerializer.Save(state);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            Check(resumed.Step(restored.Sequence, restored.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == saved,
                "checkpoint replay reapplied the creation bonus");
            for (int turn = 0; turn < 12 && !state.Finished; turn++)
            {
                battle.Step(state.Sequence, state.Counter + 1);
                resumed.Step(restored.Sequence, restored.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(restored),
                    "restored summon attacks or status lifetime diverged");
            }
            Check(summon.Statuses.Count(status => status.ClassId == 24070) == 1
                && passive.Components.HitSummon.TriggeredCount == 1,
                "turns duplicated the permanent summon bonus");
            cases++;
        }
        return "Summon fury passed: " + cases + " cultivated official summons, permanent attack bonuses, ownership and checkpoint replay.";
    }
}
