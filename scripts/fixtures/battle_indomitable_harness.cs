using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleIndomitableHarness
{
    static void Check(bool value, string message) {if(!value) throw new Exception(message);}
    static FightBattleUnitState Unit(int id, long hp)
    {
        var u=new FightBattleUnitState {Id=id,Side=id,CharacterType=1,Hp=hp,MaxHp=1000,LastMaxHp=1000,
            Level=150,SubRank=1,X=4+id,Y=4,MoveMask=5,
            Turn=new FightTurnUnit {Id=id,Hp=hp,Speed=id==1?900:1000}};
        foreach(var p in new[]{new FightPropertyValue {Id=1,Value=100},new FightPropertyValue {Id=2,Value=100},
            new FightPropertyValue {Id=3,Value=1000},new FightPropertyValue {Id=4,Value=id==1?900:1000}})
            u.Properties.Add(p);
        return u;
    }
    static FightBattleStatusState Attach(FightOfficialCombatCatalog catalog, FightBattleUnitState unit)
    {
        var s=catalog.CreateStatus(25013,1,1,1,25013,10518,6,17,1);unit.Statuses.Add(s);return s;
    }
    static FightBattleStepEvent NextAttack(FightBattleExecutor runner)
    {
        for(int i=0;i<30;i++) {
            var step=runner.Step(0,runner.State.Counter+1).Event;
            if(step.SkillCasts.Exists(c=>c.SkillEntityId==990201)) return step;
        }
        throw new Exception("Fatal fixture attack did not execute.");
    }
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var attacker=Unit(2,1000);var victim=Unit(1,1);var source=Attach(catalog,victim);
        var units=new List<FightBattleUnitState>{victim,attacker};
        var triggers=new List<FightBattleStatusSkillInvocation>();
        FightStatusTriggerRuntime.Apply(victim,catalog,true,units:units,configuredTriggerSkills:triggers);
        Check(source.HpLimitTriggeredCount==0 && triggers.Count==0,"Entry at one HP consumed fatal prevention.");
        var hitExecutor=new FightBattleHitExecutor(formula,new FightDamageSettlement());
        var skill=new FightBattleSkillDefinition {EntityId=990201,CanTriggerChild=true};
        var hit=new FightBattleHitDefinition {EntityId=990202,DamageType=8,ActionType=1,HasCustomDamage=true,CustomDamage=999};
        victim.Hp=1000;
        var result=hitExecutor.Apply(new FightRandomStream(13),1,attacker,victim,skill,hit,1,false);
        FightStatusTriggerRuntime.Apply(victim,catalog,false,units:units,configuredTriggerSkills:triggers);
        Check(victim.Hp==1 && source.HpLimitTriggeredCount==0 && result.FatalPrevention==null && triggers.Count==0,
            "Nonfatal damage to one HP consumed fatal prevention.");
        hit.CustomDamage=1;
        result=hitExecutor.Apply(new FightRandomStream(13),2,attacker,victim,skill,hit,1,false);
        Check(victim.Hp==1 && source.HpLimitTriggeredCount==1 && result.FatalPrevention==source
            && !result.Settlement.KilledTarget && result.Settlement.RealDamageValue==0,
            "A lethal hit from one HP did not preserve HP exactly once.");
        result=hitExecutor.Apply(new FightRandomStream(13),3,attacker,victim,skill,hit,1,false);
        Check(victim.Hp==0 && result.Settlement.KilledTarget && source.HpLimitTriggeredCount==1,
            "Fatal prevention could be consumed twice.");

        victim=Unit(1,1000);source=Attach(catalog,victim);attacker=Unit(2,1000);
        foreach(var name in new[]{"BaseCureAdd","BaseBeCureAdd"})
            victim.Properties.Add(new FightPropertyValue {Id=catalog.PropertyType(name),Value=10000});
        skill=new FightBattleSkillDefinition {EntityId=990201,Slot=0,TargetType=1,CanTriggerChild=true};
        skill.Range.Add(new FightGridPoint(-1,0));
        skill.Hits.Add(new FightBattleHitDefinition {EntityId=990202,DamageType=8,ActionType=1,
            HasCustomDamage=true,CustomDamage=2000,Delay=.1f});
        skill.Hits.Add(new FightBattleHitDefinition {EntityId=990203,DamageType=8,ActionType=1,
            HasCustomDamage=true,CustomDamage=2000,Delay=.2f});
        attacker.Skills.Add(skill);attacker.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,ResultRules=new FightBattleResultRules()};
        cp.Units.Add(victim);cp.Units.Add(attacker);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var step=NextAttack(runner);
        Check(source.HpLimitTriggeredCount==1 && victim.Hp>1 && !victim.Ended,"Recovery did not precede death confirmation.");
        var original=step.DamageEvents.Find(d=>d.HitEntityId==990202);
        var recovery=step.DamageEvents.Find(d=>d.SkillEntityId==10990);
        var followup=step.DamageEvents.Find(d=>d.HitEntityId==990203);
        Check(original!=null && original.Damage==-999 && original.DamageValue==-2000,
            "Fatal prevention lost raw damage or actual HP loss.");
        Check(recovery!=null && recovery.Damage>0 && recovery.SkillItemId==10518
            && step.DamageEvents.IndexOf(recovery)<step.DamageEvents.IndexOf(followup),
            "Original self-recovery did not execute immediately with secret attribution.");
        var immune=victim.Statuses.Find(s=>s.ClassId==24082);
        Check(immune!=null && immune.DurationRound==2 && immune.SourceSkillRank==6
            && immune.SourceSkillLevel==17 && immune.ApplyingSkillItemId==10518
            && followup!=null && followup.Damage==0,"Original two-round indomitable did not block the next hit.");
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        victim=cp.Units.Find(u=>u.Id==1);source=victim.Statuses.Find(s=>s.ClassId==25013);
        Check(source.HpLimitTriggeredCount==1,"Checkpoint forgot the consumed prevention.");
        foreach(var s in new List<FightBattleStatusState>(victim.Statuses))
            if(s.ClassId==24082) FightStatusLifecycle.End(victim,s);
        step=NextAttack(runner);
        Check(victim.Hp==0 && !step.SkillCasts.Exists(c=>c.SkillEntityId==10990),
            "Restoring a checkpoint granted another fatal recovery.");
    }
}
