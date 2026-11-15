using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleVoidMirageHarness
{
    static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException("Void mirage: "+message); }
    static FightBattleUnitState Unit(int id,long hp,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState {Id=id,Side=id,CharacterType=1,Level=50,SubRank=1,
            X=4+id,Y=4,Hp=hp,MaxHp=1000,LastMaxHp=1000,
            Turn=new FightTurnUnit {Id=id,Hp=hp,Speed=id==1?1000:10}};
        foreach(var entry in new Dictionary<int,long>{{1,100},{2,10},{3,1000},{4,unit.Turn.Speed}})
            unit.Properties.Add(new FightPropertyValue {Id=entry.Key,Value=entry.Value});
        unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("CritAvoidPercent"),Value=10000});
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        foreach(var damage in new long[]{499,500,501,1000})
        {
            var actor=Unit(1,1000,formula);var target=Unit(2,1000,formula);
            var props=catalog.CalculateSkillProps(25022,2,20,1)
                .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
            var passive=catalog.CreateStatus(25022,2,2,1,25022,10556,2,20,1,props);
            Check(passive.HpLessThanPercentage==.5f&&!passive.HpLessThanTryAtStart
                &&!passive.HpLessThanInvokeOnDie&&passive.HpLessThanMaxCount==1,"Original threshold configuration changed");
            target.Statuses.Add(passive);
            var skill=new FightBattleSkillDefinition {EntityId=990501,Slot=0,TargetType=1,ActionType=2};
            skill.Range.Add(new FightGridPoint(1,0));
            skill.Hits.Add(new FightBattleHitDefinition {EntityId=990502,ActionType=4,DamageType=8,
                HasCustomDamage=true,CustomDamage=damage});
            actor.Skills.Add(skill);actor.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=1});
            var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=91,
                Grid=new FightBattleGrid {Width=12,Height=12}};
            cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
            var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=runner.Step(0,1).Event;resume.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"Cold continuation differs");
            var hit=step.DamageEvents.Single(x=>x.SkillEntityId==990501);
            Check(hit.BeforeHp-hit.AfterHp==damage,"Controlled damage did not settle");
            bool expected=damage==500||damage==501;
            var shield=target.Statuses.SingleOrDefault(x=>x.ClassId==24213);
            var dodge=target.Statuses.SingleOrDefault(x=>x.ClassId==24209);
            Check((shield!=null)==expected&&(dodge!=null)==expected&&passive.HpLessThanTriggeredCount==(expected?1:0),
                "Threshold, survival gate or simultaneous effects differ for damage "+damage);
            if(expected)
            {
                Check(shield.DurationRound==2&&dodge.DurationRound==2&&!shield.CanDispersed&&!dodge.CanDispersed,
                    "Protection duration or dispel rules differ");
                Check(shield.ApplyingSkillItemId==10556&&dodge.ApplyingSkillItemId==10556
                    &&shield.SourceSkillRank==2&&shield.SourceSkillLevel==20&&dodge.SourceSkillRank==2,
                    "Effects lost passive source or cultivation");
                Check(shield.ShieldInitialized&&shield.ShieldRemainValue>0,"Original shield was not initialized");
                FightStatusLifecycle.End(target,shield);FightStatusLifecycle.End(target,dodge);
                FightStatusTriggerRuntime.Apply(target,catalog,false,units:cp.Units);
                Check(!target.Statuses.Any(x=>x.ClassId==24213||x.ClassId==24209),"Protection triggered twice");
            }
        }
        var low=Unit(3,499,formula);low.Statuses.Add(catalog.CreateStatus(25022,3,3,1,25022,10556,2,20,1));
        FightStatusTriggerRuntime.Apply(low,catalog,true,units:new[]{low});
        Check(!low.Statuses.Any(x=>x.ClassId==24213||x.ClassId==24209),"TryAtStart=false was ignored");
        return "Void mirage passed: 50% boundary, lethal/start exclusions, two original statuses, source, shield and checkpoint.";
    }
}
