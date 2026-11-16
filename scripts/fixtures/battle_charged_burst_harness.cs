using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleChargedBurstHarness
{
    static void Check(bool value,string message) { if(!value) throw new Exception("Charged burst: "+message); }
    static FightBattleUnitState Unit(int id)
    {
        var unit=new FightBattleUnitState {Id=id,Side=id,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000,
            CharacterType=1,X=5,Y=id==1?5:6,Level=50,SubRank=5,
            Turn=new FightTurnUnit {Id=id,Hp=1000000,Speed=id==1?1000:10}};
        foreach(var p in new[]{new long[]{1,1000},new long[]{2,10},new long[]{3,1000000},new long[]{4,unit.Turn.Speed}})
            unit.Properties.Add(new FightPropertyValue {Id=(int)p[0],Value=p[1]});
        return unit;
    }
    static FightBattleStatusState Add(FightOfficialCombatCatalog catalog,FightBattleUnitState owner,int id)
    {
        var status=catalog.CreateStatus(id,owner.Id,owner.Id,id,11000,10041,2,20,5);
        status.StatusProperties=catalog.CalculateSkillProps(id,2,20,5)
            .Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList();
        var condition=status.Components.HitDmgAddPer;
        Check(condition.DurationSkillCount==1 && condition.OnlySkillType==0
            && condition.OnlySkillActions.SequenceEqual(new[]{2}),"original filter differs");
        owner.Statuses.Add(status);return status;
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        foreach(var mode in new[]{0,1,2})
        {
            var actor=Unit(1);var target=Unit(2);
            var precision=Add(catalog,actor,24191);var damage=Add(catalog,actor,24219);
            var skill=catalog.CreateSkill(11010,0,10042,2,20,5,null);
            if(mode==0) skill.ActionType=3;
            if(mode==1) skill.SkillType=1;
            var hit=skill.HitGraphEntities.Single(h=>h.EntityId==11011);
            var resolved=new FightBattleHitDefinition {EntityId=hit.EntityId,ActionType=4};
            var context=new FightConditionalHitProperties(actor,skill,items,new[]{actor,target},catalog);
            var expected=new Dictionary<int,long>();
            foreach(var status in new[]{precision,damage}) foreach(var p in status.StatusProperties)
                expected[p.Id]=(expected.TryGetValue(p.Id,out long previous)?previous:0)+p.Value;
            for(int i=0;i<2;i++) {
                var actual=new Dictionary<int,long>();
                context.Merge(target,resolved,new FightDamageTypeData(),false,null,actual);
                Check(mode==2 ? expected.All(p=>actual.TryGetValue(p.Key,out long v)&&v==p.Value)
                    : actual.Count==0,"incorrect properties for action/type mode "+mode);
                Check(actor.Statuses.Contains(precision)&&actor.Statuses.Contains(damage),
                    "a multi-hit cast expired the original statuses early");
            }
            context.EndCast();
            Check(mode==2 ? !actor.Statuses.Contains(precision)&&!actor.Statuses.Contains(damage)
                : actor.Statuses.Contains(precision)&&actor.Statuses.Contains(damage),
                "nonmatching cast consumed the status or matching cast did not consume it");
        }
        ActualCast(catalog,formula,items);
        return "Charged burst passed: original cast and damage bonus, action/type filters, multi-hit lifetime and consumption.";
    }

    static FightBattleStepEvent Next(FightBattleExecutor runner,int skillId)
    {
        for(int i=0;i<40;i++) {
            var step=runner.Step(0,runner.State.Counter+1).Event;
            if(step.SkillCasts.Any(c=>c.SkillEntityId==skillId)) return step;
        }
        throw new Exception("Charged burst fixture did not cast "+skillId);
    }

    static void ActualCast(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        var actor=Unit(1);var target=Unit(2);
        var props=catalog.CalculateSkillProps(11000,2,20,5).Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList();
        var buff=catalog.CreateSkill(11000,0,10041,2,20,5,props);
        actor.Skills.Add(buff);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(buff,0));
        var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=30,RandomSeed=91,
            Grid=new FightBattleGrid {Width=12,Height=12}};
        cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        Next(runner,11000);
        Check(actor.Statuses.Any(s=>s.ClassId==24191)&&actor.Statuses.Any(s=>s.ClassId==24219),
            "original buff cast did not apply both statuses");
        actor.Skills.Clear();actor.SkillState.Slots.Clear();
        props=catalog.CalculateSkillProps(11010,2,20,5).Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList();
        var attack=catalog.CreateSkill(11010,0,10042,2,20,5,props);
        actor.Skills.Add(attack);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(attack,0));
        runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        string saved=FightBattleCheckpointSerializer.Save(cp);
        var restored=FightBattleCheckpointSerializer.Load(saved);
        var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
        var control=FightBattleCheckpointSerializer.Load(saved);
        control.Units.Single(u=>u.Id==1).Statuses.RemoveAll(s=>s.ClassId==24191||s.ClassId==24219);
        var controlRunner=FightBattleExecutor.Restore(control,formula,new FightDamageSettlement(),catalog,items);
        var withBuff=Next(runner,11010);Next(resumed,11010);var withoutBuff=Next(controlRunner,11010);
        Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),
            "restored original buff diverged during its next attack");
        long actual=-withBuff.DamageEvents.Single(e=>e.SkillEntityId==11010&&e.TargetId==2).Damage;
        long baseline=-withoutBuff.DamageEvents.Single(e=>e.SkillEntityId==11010&&e.TargetId==2).Damage;
        Check(baseline>0 && Math.Abs(actual-baseline*1.5)<=2,
            "original damage bonus differs: "+actual+" vs "+baseline);
        Check(!actor.Statuses.Any(s=>s.ClassId==24191||s.ClassId==24219),
            "original statuses remained after the next damage cast");
    }
}
