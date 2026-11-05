using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattlePetSupportHarness
{
    private static void Require(bool value, string message)
    { if (!value) throw new Exception(message); }

    private static FightBattleCheckpoint Create(FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, int itemId, int limit = -1)
    {
        var owner = new FightBattleUnitState { Id = 1, Side = 1, CharacterType = 1,
            Level = 20, SubRank = 1, X = 5, Y = 5, Hp = 1000000, MaxHp = 1000000,
            Properties = new List<FightPropertyValue> {
                new FightPropertyValue { Id = 1, Value = 1000 },
                new FightPropertyValue { Id = 2, Value = 100 },
                new FightPropertyValue { Id = 3, Value = 1000000 },
                new FightPropertyValue { Id = 32, Value = 1000 },
                new FightPropertyValue { Id = 33, Value = 1000 },
                new FightPropertyValue { Id = 4, Value = 101 } },
            Turn = new FightTurnUnit { Id = 1, Hp = 1000000, Speed = 101 } };
        var normal = new FightBattleSkillDefinition { Slot = 0, EntityId = 7777,
            ItemId = 8888, RangeVerified = true, TargetType = 1, SkillType = 0,
            Range = new List<FightGridPoint> { new FightGridPoint(1, 0) } };
        owner.Skills.Add(normal);
        owner.SkillState.Slots.Add(catalog.CreateSkillSlot(normal, 0));
        var item = items.Required(itemId);
        var skill = catalog.CreateSkill(item.EntityId, -2, itemId, 3, 20, 1,
            catalog.CalculateSkillProps(item.EntityId, 3, 20, 1)
                .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
        if (limit >= 0) skill.LimitedTimes = limit;
        owner.PetEnergy.PetClassId = itemId == 16014 ? 1614 : 1600;
        owner.PetEnergy.OwnerUnitId = 1;
        owner.PetEnergy.SupportSkills.Add(skill);
        owner.PetEnergy.SupportSkillState.Slots.Add(catalog.CreateSkillSlot(skill,
            catalog.CalculateSkillCooldown(item.EntityId, 3, 20, 1,
                owner.Properties.ToDictionary(p => p.Id, p => p.Value))));
        var enemy = new FightBattleUnitState { Id = 2, Side = 2, CharacterType = 2,
            X = 6, Y = 5, Hp = 1000000000, MaxHp = 1000000000,
            Turn = new FightTurnUnit { Id = 2, Hp = 1000000000, Speed = 100 } };
        var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 13, MaxRound = 100 };
        state.Units.Add(owner); state.Units.Add(enemy);
        return state;
    }

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        foreach (int id in new[] {16000,16002,16003,16004,16006,16009,16012,16014,16018,16019,16020,16021,16300,16304,16306})
        {
            var state = Create(catalog, items, id);
            state.Units[0].Hp /= 2; state.Units[0].Turn.Hp = state.Units[0].Hp;
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            int ownerTurns = 0, releases = 0;
            while (ownerTurns < 12 && !state.Finished)
            {
                int counter = state.Counter + 1;
                var result = executor.Step(0, counter);
                restored.Step(0, counter);
                if (result.Event.ActorId == 1)
                {
                    ownerTurns++;
                    var normal = result.Event.SkillCasts.SingleOrDefault(c => c.SkillEntityId == 7777);
                    Require(normal != null, "Support consumed normal action: " + id);
                    foreach (var cast in result.Event.SkillCasts.Where(c => c.IsPetSupport))
                    {
                        if (id == 16014)
                            Require(cast.SkillEntityId == 50165 && cast.TargetIds.SequenceEqual(new[] {1}),
                                "Passive support did not retain its original skill/master target.");
                        Require(cast.Slot < 0 && result.Event.SkillCasts.IndexOf(cast) < result.Event.SkillCasts.IndexOf(normal),
                            "Support did not precede the owner's normal skill: " + id);
                        Require(normal.Delay >= cast.Delay + Math.Max(0f, cast.Presentation.Duration),
                            "Normal skill presentation overlaps its leading support: " + id);
                        releases++;
                    }
                    if (id == 16014)
                    {
                        var stack = state.Units[0].Statuses.SingleOrDefault(s => s.ClassId == 24138);
                        Require(stack != null && stack.StackCount == Math.Min(4, ownerTurns),
                            "Passive support stack mismatch: turn=" + ownerTurns + " releases=" + releases
                                + " statuses=" + string.Join(",", state.Units[0].Statuses.Select(s => s.ClassId + ":" + s.StackCount)));
                        Require(stack.StackCountTriggered == (ownerTurns >= 4 ? 1 : 0),
                            "Passive stack threshold quota was reset or triggered before four stacks.");
                        Require(state.Units[0].Statuses.All(s => s.ClassId != 50165),
                            "Passive skill entity was incorrectly installed as a status.");
                        if (ownerTurns >= 4)
                            Require(state.Units[0].Statuses.Any(s => s.ClassId == 24139 && s.HasHpLimit
                                && s.HpLimitLowerLimit == 1 && s.HpLimitMaxCount == 1),
                                "Fourth passive support HP-floor mismatch: " + string.Join(",",
                                    state.Units[0].Statuses.Select(s => s.ClassId + ":" + s.StackCount
                                        + ":" + s.HasHpLimit + ":" + s.HpLimitLowerLimit + ":" + s.HpLimitMaxCount)));
                    }
                    Require(state.Units[0].SkillState.Slots.Count == 1
                        && state.Units[0].SkillState.Slots[0].ReleasedTimes == ownerTurns,
                        "Support changed owner slots/counts: " + id);
                }
                string saved = FightBattleCheckpointSerializer.Save(state);
                Require(saved == FightBattleCheckpointSerializer.Save(copy), "Restored support diverged: " + id);
                executor.Step(0, counter);
                Require(saved == FightBattleCheckpointSerializer.Save(state), "Repeated receipt reran support: " + id);
                copy = FightBattleCheckpointSerializer.Load(saved);
                restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            }
            Require(releases > 0, "Original support never released: " + id);
            var slot = state.Units[0].PetEnergy.SupportSkillState.Slots.Single();
            if (id == 16014)
                Require(releases == 4 && slot.LimitedTimes == 4 && slot.Cooldown == 0,
                    "Passive support did not retain its original four-release limit/CD.");
            Require(slot.ReleasedTimes == releases && !state.Units[0].PetEnergy.SupportSkillState.IsDriving,
                "Support CD/count clock did not close: " + id);
            Require(state.Statistics.Skills.Single(s => s.SkillItemId == id).Casts == releases,
                "Support cast was absent from common statistics: " + id);
            var stats = state.Statistics.Skills.Single(s => s.SkillItemId == id);
            Require(stats.Hits > 0, "Original support produced no resolved hit: " + id);
            if (id == 16002) Require(stats.Heal > 0, "Original healing support did not restore actual HP.");
            Console.WriteLine("PET_SUPPORT " + id + " ownerTurns=" + ownerTurns + " releases=" + releases + " CD=" + slot.Cooldown + " hits=" + stats.Hits + " damage=" + stats.Damage + " heal=" + stats.Heal);
        }
        CheckLimitsAndRange(catalog, items, formula);
    }

    private static void CheckLimitsAndRange(FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        var state = Create(catalog, items, 16000, 2);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        for (int i = 0; i < 45; i++) executor.Step(0, state.Counter + 1);
        Require(state.Units[0].PetEnergy.SupportSkillState.Slots[0].ReleasedTimes == 2,
            "Support exceeded LimitedTimes.");
        state = Create(catalog, items, 16003);
        state.Units[1].X = 20; state.Units[1].Y = 20;
        state.Units[0].Skills.Clear(); state.Units[0].SkillState.Slots.Clear();
        executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        executor.Step(0, 1);
        Require(state.Units[0].PetEnergy.SupportSkillState.Slots[0].ReleasedTimes == 0,
            "Out-of-range support consumed release count.");
        state = Create(catalog, items, 16002);
        executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        for (int i = 0; i < 20; i++) executor.Step(0, state.Counter + 1);
        Require(state.Units[0].PetEnergy.SupportSkillState.Slots[0].ReleasedTimes == 0,
            "Healing support ignored full-health condition.");
        state = Create(catalog, items, 16000);
        state.Units[0].PetEnergy.SupportSkillState.Slots[0].Cooldown = 3;
        executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var releasedRounds = new List<int>();
        while (state.Units[0].SkillState.Round < 12)
        {
            var result = executor.Step(0, state.Counter + 1);
            if (result.Event.SkillCasts.Any(c => c.IsPetSupport)) releasedRounds.Add(result.Event.ActorRound);
        }
        Require(releasedRounds.SequenceEqual(new[] {4,8,12}),
            "Independent support agent did not preserve nonzero original CD semantics.");
        state = Create(catalog, items, 16003);
        state.Units[1].Hp = 1; state.Units[1].Turn.Hp = 1;
        executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var final = executor.Step(0, 1);
        Require(state.Finished && state.Outcome.Won && final.Event.SkillCasts.Any(c => c.IsPetSupport)
            && state.Units[0].SkillState.Slots[0].ReleasedTimes == 0,
            "A lethal leading support did not settle victory without consuming the unavailable normal action.");
    }
}
