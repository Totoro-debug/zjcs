using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattlePotentialRebirthHarness
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static FightBattleUnitState Unit(int id,long hp,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState {Id=id,Side=id,CharacterType=1,Hp=hp,MaxHp=1000,LastMaxHp=1000,
            Level=70,SubRank=8,X=4+id,Y=4,MoveMask=4,
            Turn=new FightTurnUnit {Id=id,Hp=hp,Speed=id==1?900:1000}};
        foreach(var pair in new Dictionary<string,long>{{"Attack",100},{"Defence",0},{"MaxHp",1000},
            {"Speed",unit.Turn.Speed},{"BaseCureAdd",10000},{"BaseBeCureAdd",10000}})
            unit.Properties.Add(new FightPropertyValue{Id=formula.PropertyId(pair.Key),Value=pair.Value});
        return unit;
    }
    static FightBattleStatusState Attach(FightOfficialCombatCatalog catalog,FightBattleUnitState unit,int rank,int level)
    {
        var status=catalog.CreateStatus(25064,1,1,1,25064,10581,rank,level,8,unit.Properties);
        unit.Statuses.Add(status);return status;
    }
    static FightBattleStepEvent Next(FightBattleExecutor runner)
    {
        for(int index=0;index<20;index++){
            var step=runner.Step(runner.State.Sequence,runner.State.Counter+1).Event;
            if(step.SkillCasts.Any(cast=>cast.SkillEntityId==990501))return step;
        }
        throw new Exception("The deterministic lethal probe did not run.");
    }
    static void NonfatalBoundary(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        var owner=Unit(1,2,formula);var enemy=Unit(2,1000,formula);var listener=Attach(catalog,owner,3,1);
        var attack=new FightBattleSkillDefinition{EntityId=990501,Slot=0,TargetType=1,CanTriggerChild=true};
        attack.Range.Add(new FightGridPoint(-1,0));
        attack.Hits.Add(new FightBattleHitDefinition{EntityId=990502,DamageType=8,ActionType=1,
            HasCustomDamage=true,CustomDamage=1,Delay=.1f});
        enemy.Skills.Add(attack);enemy.SkillState.Slots.Add(new FightSkillSlotState{Slot=0,LimitedTimes=-1});
        var checkpoint=new FightBattleCheckpoint{StageId=1001,RandomSeed=13,MaxRound=8};
        checkpoint.Units.Add(owner);checkpoint.Units.Add(enemy);
        var runner=FightBattleExecutor.Begin(checkpoint,formula,new FightDamageSettlement(),catalog,items);
        var first=Next(runner);
        Check(owner.Hp==1&&listener.HpLimitTriggeredCount==0
            &&!first.SkillCasts.Any(cast=>cast.SkillEntityId==11770),
            "An ordinary hit reducing HP to one incorrectly consumed fatal recovery.");
        var second=Next(runner);
        Check(listener.HpLimitTriggeredCount==1&&owner.Hp>1
            &&second.DamageEvents.Single(hit=>hit.HitEntityId==990502).Damage==0
            &&second.DamageEvents.Single(hit=>hit.SkillEntityId==11770).BeforeHp==1,
            "A lethal hit at one HP failed to trigger recovery when clamped HP damage was zero.");
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        NonfatalBoundary(catalog,items,formula);
        int cases=0;
        foreach(int rank in new[]{3,6,7})foreach(int level in new[]{1,2,20}){
            var owner=Unit(1,500,formula);var enemy=Unit(2,1000,formula);var listener=Attach(catalog,owner,rank,level);
            owner.Statuses.Add(catalog.CreateStatus(24012,1,2,2,11340,10062,3,1,8,enemy.Properties));
            owner.Statuses.Add(catalog.CreateStatus(60421,1,1,3,60420,0,3,1,8,owner.Properties));
            var attack=new FightBattleSkillDefinition{EntityId=990501,Slot=0,TargetType=1,CanTriggerChild=true};
            attack.Range.Add(new FightGridPoint(-1,0));
            attack.Hits.Add(new FightBattleHitDefinition{EntityId=990502,DamageType=8,ActionType=1,
                HasCustomDamage=true,CustomDamage=2000,Delay=.1f});
            enemy.Skills.Add(attack);enemy.SkillState.Slots.Add(new FightSkillSlotState{Slot=0,LimitedTimes=-1});
            var checkpoint=new FightBattleCheckpoint{StageId=1001,RandomSeed=13,MaxRound=8};
            checkpoint.Units.Add(owner);checkpoint.Units.Add(enemy);
            var runner=FightBattleExecutor.Begin(checkpoint,formula,new FightDamageSettlement(),catalog,items);
            var step=Next(runner);var fatal=step.DamageEvents.Single(hit=>hit.HitEntityId==990502);
            var cure=step.DamageEvents.Single(hit=>hit.SkillEntityId==11770);
            var props=catalog.CalculateSkillProps(11770,rank,level,8);
            long expected=(long)(1000m*props[210]/10000m+props[170]);
            Check(fatal.BeforeHp==500&&fatal.AfterHp==1&&listener.HpLimitTriggeredCount==1,
                "Potential rebirth did not clamp lethal damage to one HP exactly once.");
            Check(cure.IsCure&&cure.SkillItemId==10581&&cure.HitEntityId==11771&&cure.DamageEntityId==11772
                &&cure.BeforeHp==1&&cure.DamageValue==expected&&cure.Damage==Math.Min(999,expected)&&cure.AfterHp==1+cure.Damage,
                "Potential rebirth original cultivated maximum-HP healing changed: "+cure.Damage+"/"+expected);
            Check(!owner.Statuses.Any(status=>status.ClassId==24012||status.ClassId==60421)
                &&owner.Statuses.Contains(listener)&&!owner.Ended,
                "Potential rebirth did not remove both dispellable status types or retained its single-use listener incorrectly.");
            Check(cure.HasDisperseNotification&&step.DispersedUnitIds.Contains(1),
                "The original healing damage did not record its actual successful dispersal operation.");
            string saved=FightBattleCheckpointSerializer.Save(checkpoint);
            var restored=FightBattleCheckpointSerializer.Load(saved);
            runner=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            Check(runner.Step(restored.Sequence,restored.Counter).Replayed
                &&FightBattleCheckpointSerializer.Save(restored)==saved,"Potential rebirth restore repeated the recovery.");
            step=Next(runner);owner=restored.Units.Single(unit=>unit.Id==1);
            Check(owner.Hp==0&&!step.SkillCasts.Any(cast=>cast.SkillEntityId==11770)
                &&owner.Statuses.Single(status=>status.ClassId==25064).HpLimitTriggeredCount==1,
                "Potential rebirth gained another fatal recovery after restoration.");
            cases++;
        }
        return "Potential rebirth current graph, cultivated max-HP healing, both dispellable status types, one-HP survival, single use and restore passed (including nonfatal one-HP and zero-settled fatal controls): "+cases;
    }
}
