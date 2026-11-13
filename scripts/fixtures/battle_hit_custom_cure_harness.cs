using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zjcs.Combat.Official;

public static class BattleHitCustomCureHarness
{
    static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
    static Dictionary<int,long> Props(List<FightPropertyValue> values) {
        var result=new Dictionary<int,long>();foreach(var p in values) result[p.Id]=p.Value;return result;
    }
    static FightBattleUnitState Unit(FightOfficialCombatCatalog c,int id,int side,int x) {
        var u=new FightBattleUnitState {Id=id,Side=side,X=x,Y=5,Hp=500000000,MaxHp=1000000000,LastMaxHp=1000000000,Level=150,SubRank=1,CharacterType=1};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:1;
        foreach(var p in new[]{new FightPropertyValue{Id=1,Value=10001},new FightPropertyValue{Id=2,Value=0},
            new FightPropertyValue{Id=3,Value=1000000000},new FightPropertyValue{Id=4,Value=id==1?1000:1}})u.Properties.Add(p);
        foreach(string name in new[]{"BaseEffectRate","BaseEffectDodge","BaseCureAdd","BaseBeCureAdd"})
            u.Properties.Add(new FightPropertyValue {Id=c.PropertyType(name),Value=10000});
        return u;
    }
    static FightBattleCheckpoint Checkpoint(FightBattleUnitState a,FightBattleUnitState b) {
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(a);cp.Units.Add(b);return cp;
    }
    public static void Run(FightOfficialCombatCatalog c,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        var owner=Unit(c,1,1,5);var target=Unit(c,2,2,6);
        var status=c.CreateStatus(25023,1,1,1,0,10544,2,20,1);owner.Statuses.Add(status);
        var cp=Checkpoint(owner,target);var settlement=new FightDamageSettlement();
        var exec=FightBattleExecutor.Begin(cp,formula,settlement,c,items);
        var component=status.Components.HitCustomCure;var filter=component.Filter;
        Check(component.DefinitionResolved&&component.Supported&&filter.SkillId==10520&&filter.ConditionCount==1
            &&filter.Rate==1&&filter.MaxInvokeCount==-1&&filter.Damage&&!filter.Cure,"Original custom-cure definition differs");
        Check(formula.ReadProperty(Props(status.StatusProperties),formula.PropertyId("SuckHpByDamage"))>0,"Original passive properties were not initialized");
        var skill=c.CreateSkill(10810,0,10029,2,20,1,c.CalculateSkillProps(10810,2,20,1)
            .Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
        var hit=new FightBattleHitDefinition {ActionType=4,DamageProperty=formula.PropertyId("SkillAttack1")};
        var result=new FightBattleHitResult {Formula=new FightDamageFormulaResult {Damage=-1000},
            Settlement=settlement.Apply(1,new FightEntityState(1,1000,1000),new FightEntityState(2,1,1000),-1000)};
        var list=new List<FightBattleStatusSkillInvocation>();
        var method=typeof(FightBattleExecutor).GetMethod("CollectHitCustomCures",BindingFlags.Instance|BindingFlags.NonPublic);
        Action invoke=()=>method.Invoke(exec,new object[]{owner,target,skill,hit,result,.35f,list});
        long expected=formula.CalculateSuckHp(new FightSuckHpFormulaRequest {SourceProps=Props(owner.Properties),SourceType=owner.CharacterType,
            SkillProps=Props(status.StatusProperties),SuckPropType=formula.PropertyId("SuckHpByDamage"),FixedSuckPropType=formula.PropertyId("FixedSuckHp"),
            Damage=-1000,DamageSkillProps=Props(skill.Properties),DamageHitPropType=hit.DamageProperty});
        invoke();
        Check(expected>0&&list.Count==1&&list[0].CustomCure==expected&&list[0].HasCustomCure
            &&list[0].TargetUnitId==1&&list[0].TargetPositionUnitId==1&&list[0].Delay==.35f
            &&list[0].Trigger.SourceSkillItemId==10544,"Overkill HP clipping, source or self-position changed absorption");
        Check(result.Settlement.RealDamageValue==-1&&result.Settlement.DamageValue==-1000,"Overkill fixture did not exercise clipping");
        result.ShieldDamage=1000;result.Formula.Damage=0;
        result.Settlement=settlement.Apply(1,new FightEntityState(1,1000,1000),new FightEntityState(2,1000,1000),0);
        invoke();
        expected=formula.CalculateSuckHp(new FightSuckHpFormulaRequest {SourceProps=Props(owner.Properties),SourceType=owner.CharacterType,
            SkillProps=Props(status.StatusProperties),SuckPropType=formula.PropertyId("SuckHpByDamage"),FixedSuckPropType=formula.PropertyId("FixedSuckHp"),
            Damage=0,DamageSkillProps=Props(skill.Properties),DamageHitPropType=hit.DamageProperty});
        Check(list.Count==2&&list[1].CustomCure==expected,"Shield absorption leaked into the HP damage base");
        int count=filter.InvokeCount;
        result.Type.Dodge=true;invoke();result.Type.Dodge=false;
        result.Type.Blinding=true;invoke();result.Type.Blinding=false;
        result.Type.Immunity=true;invoke();result.Type.Immunity=false;
        result.IsCure=true;invoke();result.IsCure=false;
        status.ForbiddenType=1;invoke();status.ForbiddenType=0;
        status.StackCount=0;invoke();status.StackCount=1;
        skill.CanTriggerChild=false;invoke();
        Check(filter.InvokeCount==count&&list.Count==2,"Excluded hit consumed the counter or triggered healing");
        skill.CanTriggerStatusEntityIds.Add(25023);invoke();
        Check(filter.InvokeCount==++count&&list.Count==3,"Explicit whitelist failed");skill.CanTriggerChild=true;
        filter.MaxInvokeCount=count;invoke();Check(filter.InvokeCount==count,"Invocation limit ignored");filter.MaxInvokeCount=-1;
        var copied=component.Copy();copied.Filter.HitSkillTypes.Add(99);
        Check(filter.HitSkillTypes.Count==0,"Copied filter shares mutable lists");
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        FightBattleExecutor.Restore(cp,formula,settlement,c,items);
        var restored=cp.Units[0].Statuses.Single(s=>s.ClassId==25023).Components.HitCustomCure;
        Check(restored.Filter.InvokeCount==count,"Checkpoint replayed or lost absorption counter");
        restored.DefinitionResolved=false;FightBattleExecutor.Restore(cp,formula,settlement,c,items);
        Check(cp.Units[0].Statuses.Single(s=>s.ClassId==25023).Components.HitCustomCure.Filter.InvokeCount==count,"Legacy restore lost counter");

        foreach(var probe in new[]{(10810,0),(10810,24223),(10810,25136),(10810,24435),(12120,0),(11690,0),(11690,24223),(11690,25136)}) {
            int entityId=probe.Item1,combination=probe.Item2;
            owner=Unit(c,1,1,5);target=Unit(c,2,2,6);var other=Unit(c,3,2,7);
            status=c.CreateStatus(25023,1,1,1,0,10544,2,20,1);owner.Statuses.Add(status);
            if(combination!=0&&combination!=24435)owner.Statuses.Add(c.CreateStatus(combination,1,1,2,0,0,2,20,1));
            if(combination==24435)foreach(var enemy in new[]{target,other})
                enemy.Statuses.Add(c.CreateStatus(24435,enemy.Id,enemy.Id,3,0,0,2,20,1,customShieldValue:1000000000));
            skill=c.CreateSkill(entityId,0,entityId==10810?10029:entityId==11690?10072:0,2,20,1,c.CalculateSkillProps(entityId,2,20,1)
                .Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
            owner.Skills.Add(skill);owner.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
            cp=Checkpoint(owner,target);cp.Units.Add(other);
            var step=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items).Step(0,1).Event;
            int hits=step.DamageEvents.Count(h=>h.SourceId==1&&h.SkillEntityId==entityId&&!h.Dodge&&!h.Blinding&&!h.Immunity);
            var cures=step.DamageEvents.Where(h=>h.SkillEntityId==10520&&h.IsCure).ToArray();
            Check(hits>=(entityId==10810?2:3)&&status.Components.HitCustomCure.Filter.InvokeCount==hits
                &&step.SkillCasts.Count(cast=>cast.SkillEntityId==10520)==hits&&cures.Length==hits
                &&cures.All(h=>h.TargetId==1),"Original multi-target attack did not dispatch self-cure per hit");
            if(entityId==11690)Check(hits==6&&new[]{target.Id,other.Id}.All(id=>step.DamageEvents.Count(h=>h.SkillEntityId==11690&&h.TargetId==id)==3),
                "Scoped original fire must dispatch three independent cures for each of two targets");
            if(combination==0)Check(cures.Sum(h=>h.AfterHp-h.BeforeHp)>0,"Original self-cure did not restore HP");
            else if(combination!=24435)Check(cures.All(h=>h.AfterHp==h.BeforeHp),"Healing interception failed: "+combination);
            if(combination==25136)Check(owner.Statuses.Any(s=>s.ClassId==24435&&s.ShieldRemainValue>0),"Original cure-to-shield chain was bypassed");
            if(combination==24435)Check(step.DamageEvents.Where(h=>h.SkillEntityId==entityId).All(h=>h.ShieldDamage>0&&h.BeforeHp==h.AfterHp),
                "The full-shield routing probe did not fully absorb the original hits");
        }
    }
}
