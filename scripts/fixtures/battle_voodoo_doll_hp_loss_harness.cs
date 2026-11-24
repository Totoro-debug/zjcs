using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleVoodooDollHpLossHarness
{
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        MaxHpExpiry(catalog, items, formula, false);
        MaxHpExpiry(catalog, items, formula, true);
        SkillCost(catalog, items, formula);
        SkillCost(catalog, items, formula, true);
        ShieldAbsorption(catalog, items, formula);
        RestoreNormalization(catalog, items, formula);
        LinkedSummonDeath(catalog, items, formula);
        LinkedPlayerRevival(catalog, items, formula, false);
        LinkedPlayerRevival(catalog, items, formula, true);
    }

    private static FightBattleUnitState Unit(int id, int side, long hp, int speed)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = side,
            Hp = hp, MaxHp = hp, LastMaxHp = hp, Level = 100, SubRank = 8,
            X = id + 4, Y = 5, MoveMask = 5,
            Turn = new FightTurnUnit { Id = id, Hp = hp, Speed = speed } };
        unit.Properties.AddRange(new[] { new FightPropertyValue { Id = 1, Value = 1000 },
            new FightPropertyValue { Id = 2, Value = 1000 }, new FightPropertyValue { Id = 3, Value = hp },
            new FightPropertyValue { Id = 4, Value = speed } });
        return unit;
    }

    private static FightBattleCheckpoint Checkpoint(FightOfficialCombatCatalog catalog)
    {
        var actor = Unit(1, 1, 20000, 1000);
        var target = Unit(2, 2, 20000, 1);
        var doll = Unit(3, 2, 1000, 1);
        var definition = catalog.VoodooDollDefinition(4600);
        doll.MasterId = 1; doll.MonsterId = doll.MonsterTemplateId = 4600;
        doll.Turn.MasterId = 1; doll.Turn.RoundType = 1;
        doll.VoodooDoll = new FightVoodooDollLink { TargetId = target.Id,
            SyncSkillEntityId = definition.SyncSkillEntityId, SyncHitEntityId = definition.SyncHitEntityId,
            SyncDamageEntityId = definition.SyncDamageEntityId };
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20, RandomSeed = 41,
            Grid = new FightBattleGrid { Width = 20, Height = 20 }, ResultRules = new FightBattleResultRules() };
        checkpoint.Units.AddRange(new[] { actor, target, doll });
        return checkpoint;
    }

    private static void MaxHpExpiry(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, bool unchangedHp)
    {
        var checkpoint = Checkpoint(catalog);
        var doll = checkpoint.Units[2];
        // A controlled system status exercises the ordinary property/lifetime
        // module. Initial property assembly must not transmit an HP change.
        doll.Statuses.Add(new FightBattleStatusState { ClassId = 990097, InstanceId = 990097,
            OwnerId = doll.Id, CreatorId = 1, StatusType = 0, HasPropertyComponent = true,
            PropertyDefinitionResolved = true, DurationRound = 1, CurDurationRound = 1,
            RoundTarget = FightStatusLifecycle.TargetCreator, RoundUpdateTiming = FightStatusLifecycle.TimingEnd,
            StartRound = 0, ResolvedProperties = new List<FightPropertyValue> {
                new FightPropertyValue { Id = 3, Value = 1000 } } });
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        Check(doll.Hp == 2000 && doll.MaxHp == 2000 && checkpoint.Units[1].Hp == 20000,
            "Initial substitute property assembly transmitted HP.");
        if (unchangedHp) doll.Hp = doll.Turn.Hp = 1;
        var step = executor.Step(0, 1).Event;
        Check(doll.MaxHp == 1000 && doll.Hp == (unchangedHp ? 1 : 1000),
            "The controlled status did not expire through the ordinary maximum-HP module.");
        var transfers = step.DamageEvents.Where(hit => hit.SkillEntityId == 10140).ToArray();
        BattleVoodooDollHarness.CheckTransmissionPresentation(step);
        Check(transfers.Length == (unchangedHp ? 0 : 1)
            && checkpoint.Units[1].Hp == (unchangedHp ? 20000 : 19000),
            "Maximum-HP change omitted, duplicated or invented actual HP transmission.");
        if (!unchangedHp) Check(transfers[0].DamageValue == -1000 && transfers[0].TrueDamage
            && transfers[0].IgnoreShield, "Non-hit HP loss changed its amount or true-damage flags.");
        Replay(checkpoint, catalog, items, formula);
    }

    private static void SkillCost(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, bool lethal = false)
    {
        var checkpoint = Checkpoint(catalog);
        var doll = checkpoint.Units[2];
        doll.Turn.RoundType = 0; doll.Turn.Speed = 2000;
        doll.Properties.Single(property => property.Id == 4).Value = 2000;
        var skill = new FightBattleSkillDefinition { EntityId = 990098, Slot = 0, TargetType = 0,
            CostType = 1, SkillCost = 123, LimitedTimes = 1, Duration = .2f, ActionDuration = .2f };
        skill.Range.Add(new FightGridPoint(0, 0));
        doll.Skills.Add(skill); doll.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        if (lethal) checkpoint.Units[1].Hp = checkpoint.Units[1].Turn.Hp = 1;
        var step = executor.Step(0, 1).Event;
        Check(step.ActorId == doll.Id && doll.Hp == 877 && checkpoint.Units[1].Hp == (lethal ? 0 : 19877)
            && step.DamageEvents.Count(hit => hit.SkillEntityId == 10140 && hit.DamageValue == -123) == 1,
            "Non-hit skill cost did not transmit its actual HP reduction exactly once.");
        BattleVoodooDollHarness.CheckTransmissionPresentation(step);
        Replay(checkpoint, catalog, items, formula);
    }

    private static void ShieldAbsorption(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var checkpoint = Checkpoint(catalog);
        var actor = checkpoint.Units[0];
        var doll = checkpoint.Units[2];
        doll.Statuses.Add(new FightBattleStatusState { ClassId = 990099, InstanceId = 990099,
            OwnerId = doll.Id, StatusType = 0, ShieldKind = FightBattleShieldKind.Persistent,
            ShieldInitialized = true, ShieldValue = 1000, ShieldRemainValue = 1000 });
        var skill = new FightBattleSkillDefinition { EntityId = 990100, Slot = 0, TargetType = 1 };
        skill.Range.Add(new FightGridPoint(doll.X - actor.X, 0));
        skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990101, DamageType = 8,
            HasCustomDamage = true, CustomDamage = -123 });
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(0, 1).Event;
        Check(step.DamageEvents.Any(hit => hit.TargetId == doll.Id && hit.ShieldDamage > 0)
            && doll.Hp == 1000 && checkpoint.Units[1].Hp == 20000
            && step.DamageEvents.All(hit => hit.SkillEntityId != 10140),
            "A hit absorbed by the substitute's shield incorrectly transmitted HP loss.");
    }

    private static void RestoreNormalization(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var checkpoint = Checkpoint(catalog);
        FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var doll = checkpoint.Units[2];
        doll.MaxHp = doll.LastMaxHp = doll.Hp = 2000;
        checkpoint = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        FightBattleExecutor.Restore(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        Check(checkpoint.Units[2].Hp == 1000 && checkpoint.Units[1].Hp == 20000,
            "Checkpoint property normalization transmitted an initialization HP difference.");
    }

    private static void LinkedSummonDeath(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var checkpoint = Checkpoint(catalog);
        var actor = checkpoint.Units[0];
        var target = checkpoint.Units[1];
        var doll = checkpoint.Units[2];
        var master = Unit(4, 2, 20000, 1);
        master.Hp = master.Turn.Hp = 19998;
        target.CharacterType = doll.CharacterType = 3;
        target.MasterId = target.Turn.MasterId = master.Id;
        target.MonsterId = target.MonsterTemplateId = 3196;
        target.Hp = target.MaxHp = target.LastMaxHp = target.Turn.Hp = 100;
        target.Properties.Single(property => property.Id == 3).Value = 100;
        catalog.AssembleUnit(master, null, catalog.CreatePassiveStatusBindings(master, items,
            new[] { new FightOfficialPassiveSkillBinding { ItemId = 10600, Rank = 2, Level = 1, SubRank = 8 } }));
        checkpoint.Units.Add(master);
        var skill = new FightBattleSkillDefinition { EntityId = 990102, Slot = 0, TargetType = 1 };
        skill.Range.Add(new FightGridPoint(doll.X - actor.X, 0));
        skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990103, DamageType = 8,
            HasCustomDamage = true, CustomDamage = -200, Delay = .4f });
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(0, 1).Event;
        Check(target.Hp == 0 && doll.Hp > 0 && master.Hp == master.MaxHp
            && step.DamageEvents.Count(hit => hit.SkillEntityId == 12050 && hit.TargetId == master.Id
                && hit.IsCure && hit.Damage == 2 && Math.Abs(hit.Delay - .4f) < .0001f) == 1,
            "A lethal substitute transmission did not run the linked summon's ordinary death/heal response.");
        Replay(checkpoint, catalog, items, formula);
    }

    private static void LinkedPlayerRevival(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, bool onlyDollDies)
    {
        var checkpoint = Checkpoint(catalog);
        var actor = checkpoint.Units[0];
        var target = checkpoint.Units[1];
        var doll = checkpoint.Units[2];
        var holder = Unit(4, 2, 20000, 1);
        target.CharacterType = doll.CharacterType = 1;
        if (!onlyDollDies) target.Hp = target.Turn.Hp = 100;
        catalog.AssembleUnit(holder, null, catalog.CreatePassiveStatusBindings(holder, items,
            new[] { new FightOfficialPassiveSkillBinding { ItemId = 10531, Rank = 5, Level = 20, SubRank = 8 } }));
        checkpoint.Units.Add(holder);
        var skill = new FightBattleSkillDefinition { EntityId = 990104, Slot = 0, TargetType = 1 };
        skill.Range.Add(new FightGridPoint(doll.X - actor.X, 0));
        skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990105, DamageType = 8,
            HasCustomDamage = true, CustomDamage = onlyDollDies ? -2000 : -200, Delay = .4f });
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(0, 1).Event;
        var listener = holder.Statuses.Single(status => status.ClassId == 25014);
        Check(target.Hp > 0 && listener.RoleDieTriggeredCount == (onlyDollDies ? 0 : 1),
            onlyDollDies ? "An unhealable substitute consumed a revival charge."
                : "Lethal mapped HP loss did not run the linked player's ordinary immediate revival.");
        if (!onlyDollDies)
        {
            var revivals = step.SkillCasts.Where(cast => cast.ActorId == holder.Id && cast.SkillItemId == 10531).ToArray();
            Check(revivals.Length > 0 && revivals.Min(cast => cast.Delay) >= .3999f,
                "Mapped lethal damage revived its target before the fatal hit time.");
        }
        Replay(checkpoint, catalog, items, formula);
    }

    private static void Replay(FightBattleCheckpoint checkpoint, FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        string saved = FightBattleCheckpointSerializer.Save(checkpoint);
        var copy = FightBattleCheckpointSerializer.Load(saved);
        var executor = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        Check(executor.Step(copy.Sequence, copy.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(copy) == saved,
            "Restore replay repeated a substitute HP transmission.");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
