using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSanctuarySharingHarness
{
    private sealed class DefenceResponse : IFightBattleImpactGate, IFightBattleImpactSettlementObserver
    {
        private readonly FightBattleUnitState guardian;
        private readonly int property, value;
        public DefenceResponse(FightBattleUnitState guardian, int property, int value)
        { this.guardian = guardian; this.property = property; this.value = value; }
        public void AwaitImpact(FightBattlePendingImpact impact) { }
        public void ImpactSettled(FightBattlePendingImpact impact, FightBattleDamageEvent hit)
        {
            if (hit.SkillEntityId != 990580) return;
            guardian.BaseProperties.Single(item => item.Id == property).Value = value;
            guardian.Properties.Single(item => item.Id == property).Value = value;
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Sanctuary sharing: " + message);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 3, 5 })
        foreach (int level in new[] { 1, 2, 20 })
        {
            foreach (long incoming in new long[] { 0, 1, 3, 101, 1000 })
            foreach (int defence in new[] { 50, 100, 150, 199, 200 })
                CheckPlan(catalog, formula, rank, level, incoming, defence);
            foreach (int defence in new[] { 100, 150, 200 })
            foreach (int shield in new[] { 0, 800 })
            {
                CheckBattle(catalog, formula, items, rank, level, defence, shield, 20000, 20000);
                cases++;
            }
        }
        CheckBattle(catalog, formula, items, 4, 2, 100, 800, 1, 20000);
        CheckBattle(catalog, formula, items, 4, 2, 100, 0, 20000, 1);
        CheckBattle(catalog, formula, items, 4, 2, 100, 0, 20000, 20000, 150);
        CheckBattle(catalog, formula, items, 4, 2, 100, 800, 1, 20000, selfMaster: true);
        CheckBattle(catalog, formula, items, 4, 2, 100, 0, 20000, 1, selfMaster: true);
        CheckBattle(catalog, formula, items, 4, 2, 100, 0, 20000, 20000, selfMaster: true);
        return $"Sanctuary sharing passed: 225 numeric/filter cases, {cases + 6} cultivated battles, independent shields, death order, restore and receipt replay.";
    }

    private static void CheckPlan(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        int rank, int level, long incoming, int defence)
    {
        var guardian = Unit(1, 1, 3, 20000, defence, 2000, formula);
        var attacker = Unit(2, 2, 4, 20000, 100, 1000, formula);
        var receiver = Unit(3, 1, 4, 20000, 100, 900, formula);
        var units = new[] { guardian, attacker, receiver };
        var status = catalog.CreateStatus(24304, 3, 1, 10, 25074, 10580, rank, level, 8, guardian.Properties);
        receiver.Statuses.Add(status);
        FightStatusPropertyRuntime.RebuildAll(units, catalog, formula);
        var skill = new FightBattleSkillDefinition { CanTriggerChild = true };
        var context = new FightBattleTransientDamageContext(new FightDamageTypeData(), -incoming, -incoming, false);
        var pending = new List<FightBattleStatusSkillInvocation>();
        FightBattleDamageSharingRuntime.Collect(attacker, receiver, skill, context, units, formula, pending);
        long split = incoming / 2;
        Check(context.Damage == -incoming + split && context.AbsoluteDamage == -incoming + split,
            $"the pre-shield half split did not round down (rank {rank}, level {level}, incoming {incoming}, defence {defence}, actual {context.Damage}, properties {string.Join(",", (status.StatusProperties ?? new List<FightPropertyValue>()).Select(value => value.Id + ":" + value.Value))})");
        Check(pending.Count == (split > 0 ? 1 : 0), "the half split emitted an unexpected child");
        if (split > 0)
        {
            var child = pending.Single();
            decimal scale = defence == 150 ? .5m : defence == 199 ? .01m : defence == 200 ? 0m : 1m;
            long expected = (long)(-formula.CalculateStatusDamage(
                catalog.CalculateSkillProps(24304, rank, level, 8).ToDictionary(pair => pair.Key, pair => pair.Value),
                formula.PropertyId("DamageByDamage"), formula.PropertyId("FixedDamageByDamage"), split) * scale);
            Check(child.SourceUnitId == attacker.Id && child.TargetUnitId == guardian.Id
                && child.TargetPositionUnitId == guardian.Id && child.CustomDamage == expected
                && child.DamageSharingTransfer && child.Trigger.EntityId == 11780
                && child.Trigger.SourceSkillItemId == 10580 && child.Trigger.SourceSkillRank == rank
                && child.Trigger.SourceSkillLevel == level, "the original transfer roles, value or cultivation changed");
        }
        foreach (int condition in new[] { 0, 1, 2, 3 })
        {
            status.ForbiddenType = condition == 0 ? 1 : 0;
            guardian.Hp = condition == 1 ? 0 : 20000;
            skill.CanTriggerChild = condition != 2;
            skill.DamageSharingTransfer = condition == 3;
            context = new FightBattleTransientDamageContext(new FightDamageTypeData(), -1000, -1000, false);
            pending.Clear();
            FightBattleDamageSharingRuntime.Collect(attacker, receiver, skill, context, units, formula, pending);
            Check(context.Damage == -1000 && pending.Count == 0,
                "forbidden/dead/child-filtered or recursively transferred damage was split");
        }
    }

    private static void CheckBattle(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int defence, int receiverShield,
        long guardianHp, long receiverHp, int responseDefence = 0, bool selfMaster = false)
    {
        var state = new FightBattleCheckpoint { StageId = 1, RandomSeed = 71, MaxRound = 8,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        var guardian = Unit(1, 1, 3, guardianHp, defence, 2000, formula);
        var attacker = Unit(2, 2, 4, 20000, 100, 1000, formula);
        var receiver = Unit(3, 1, 4, receiverHp, 100, 900, formula);
        if (selfMaster)
        {
            guardian.MasterId = guardian.Turn.MasterId = guardian.Id;
            receiver.MasterId = receiver.Turn.MasterId = receiver.Id;
        }
        guardian.Statuses.Add(catalog.CreateStatus(25074, 1, 1, 1, 25074, 10580,
            rank, level, 8, guardian.Properties));
        if (receiverShield > 0) AddShield(catalog, receiver, receiverShield, 20);
        AddShield(catalog, guardian, 25, 21);
        var skill = new FightBattleSkillDefinition { EntityId = 990580, Slot = 0,
            TargetType = 1, ActionType = 2, SkillType = 2, CanTriggerChild = true };
        skill.Range.Add(new FightGridPoint(0, -1));
        skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990581, DamageId = 10001,
            DamageType = 8, ActionType = 1, HasCustomDamage = true, CustomDamage = 1001 });
        attacker.Skills.Add(skill);
        attacker.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
        state.Units.AddRange(new[] { guardian, attacker, receiver });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(attacker.Id);
        state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
        var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        if (responseDefence > 0)
        {
            executor.ImpactGate = new DefenceResponse(guardian, formula.PropertyId("Defence"), responseDefence);
            restored.ImpactGate = new DefenceResponse(copy.Units.Single(unit => unit.Id == guardian.Id),
                formula.PropertyId("Defence"), responseDefence);
        }
        FightBattleStepEvent impact = null;
        bool grantObserved = false;
        for (int i = 0; i < 10 && impact == null; i++)
        {
            var step = executor.Step(state.Sequence, state.Counter + 1).Event;
            restored.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "original shelter selection or damage settlement changed after restore");
            if (step.SkillCasts.Any(cast => cast.SkillEntityId == 11790))
            {
                Check(receiver.Statuses.Any(status => status.ClassId == 24304
                        && status.CreatorId == guardian.Id && status.SourceSkillLevel == level)
                    && guardian.Statuses.Any(status => status.ClassId == 24305
                        && status.CreatorId == guardian.Id && status.SourceSkillLevel == level)
                    && !receiver.Statuses.Any(status => status.ClassId == 24305),
                    "the original status-start Creator target did not link the guardian");
                grantObserved = true;
            }
            if (step.DamageEvents.Any(hit => hit.SkillEntityId == 990580)) impact = step;
        }
        Check(grantObserved && impact != null, "the original grant or incoming controlled hit did not execute");
        var received = impact.DamageEvents.Single(hit => hit.SkillEntityId == 990580);
        var transferred = impact.DamageEvents.Single(hit => hit.SkillEntityId == 11780);
        Check(impact.DamageEvents.IndexOf(received) < impact.DamageEvents.IndexOf(transferred),
            "guardian damage settled before the receiver");
        long receiverLoss = Math.Min(receiverHp, Math.Max(0, 501 - receiverShield));
        Check(received.TargetId == receiver.Id && received.BeforeHp - received.AfterHp == receiverLoss
            && received.ShieldDamage == Math.Min(receiverShield, 501), "the split happened after the receiver's shield");
        int finalDefence = responseDefence > 0 ? responseDefence : defence;
        decimal scale = finalDefence == 150 ? .5m : finalDefence == 200 ? 0m : 1m;
        long raw = (long)(-formula.CalculateStatusDamage(
            catalog.CalculateSkillProps(24304, rank, level, 8).ToDictionary(pair => pair.Key, pair => pair.Value),
            formula.PropertyId("DamageByDamage"), formula.PropertyId("FixedDamageByDamage"), 500) * scale);
        long guardianLoss = Math.Min(guardianHp, Math.Max(0, raw - 25));
        Check(transferred.SourceId == attacker.Id && transferred.TargetId == guardian.Id
            && transferred.HitEntityId == 11781 && transferred.DamageEntityId == 10005
            && transferred.DamageType == 2 && transferred.SkillItemId == 10580
            && transferred.BeforeHp - transferred.AfterHp == guardianLoss
            && transferred.ShieldDamage == Math.Min(raw, 25),
            $"the original child lost attribution, numeric units or guardian shield settlement (rank {rank}, level {level}, defence {defence}, expected raw {raw}, loss {guardianLoss}, actual {transferred.SourceId}->{transferred.TargetId}, hit {transferred.HitEntityId}, damage {transferred.DamageEntityId}, type {transferred.DamageType}, item {transferred.SkillItemId}, HP {transferred.BeforeHp}->{transferred.AfterHp}, shield {transferred.ShieldDamage})");
        Check(impact.DamageEvents.Count(hit => hit.SkillEntityId == 11780) == 1,
            "the transferred damage recursively split");
        string settled = FightBattleCheckpointSerializer.Save(state);
        var settledState = FightBattleCheckpointSerializer.Load(settled);
        var settledExecutor = FightBattleExecutor.Restore(settledState, formula,
            new FightDamageSettlement(), catalog, items);
        Check(settledExecutor.Step(settledState.Sequence, settledState.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(settledState) == settled,
            "restoring the settled hit changed its living or dead adventurer into a removed summon");
        var replay = executor.Step(state.Sequence, state.Counter);
        Check(replay.Replayed && FightBattleCheckpointSerializer.Save(state) == settled,
            "replaying the hit charged its guardian twice");
    }

    private static void AddShield(FightOfficialCombatCatalog catalog, FightBattleUnitState unit,
        long value, int instance)
    {
        var shield = catalog.CreateStatus(24180, unit.Id, unit.Id, instance, 0, 0, 1, 1, 8,
            unit.Properties, value);
        shield.ShieldUsesCustomValue = true;
        unit.Statuses.Add(shield);
    }

    private static FightBattleUnitState Unit(int id, int side, int x, long hp, int defence,
        int speed, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1,
            Profession = 4, Level = 70, SubRank = 8, X = x, Y = id == 2 ? 4 : 3,
            Hp = hp, MaxHp = 20000, LastMaxHp = 20000,
            Turn = new FightTurnUnit { Id = id, Hp = hp, Speed = speed } };
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", defence },
            { "MaxHp", 20000 }, { "Speed", speed }, { "BaseCureAdd", 10000 }, { "BaseBeCureAdd", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
