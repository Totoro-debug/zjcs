using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStatusHitScopeHarness
{
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    static FightBattleUnitState Unit(int id,int side,int x,int y) {
        var u=new FightBattleUnitState {Id=id,Side=side,X=x,Y=y,Hp=1000000000,MaxHp=1000000000,LastMaxHp=1000000000,Level=150,SubRank=1};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:1;
        foreach(var p in new[]{new FightPropertyValue{Id=1,Value=10001},new FightPropertyValue{Id=2,Value=10003},
            new FightPropertyValue{Id=3,Value=1000000000},new FightPropertyValue{Id=4,Value=id==1?1000:1}})u.Properties.Add(p);
        return u;
    }
    static FightBattleStatusState Passive(FightOfficialCombatCatalog c,FightBattleUnitState owner) {
        var s=c.CreateStatus(25045,owner.Id,owner.Id,1,0,10576,2,20,1);owner.Statuses.Add(s);return s;
    }
    public static void Run(FightOfficialCombatCatalog c,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        var owner=Unit(1,1,5,5);var target=Unit(2,2,6,5);var passive=Passive(c,owner);
        var component=passive.Components.HitScope;
        Check(component.DefinitionResolved&&component.SkillElementTypes.SequenceEqual(new[]{102})
            &&component.ConditionCount==1&&Math.Abs(component.Rate-.2f)<1e-7&&component.SkillId==11580&&component.MaxCount==-1,
            "Original fire-scope configuration differs");
        var skill=c.CreateSkill(11690,0,10072,2,20,1,null);
        var result=new FightBattleHitResult();var list=new List<FightBattleStatusSkillInvocation>();
        var rng=new FightRandomStream(73);var expected=new FightRandomStream(73);int successes=0;
        for(int i=0;i<128;i++){
            if(expected.Next()<=component.Rate)successes++;
            FightStatusHitScope.Collect(owner,target,skill,result,.25f,list,rng);
        }
        Check(successes>0&&successes<128&&list.Count==successes&&component.TriggeredCount==successes,
            "Original probability was not drawn once for each successful occurrence");
        Check(list.All(x=>x.Trigger.EntityId==11580&&x.Trigger.SourceSkillItemId==10576
            &&x.Trigger.SourceSkillRank==2&&x.Trigger.SourceSkillLevel==20&&x.Delay==.25f
            &&x.HitOrigin.Value.X==6&&x.HitOrigin.Value.Y==5&&x.AllowDeadTarget),
            "Passive source, hit delay or frozen cell was not retained");
        target.X=9;Check(list[0].HitOrigin.Value.X==6,"Later displacement changed the captured cell");target.X=6;
        int before=component.TriggeredCount;
        foreach(int mode in new[]{0,1,2}){
            result.Type.Dodge=mode==0;result.Type.Immunity=mode==1;result.Type.Blinding=mode==2;
            FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);
        }
        Check(component.TriggeredCount==before,"Failed hits triggered scope");result=new FightBattleHitResult();
        component.Rate=1;result.Type.Block=true;
        int blockedBefore=list.Count;FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);
        Check(list.Count==blockedBefore+1,"A successful blocked fire hit was dropped");
        result=new FightBattleHitResult();
        int count=list.Count;
        passive.ForbiddenType=1;FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);passive.ForbiddenType=0;
        passive.StackCount=0;FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);passive.StackCount=1;
        int element=skill.ElementType;skill.ElementType=104;FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);skill.ElementType=element;
        var child=c.CreateSkill(11580,-1,10576,2,20,1,null);FightStatusHitScope.Collect(owner,target,child,result,0,list,rng);
        Check(list.Count==count,"Forbidden, zero-layer, other element or recursive child triggered");
        skill.CanTriggerChild=false;FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);
        Check(list.Count==count,"Disallowed skill triggered passive");
        skill.CanTriggerStatusEntityIds.Add(25045);FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);
        Check(list.Count==count+1,"Explicit original status whitelist was ignored");skill.CanTriggerChild=true;
        component.MaxCount=component.TriggeredCount;FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);
        Check(list.Count==count+1,"Maximum trigger count was ignored");component.MaxCount=-1;
        component.ConditionCount=2;component.ConditionProgress=0;
        FightStatusHitScope.Collect(owner,target,skill,result,0,list,rng);Check(component.ConditionProgress==1,"Threshold progress was not retained");
        var copied=passive.Components.Copy();copied.HitScope.SkillElementTypes.Clear();
        Check(component.SkillElementTypes.Count==1,"Copy shared element filters");
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(owner);cp.Units.Add(target);
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),c,items);
        var restored=cp.Units[0].Statuses.Single(x=>x.ClassId==25045).Components.HitScope;
        Check(restored.TriggeredCount==component.TriggeredCount&&restored.ConditionProgress==1,"Restore lost or replayed counters");
        list.Clear();FightStatusHitScope.Collect(cp.Units[0],cp.Units[1],skill,result,0,list,rng);
        Check(list.Count==1&&restored.ConditionProgress==0,"Resumed threshold did not trigger once");
        restored.DefinitionResolved=false;int saved=restored.TriggeredCount;
        FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),c,items);
        restored=cp.Units[0].Statuses.Single(x=>x.ClassId==25045).Components.HitScope;
        Check(restored.DefinitionResolved&&restored.TriggeredCount==saved&&Math.Abs(restored.Rate-.2f)<1e-7,"Legacy definition did not retain progress and restore original values");

        // Actual original 11690 has three hit segments; two enemies are in its authored area.
        owner=Unit(1,1,5,5);target=Unit(2,2,6,5);var other=Unit(3,2,7,5);
        passive=Passive(c,owner);passive.Components.HitScope.Rate=1; // Isolated deterministic routing probe.
        skill=c.CreateSkill(11690,0,10072,2,20,1,null);owner.Skills.Add(skill);owner.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
        cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(owner);cp.Units.Add(target);cp.Units.Add(other);
        var exec=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items);
        var step=exec.Step(0,1).Event;
        int hits=step.DamageEvents.Count(x=>x.SourceId==1&&x.SkillEntityId==11690&&!x.Dodge&&!x.Immunity&&!x.Blinding);
        int casts=step.SkillCasts.Count(x=>x.SkillEntityId==11580);
        Check(hits>=6&&casts==hits&&passive.Components.HitScope.TriggeredCount==hits,
            "Real multi-target/multi-hit fire routing differs: hits="+hits+" casts="+casts);
        foreach(var enemy in new[]{target,other})
            Check(cp.GridItems.Any(x=>x.GridItemClassId==3320&&x.X==enemy.X&&x.Y==enemy.Y
                &&x.Runtime.Statuses.Any(status=>status.ClassId==11582&&status.RootSkillId==11580
                    &&status.ApplyingSkillItemId==10576&&status.CreatorId==owner.Id
                    &&status.SourceSkillRank==2&&status.SourceSkillLevel==20)),
                "Burning ground position, original status or passive source differs");
        var creates=step.GridViewEvents.Where(x=>x.Kind==FightBattleGridViewEventKind.Create).ToArray();
        var childCasts=step.SkillCasts.Where(x=>x.SkillEntityId==11580).ToArray();
        Check(creates.Length==hits&&creates.Select(x=>x.Delay).SequenceEqual(childCasts.Select(x=>x.Delay)),
            "Burning-ground visual creation did not retain each hit's delay");

        // A lethal hit still owns its hit-time cell when its queued child runs.
        owner=Unit(1,1,5,5);target=Unit(2,2,6,5);other=Unit(3,2,7,5);
        target.Hp=target.Turn.Hp=other.Hp=other.Turn.Hp=1;
        foreach(var enemy in new[]{target,other}) enemy.Properties.Single(p=>p.Id==2).Value=0;
        Passive(c,owner).Components.HitScope.Rate=1;
        skill=c.CreateSkill(11690,0,10072,2,20,1,c.CalculateSkillProps(11690,2,20,1)
            .Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList());
        owner.Skills.Add(skill);owner.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
        cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(owner);cp.Units.Add(target);cp.Units.Add(other);
        step=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items).Step(0,1).Event;
        Check(target.Hp==0&&other.Hp==0,"The lethal-hit probe did not kill both original targets: "
            +target.Hp+","+other.Hp+"; hits="+string.Join(",",step.DamageEvents.Select(h=>h.Damage)));
        foreach(var enemy in new[]{target,other})
            Check(cp.GridItems.Any(x=>x.GridItemClassId==3320&&x.X==enemy.X&&x.Y==enemy.Y),
                "Lethal damage dropped the original burning ground at the dead target's hit-time cell");

    }
}
