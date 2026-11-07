using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleHpIncreaseHarness
{
    static void Check(bool value,string message) {if(!value) throw new Exception(message);}
    static FightBattleStepEvent NextCast(FightBattleExecutor runner,int skillId)
    {
        for(int attempt=0;attempt<20;attempt++)
        {
            var step=runner.Step(0,runner.State.Counter+1).Event;
            if(step.SkillCasts.Exists(c=>c.SkillEntityId==skillId)) return step;
        }
        throw new Exception("Fixture did not cast skill "+skillId);
    }
    static FightBattleUnitState Unit(int id,int side,int speed=1)
    {
        var unit=new FightBattleUnitState {Id=id,Side=side,CharacterType=side,Hp=100,MaxHp=1000,
            LastMaxHp=1000,Level=150,SubRank=1,X=5,Y=5,MoveMask=5,
            Turn=new FightTurnUnit {Id=id,Hp=100,Speed=speed}};
        unit.Properties.Add(new FightPropertyValue {Id=1,Value=10001});
        unit.Properties.Add(new FightPropertyValue {Id=2,Value=10003});
        unit.Properties.Add(new FightPropertyValue {Id=3,Value=1000});
        unit.Properties.Add(new FightPropertyValue {Id=4,Value=speed});return unit;
    }
    public static void Run(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        var player=Unit(1,1,1000);var enemy=Unit(2,2);enemy.X=6;enemy.Hp=enemy.Turn.Hp=1000;
        foreach(var participant in new[] {player,enemy})
            foreach(string name in new[] {"BaseCureAdd","BaseBeCureAdd"})
                participant.Properties.Add(new FightPropertyValue {Id=catalog.PropertyType(name),Value=10000});
        var source=catalog.CreateStatus(25034,1,1,1,90010,0,5,20,1);player.Statuses.Add(source);
        var heal=new FightBattleSkillDefinition {EntityId=90010,Slot=0,TargetType=0,HasCustomCure=true,CustomCure=100};
        heal.Range.Add(new FightGridPoint(0,0));
        heal.Hits.Add(new FightBattleHitDefinition {EntityId=90011,DamageType=8,ActionType=5,
            HasCustomDamage=true,CustomDamage=100,Delay=0.4f});
        player.Skills.Add(heal);player.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,ResultRules=new FightBattleResultRules()};
        cp.Units.Add(player);cp.Units.Add(enemy);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        Check(source.Components.HpIncreaseUnit.TriggeredCount==0,"Initial HP was treated as healing.");
        var step=NextCast(runner,90010);
        Check(player.Hp==200,"First actual healing hit did not restore exactly 100 HP: "+player.Hp
            +", events="+step.DamageEvents.Count+", cast targets="+string.Join(",",step.SkillCasts[0].TargetIds)
            +(step.DamageEvents.Count==0 ? "" : ", delta="+step.DamageEvents[0].Damage));
        Check(source.Components.HpIncreaseUnit.TriggeredCount==0 && source.Components.HpIncreaseUnit.AccumulatedHp==100,
            "Sub-threshold healing did not stay in the balance.");
        int previousLayers=player.Statuses.Find(s=>s.ClassId==24242)?.StackCount ?? 0;
        NextCast(runner,90010);
        Check(source.Components.HpIncreaseUnit.TriggeredCount==1 && source.Components.HpIncreaseUnit.AccumulatedHp==50
            && (player.Statuses.Find(s=>s.ClassId==24242)?.StackCount ?? 0)==previousLayers-1,"Two small heals did not consume the source's missing-HP layer with a 50-HP remainder: HP="+player.Hp+", count="+source.Components.HpIncreaseUnit.TriggeredCount+", balance="+source.Components.HpIncreaseUnit.AccumulatedHp);
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        player=cp.Units.Find(u=>u.Id==1);source=player.Statuses.Find(s=>s.ClassId==25034);
        NextCast(runner,90010);
        Check(player.Hp==400 && source.Components.HpIncreaseUnit.TriggeredCount==2
            && source.Components.HpIncreaseUnit.AccumulatedHp==0,"Checkpoint lost fractional progress or repeated startup.");
        player.Skills[0].CustomCure=1000;
        NextCast(runner,90010);
        Check(player.Hp==1000 && source.Components.HpIncreaseUnit.TriggeredCount==6
            && source.Components.HpIncreaseUnit.AccumulatedHp==0,"Overheal entered the actual-healing balance.");
        NextCast(runner,90010);
        Check(source.Components.HpIncreaseUnit.TriggeredCount==6,"Full-HP healing triggered an extra unit.");

        var unit=Unit(1,1);unit.MaxHp=1001;var units=new List<FightBattleUnitState> {unit};
        var fractional=catalog.CreateStatus(25034,1,1,1,0,0,5,20,1);unit.Statuses.Add(fractional);
        FightStatusTriggerRuntime.ApplyHpIncrease(unit,150,catalog,units);
        Check(fractional.Components.HpIncreaseUnit.TriggeredCount==0,"A fractional HP threshold fired early.");
        FightStatusTriggerRuntime.ApplyHpIncrease(unit,1,catalog,units);
        Check(fractional.Components.HpIncreaseUnit.TriggeredCount==1
            && Math.Abs(fractional.Components.HpIncreaseUnit.AccumulatedHp-0.85)<0.0000001,
            "Fractional threshold rounded away its remainder.");
        unit=Unit(1,1);units=new List<FightBattleUnitState> {unit};
        var capped=catalog.CreateStatus(25255,1,1,1,0,0,5,20,1);unit.Statuses.Add(capped);
        FightStatusTriggerRuntime.ApplyHpIncrease(unit,750,catalog,units);
        Check(capped.Components.HpIncreaseUnit.TriggeredCount==5 && capped.Components.HpIncreaseUnit.AccumulatedHp==250,
            "Official per-change maximum five lost its unconsumed balance.");
        capped.Components.HpIncreaseUnit.MaxCount=6;
        FightStatusTriggerRuntime.ApplyHpIncrease(unit,1,catalog,units);
        Check(capped.Components.HpIncreaseUnit.TriggeredCount==6 && capped.Components.HpIncreaseUnit.AccumulatedHp==151,
            "Total maximum did not preserve the unconsumed remainder.");
        capped.Components.HpIncreaseUnit.MaxCount=-1;
        capped.ForbiddenType=1;long before=capped.Components.HpIncreaseUnit.TriggeredCount;
        double balance=capped.Components.HpIncreaseUnit.AccumulatedHp;
        FightStatusTriggerRuntime.ApplyHpIncrease(unit,100,catalog,units);
        Check(capped.Components.HpIncreaseUnit.TriggeredCount==before
            && capped.Components.HpIncreaseUnit.AccumulatedHp==balance,"Forbidden status consumed healing.");
        StackReduction(catalog);
        HpNotifications(catalog,formula);
        Lifesteal(catalog,items,formula);
    }
    static void HpNotifications(FightOfficialCombatCatalog catalog,FightDamageFormula formula)
    {
        var first=Unit(1,1);var second=Unit(2,1);
        var units=new List<FightBattleUnitState> {first,second};
        long firstAmount=0,secondAmount=0;
        first.HpIncreased=(u,amount)=>firstAmount+=amount;
        second.HpIncreased=(u,amount)=>secondAmount+=amount;
        // Both changes settle before notifications. A nested callback changing the second unit
        // must not mutate its already captured property-rebuild delta.
        first.HpUpdated=u=>second.Hp=500;
        first.Properties.Find(p=>p.Id==3).Value=2000;
        second.Properties.Find(p=>p.Id==3).Value=2000;
        FightStatusPropertyRuntime.RebuildAll(units,catalog,formula);
        Check(firstAmount==100 && secondAmount==100,"Property HP notifications lost frozen actual deltas.");
        first.Hp=0;first.HpUpdated=null;firstAmount=0;
        var revival=new FightBattleStatusState {ClassId=90030,OwnerId=1,CreatorId=1,
            RootSkillId=90030,HasBackToLifeComponent=true};
        FightStatusLifecycle.Apply(first,revival,0,40);
        Check(first.Hp==1 && firstAmount==1,"Revival did not notify its actual one-HP restoration exactly once.");
    }
    static void StackReduction(FightOfficialCombatCatalog catalog)
    {
        var unit=Unit(1,1);
        var own=catalog.CreateStatus(24242,1,1,1,12040,0,5,20,1);own.StackCount=5;
        var otherSkill=catalog.CreateStatus(24242,1,1,2,12050,0,5,20,1);otherSkill.StackCount=5;
        var otherCaster=catalog.CreateStatus(24242,1,2,3,12040,0,5,20,1);otherCaster.StackCount=5;
        unit.Statuses.AddRange(new[] {own,otherSkill,otherCaster});
        var template=catalog.CreateStatus(25131,1,1,4,12040,0,5,20,1);
        // Keep the instant status alive for this direct lifecycle test of same-source renewals.
        var live=FightStatusLifecycle.Apply(unit,template,0,4);
        Check(own.StackCount==4 && otherSkill.StackCount==5 && otherCaster.StackCount==5,
            "25131 changed a different source or failed its first application.");
        for(int i=0;i<5;i++)
            Check(ReferenceEquals(live,FightStatusLifecycle.Apply(unit,template,0,5+i)),
                "Same-source renewal replaced the live StackChange counter.");
        Check(!unit.Statuses.Contains(own) && live.Components.StackChange.TriggeredCount==5
            && otherSkill.StackCount==5 && otherCaster.StackCount==5,"25131 failed its five-call cap or zero-layer removal.");
        var saved=FightBattleCheckpointSerializer.Save(new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
            Units=new List<FightBattleUnitState> {unit}});
        unit=FightBattleCheckpointSerializer.Load(saved).Units[0];
        live=unit.Statuses.Find(s=>s.ClassId==25131);
        Check(live.Components.StackChange.TriggeredCount==5,"StackChange counter was not saved.");
        own=catalog.CreateStatus(24242,1,1,10,12040,0,5,20,1);own.StackCount=5;unit.Statuses.Add(own);
        FightStatusLifecycle.Apply(unit,template,0,20);
        Check(own.StackCount==5,"Restoring reset the StackChange lifetime cap.");
    }
    static void Lifesteal(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        var player=Unit(1,1,1000);var enemy=Unit(2,2);enemy.X=6;enemy.Hp=enemy.Turn.Hp=1000;
        int property=catalog.PropertyType("OnceHitHemophagiaPer");
        Check(property>0,"Official OnceHitHemophagiaPer property is absent.");
        player.Properties.Add(new FightPropertyValue {Id=property,Value=10000});
        var source=catalog.CreateStatus(25034,1,1,1,0,0,5,20,1);player.Statuses.Add(source);
        var skill=new FightBattleSkillDefinition {EntityId=90012,Slot=0,TargetType=1};
        skill.Range.Add(new FightGridPoint(1,0));
        skill.Hits.Add(new FightBattleHitDefinition {EntityId=90013,DamageType=8,
            ActionType=1,HasCustomDamage=true,CustomDamage=-200,HemophagiaProperty=property});
        player.Skills.Add(skill);player.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,ResultRules=new FightBattleResultRules()};
        cp.Units.Add(player);cp.Units.Add(enemy);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        NextCast(runner,90012);
        Check(player.Hp==300 && source.Components.HpIncreaseUnit.TriggeredCount==1
            && source.Components.HpIncreaseUnit.AccumulatedHp==50,"Lifesteal did not deliver its actual HP increase exactly once.");
    }
}
