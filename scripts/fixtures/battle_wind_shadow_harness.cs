using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWindShadowHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception("Wind shadow: " + message); }
    static FightBattleUnitState Unit(int id, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id=id, Side=id, CharacterType=1, Level=50, SubRank=5,
            X=5, Y=id==1 ? 5 : 6, Hp=1000000, MaxHp=1000000, LastMaxHp=1000000 };
        unit.Turn.Id=id; unit.Turn.Hp=unit.Hp; unit.Turn.Speed=id==1 ? 1000 : 10;
        foreach (var entry in new Dictionary<int,long> {{1,1000},{2,10},{3,unit.MaxHp},{4,unit.Turn.Speed}})
            unit.Properties.Add(new FightPropertyValue { Id=entry.Key, Value=entry.Value });
        unit.Properties.Add(new FightPropertyValue { Id=formula.PropertyId("CritAvoidPercent"), Value=10000 });
        unit.Properties.Add(new FightPropertyValue { Id=formula.PropertyId("BaseEffectRate"), Value=10000 });
        unit.Properties.Add(new FightPropertyValue { Id=formula.PropertyId("BaseEffectDodge"), Value=10000 });
        return unit;
    }
    static FightBattleStepEvent NextCast(FightBattleExecutor executor)
    {
        for (int i=0; i<50; i++) {
            var step=executor.Step(0,executor.State.Counter+1).Event;
            if (step.SkillCasts.Any(c=>c.SkillEntityId==11010)) return step;
        }
        throw new Exception("Wind shadow fixture did not cast");
    }
    static bool ProbeDelay(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int seed)
    {
        var cp=new FightBattleCheckpoint {StageId=1001, MaxRound=5, RandomSeed=seed,
            Grid=new FightBattleGrid {Width=12,Height=12}};
        var actor=Unit(1,formula); var target=Unit(2,formula);
        target.Turn.Speed=1000;
        target.Properties.Single(p=>p.Id==4).Value=1000;
        var passive=catalog.CreateStatus(25038,1,1,1,25038,10569,2,20,5);
        passive.Components.HitSkill.ConditionProgress=2;
        passive.Components.HitSkill.Rate=1f;
        actor.Statuses.Add(passive);
        var props=catalog.CalculateSkillProps(11010,2,20,5)
            .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
        var skill=catalog.CreateSkill(11010,0,10042,2,20,5,props);
        skill.ElementType=100; skill.LimitedTimes=1;
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        cp.Units.AddRange(new[]{actor,target}); var wave=new FightBattleWaveState();
        wave.UnitIds.Add(2); cp.Waves.Add(wave);
        var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var step=NextCast(executor);
        Check(step.DamageEvents.Any(e=>e.SkillEntityId==11670 && e.Damage<0),"delay probe missed child damage");
        var delay=target.Statuses.SingleOrDefault(s=>s.ClassId==24232);
        if (delay == null) return false;
        Check(delay.DurationRound==1 && delay.CanDispersed && delay.Components.RoundInterval!=null
            && Math.Abs(delay.Components.RoundInterval.RoundIntervalPercent-.2f)<.0001f
            && !delay.Components.RoundInterval.FastForward,"applied delay differs from original status");
        for (int i=0; i<10 && delay.Components.RoundInterval.TriggeredCount==0; i++)
            executor.Step(0,executor.State.Counter+1);
        Check(delay.Components.RoundInterval.TriggeredCount==1,"delay status did not reach its configured round trigger");
        var entry=cp.Timeline.Entries.Single(e=>e.Id==target.Id);
        long expected=(long)Math.Round(target.Turn.IntervalTicks*1.2);
        Check(Math.Abs((entry.Next-cp.Timeline.Current)-expected)<=2,
            "delay did not add 20% to the real turn interval");
        return true;
    }
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var cp=new FightBattleCheckpoint { StageId=1001, MaxRound=20, RandomSeed=91,
            Grid=new FightBattleGrid { Width=12, Height=12 } };
        var actor=Unit(1,formula); var target=Unit(2,formula);
        var passive=catalog.CreateStatus(25038,1,1,1,25038,10569,2,20,5);
        var component=passive.Components.HitSkill;
        Check(component.ConditionCount==3 && Math.Abs(component.Rate-.3f)<.0001f,
            "original threshold or probability differs");
        var childDefinition=catalog.CreateSkill(11670,0,10569,2,20,5,null);
        Check(childDefinition.HitGraphEntities.Any(h=>h.DamageId==11672
            && h.AppliedStatuses.Any(s=>s.StatusId==24232 && Math.Abs(s.Chance-.5)<.0001)),
            "original child graph lost the delay status application");
        component.Rate=.000001f;
        actor.Statuses.Add(passive);
        var props=catalog.CalculateSkillProps(11010,2,20,5)
            .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
        var skill=catalog.CreateSkill(11010,0,10042,2,20,5,props);
        skill.ElementType=100; skill.LimitedTimes=-1;
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        cp.Units.AddRange(new[]{actor,target}); var wave=new FightBattleWaveState();
        wave.UnitIds.Add(2); cp.Waves.Add(wave);
        var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        for (int i=1; i<=2; i++) {
            var step=NextCast(executor);
            Check(!step.SkillCasts.Any(c=>c.SkillEntityId==11670),"triggered before three hits");
            Check(component.ConditionProgress==i,"qualifying hit did not advance count " + i);
        }
        skill.ElementType=101;
        Check(!NextCast(executor).SkillCasts.Any(c=>c.SkillEntityId==11670)
            && component.ConditionProgress==2,"another element advanced the wind counter");
        skill.ElementType=100;
        var dodge=new FightPropertyValue {Id=formula.PropertyId("DodgePercent"),Value=10000};
        target.BaseProperties.Add(dodge);
        FightStatusPropertyRuntime.RebuildAll(cp.Units,catalog,formula);
        var missed=NextCast(executor);
        Check(missed.DamageEvents.Any(e=>e.SkillEntityId==11010 && e.Dodge)
            && component.ConditionProgress==2,"dodge fixture: dodge="
                + missed.DamageEvents.Any(e=>e.SkillEntityId==11010 && e.Dodge)
                + " count="+component.ConditionProgress);
        target.BaseProperties.Remove(dodge);
        FightStatusPropertyRuntime.RebuildAll(cp.Units,catalog,formula);
        var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
        var restoredComponent=restored.Units.Single(u=>u.Id==1).Statuses.Single(s=>s.ClassId==25038).Components.HitSkill;
        Check(restoredComponent.ConditionProgress==2,"checkpoint lost hit count");
        component.Rate=restoredComponent.Rate=1f;
        var third=NextCast(executor); var replay=NextCast(resumed);
        Check(third.SkillCasts.Count(c=>c.SkillEntityId==11670)==1 && component.ConditionProgress==0,
            "third qualifying hit did not trigger the original child skill");
        Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),
            "checkpoint diverged on the third hit");
        Check(replay.DamageEvents.Any(e=>e.SkillEntityId==11670 && e.TargetId==2 && e.Damage<0),
            "original child did not damage the hit target");
        component.Rate=.000001f;
        for (int i=1; i<=3; i++) {
            var step=NextCast(executor);
            Check(!step.SkillCasts.Any(c=>c.SkillEntityId==11670),"failed probability unexpectedly triggered");
            Check(component.ConditionProgress==i%3,"failed probability did not start a new three-hit count");
        }
        int applied=0;
        for (int seed=1; seed<=16; seed++) if (ProbeDelay(catalog,formula,items,seed)) applied++;
        Check(applied>0 && applied<16,"original delay probability did not exhibit both outcomes: "+applied);
        return "Wind shadow passed: qualifying hit counter, child damage, delay status and checkpoint.";
    }
}
