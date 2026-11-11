using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleActionInvisibleHarness
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        foreach (int model in new[] {3182, 3183})
        {
            var enemy = new FightBattleUnitState {Id=1, Side=2, Hp=100, MaxHp=100, X=0, Y=0};
            var summon = new FightBattleUnitState {Id=2, Side=1, Hp=100, MaxHp=100, X=0, Y=1, MonsterId=model};
            var skill = new FightBattleSkillDefinition {TargetType=1, RangeVerified=true};
            skill.Range.Add(new FightGridPoint(0,1));
            var units = new List<FightBattleUnitState> {enemy, summon};
            Check(FightBattleTargeting.Select(enemy, units, 1).Count == 1, "Visible enemy was not selectable");
            summon.Statuses.Add(catalog.CreateStatus(24008, summon.Id, summon.Id));
            Check(summon.Statuses[0].ActionType == 50, "Original invisibility action differs");
            Check(FightBattleTargeting.Select(enemy, units, 1).Count == 0, "Enemy selected invisible summon");
            Check(FightBattleTargeting.SelectInSkillRange(enemy, units, skill).Count == 0, "Range selection bypassed invisibility");
            Check(!FightBattleTargeting.IsEligibleSkillTarget(enemy, summon, skill), "Trigger selector bypassed invisibility");
            Check(FightBattleTargeting.IsSelectable(summon) && summon.Targetable, "Invisibility removed occupancy/life eligibility");
            Check(FightBattleTargeting.Select(summon, units, 1).Count == 1, "Invisibility prevented summon attacks");
            Check(FightBattleTargeting.Select(summon, units, 0).Count == 1, "Invisibility prevented self-targeting");
            var friend = new FightBattleUnitState {Id=3, Side=1, Hp=100};
            Check(FightBattleTargeting.Select(friend, new[] {summon}, 2).Count == 1, "Friendly support excluded summon");
            var hit = new FightBattleHitDefinition();
            hit.Scope.Add(new FightGridPoint(0,0));
            Check(FightBattleTargeting.SelectScopeTargets(enemy, units, skill, summon, hit).Count == 0,
                "Enemy area hit bypassed invisibility");
            var direct = new FightBattleHitDefinition();
            Check(FightBattleHitPatterns.For(direct).Plan(new FightHitPatternContext {
                Source=enemy, Units=units, Skill=skill, Hit=direct,
                Targets=new List<FightBattleUnitState> {summon}, Primary=summon}).Count == 0,
                "Preselected direct hit bypassed invisibility");
            var cp = new FightBattleCheckpoint {StageId=1, MaxRound=10, RandomSeed=73};
            enemy.Turn.Id = enemy.Id; summon.Turn.Id = summon.Id;
            cp.Units.AddRange(units);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            Check(FightBattleTargeting.Select(restored.Units[0], restored.Units, 1).Count == 0,
                "Checkpoint lost invisibility");
            summon.Statuses[0].ForbiddenType = FightStatusLifecycle.ForbiddenStatusTrigger;
            Check(FightBattleTargeting.Select(enemy, units, 1).Count == 1, "Disabled invisibility still filtered enemy");
            summon.Statuses[0].ForbiddenType = 0;
            Check(FightBattleTargeting.Select(enemy, units, 1).Count == 0, "Reenabled invisibility was ignored");
            summon.Statuses.Clear();
            Check(FightBattleTargeting.Select(enemy, units, 1).Count == 1, "Removed invisibility remained sticky");
        }
        var actor = new FightBattleUnitState {Id=1, Side=2, Hp=100, MaxHp=100, X=0, Y=0};
        var protectedUnit = new FightBattleUnitState {Id=2, Side=1, Hp=100, MaxHp=100, X=0, Y=1};
        var visible = new FightBattleUnitState {Id=3, Side=1, Hp=100, MaxHp=100, X=0, Y=2};
        protectedUnit.Statuses.Add(catalog.CreateStatus(24008, 2, 2));
        var attack = new FightBattleSkillDefinition {EntityId=9001, Slot=0, TargetType=1, RangeVerified=true};
        attack.Range.Add(new FightGridPoint(0,1)); attack.Range.Add(new FightGridPoint(0,2));
        var area = new FightBattleHitDefinition {EntityId=9002, ActionType=4, DamageType=8, DamageParam=20, TargetMode=1};
        area.Scope.Add(new FightGridPoint(0,0)); area.Scope.Add(new FightGridPoint(0,-1));
        attack.Hits.Add(area); actor.Skills.Add(attack);
        actor.SkillState.Slots.Add(catalog.CreateSkillSlot(attack,0));
        var battle = new FightBattleCheckpoint {StageId=1, MaxRound=10, RandomSeed=73};
        foreach (var unit in new[] {actor, protectedUnit, visible}) {
            unit.Turn.Id=unit.Id; unit.Turn.Hp=unit.Hp; unit.Turn.Speed=unit.Id==1?100:1;
            battle.Units.Add(unit);
        }
        var runner = FightBattleExecutor.Begin(battle, formula, new FightDamageSettlement(), catalog);
        var step = runner.Step(0,1);
        Check(step.Event != null && step.Event.ActorId == 1 && visible.Hp < 100,
            "Area integration did not execute enemy damage");
        Check(protectedUnit.Hp == 100, "Enemy area damaged invisible summon in executor");
    }
}
