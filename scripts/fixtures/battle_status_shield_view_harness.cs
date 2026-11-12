using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleStatusShieldViewHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        foreach (int id in new[] {24175, 24435})
        {
            var owner = new FightBattleUnitState { Id = 2, Hp = 1000, MaxHp = 1000 };
            var shield = catalog.CreateStatus(id, 1, 1, id, 10810);
            Check(shield.ShieldKind == FightBattleShieldKind.Persistent, "Original persistent shield definition changed: " + id);
            shield.ShieldUsesCustomValue = true; shield.CustomShieldValue = 100;
            FightBattleShieldRuntime.Initialize(shield, null, 0); owner.Statuses.Add(shield);
            var values = new List<long>(); var presence = new List<bool>();
            var snapshots = new List<FightBattleStatusViewState>();
            owner.StatusRefreshed = (unit, status) => {
                values.Add(status.ShieldRemainValue); presence.Add(unit.Statuses.Contains(status));
                snapshots.Add(FightBattleStatusViewState.Capture(status));
            };
            var hit = new FightBattleHitDefinition();
            Check(FightBattleShieldRuntime.AbsorbDamage(null, owner, hit, -30) == 0,
                "Partial shield absorption changed.");
            Check(FightBattleShieldRuntime.AbsorbDamage(null, owner, hit, -90) == -20,
                "Shield overflow damage changed.");
            Check(values.Count == 3 && values[0] == 70 && values[1] == 0 && values[2] == 0
                && presence[0] && presence[1] && !presence[2], "Shield zero/update/removal notification order changed.");
            Check(snapshots[0].ShieldRemaining == 70 && snapshots[0].ShieldMaximum == 100,
                "Later depletion mutated a captured shield receipt.");
            owner.Statuses.Add(shield); shield.ShieldRemainValue = 60;
            Check(FightBattleShieldRuntime.ConsumeAllPersistentShields(owner) == 60
                && values[3] == 0 && presence[3], "Shield consumption lost its zero-value notification.");
        }
        foreach (int id in new[] {24082, 24083})
        {
            var owner = new FightBattleUnitState { Id = 2, Hp = 1000, MaxHp = 1000 };
            var status = catalog.CreateStatus(id, 1, 1, id, 10810);
            var component = status.Components.DamageReduceAbsolute;
            Check(component != null, "Original absolute reduction component missing.");
            component.Rate = 1; component.DamageGreaterMaxHpPer = -1; component.MaxCount = 1;
            component.AutoRemove = true; owner.Statuses.Add(status);
            var events = new List<string>(); FightBattleStatusViewState snapshot = null;
            owner.StatusRefreshed = (unit, value) => {
                events.Add("update:" + value.Components.DamageReduceAbsolute.TriggeredCount);
                snapshot = FightBattleStatusViewState.Capture(value);
            };
            owner.StatusEnded = (unit, value) => events.Add("end");
            long damage = -80, absolute = 0;
            Check(FightBattleHitComponentRuntime.ApplyDamageReduceAbsolute(owner, null, ref damage, ref absolute, null)
                && damage == 0 && absolute == 0 && !owner.Statuses.Contains(status), "Absolute reduction settlement changed.");
            Check(events.Count == 2 && events[0] == "update:1" && events[1] == "end",
                "Absolute count must reach the view before automatic removal.");
            component.TriggeredCount = 9;
            Check(snapshot.AbsoluteMaximumCount == 1 && snapshot.AbsoluteTriggerCount == 1,
                "Absolute reduction receipt was not immutable.");
        }
    }
}
