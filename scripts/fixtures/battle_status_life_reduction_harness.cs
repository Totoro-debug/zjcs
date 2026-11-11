using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleStatusLifeReductionHarness
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static FightBattleUnitState Unit(int id=1) {
        var u = new FightBattleUnitState {Id=id, Side=id==1?1:2, Hp=100, MaxHp=100, LastMaxHp=100};
        u.Turn.Id=id; u.Turn.Hp=100; u.Turn.Speed=10; return u;
    }
    static FightBattleStatusState Add(FightOfficialCombatCatalog c, FightBattleUnitState u, int cls, int instance, int root=10710) {
        return FightStatusLifecycle.Apply(u,c.CreateStatus(cls,u.Id,u.Id,rootSkillId:root),0,instance);
    }
    public static void Run(FightOfficialCombatCatalog c, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var u=Unit(); var life=Add(c,u,10712,1); var reduce=Add(c,u,24072,2);
        Check(life.DurationRound==5 && life.CurDurationRound==3 && !u.Ended,"Existing five-round life was not shortened to three");
        Add(c,u,24072,3); FightStatusLifeReduction.OnApplied(u,reduce);
        Check(life.CurDurationRound==3,"Renewal or repeated processing deducted twice");
        var extra=Add(c,u,10712,4,9001);
        Check(extra.CurDurationRound==3,"New lifespan did not inherit active reduction");
        long oldIdentity=extra.Components.LifeReductionIdentity;
        extra.RemoveApplyEntityOnEnd=false;
        FightStatusLifecycle.End(u,extra);
        var reused=Add(c,u,10712,4,9001);
        Check(reused.CurDurationRound==3 && reused.Components.LifeReductionIdentity!=oldIdentity,
            "Reused executor instance id suppressed a newly applied lifespan");
        var copy=reduce.Components.Copy(); copy.LifeReduction.ReducedStatusKeys.Clear();
        copy.LifeReduction.TargetTagQuery[0].Tags[0]=0;
        Check(reduce.Components.LifeReduction.ReducedStatusKeys.Count==3
            && reduce.Components.LifeReduction.TargetTagQuery[0].Tags[0]==33554432,"Copy shared receipts or tag masks");

        var ended=Unit(); int endedCount=0; ended.StatusEnded=(owner,status)=>endedCount++;
        var shortLife=Add(c,ended,11142,1); Add(c,ended,24072,2);
        Check(ended.Ended && !ended.Statuses.Contains(shortLife) && endedCount==1,
            "Two-round life did not execute End and RemoveApplyEntity once");
        var pending=Unit(); Add(c,pending,24072,1); shortLife=Add(c,pending,11142,2);
        Check(pending.Ended && !pending.Statuses.Contains(shortLife),"New two-round life did not expire immediately");

        var disabled=Unit(); var template=c.CreateStatus(24072,1,1); template.ForbiddenType=FightStatusLifecycle.ForbiddenStatusTrigger;
        var disabledReduce=FightStatusLifecycle.Apply(disabled,template,0,1);
        var delayed=Add(c,disabled,10712,2);
        Check(delayed.CurDurationRound==5,"Disabled reducer shortened lifespan");
        FightStatusLifecycle.SetForbidden(disabled,disabledReduce,FightStatusLifecycle.ForbiddenStatusTrigger,false);
        Check(delayed.CurDurationRound==3,"Reducer becoming effective failed to shorten existing lifespan");
        FightStatusLifecycle.SetForbidden(disabled,disabledReduce,FightStatusLifecycle.ForbiddenStatusTrigger,true);
        FightStatusLifecycle.SetForbidden(disabled,disabledReduce,FightStatusLifecycle.ForbiddenStatusTrigger,false);
        Check(delayed.CurDurationRound==3,"Reenable charged the same lifespan again");

        var noTag=Unit(); var finite=c.CreateStatus(24031,1,1); finite.DurationRound=5;
        var untouched=FightStatusLifecycle.Apply(noTag,finite,0,1); Add(c,noTag,24072,2);
        Check(untouched.CurDurationRound==5,"Unmatched finite status was shortened");
        var infinite=c.CreateStatus(10712,1,1,rootSkillId:9002); infinite.DurationRound=-1;
        Check(FightStatusLifecycle.Apply(noTag,infinite,0,3).CurDurationRound==-1,"Infinite status was shortened");

        var cp=new FightBattleCheckpoint {StageId=1,MaxRound=10,RandomSeed=73};
        cp.Units.Add(u); cp.Units.Add(Unit(2));
        var loaded=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var restored=FightBattleExecutor.Restore(loaded,formula,new FightDamageSettlement(),c);
        var owner=loaded.Units[0]; var restoredLife=owner.Statuses.Find(x=>x.InstanceId==1);
        var restoredReduce=owner.Statuses.Find(x=>x.ClassId==24072);
        FightStatusLifeReduction.OnApplied(owner,restoredReduce);
        Check(restoredLife.CurDurationRound==3,"Cold restore deducted again");
        var afterRestore=Add(c,owner,10712,5,9003);
        Check(afterRestore.CurDurationRound==3,"Cold restore lost future status handling");

        restoredReduce.Components.LifeReduction=null; restoredLife.Components.GameplayTags=null;
        restored=FightBattleExecutor.Restore(loaded,formula,new FightDamageSettlement(),c);
        FightStatusLifeReduction.OnApplied(owner,restoredReduce);
        Check(restoredLife.CurDurationRound==3,"Legacy restore retroactively deducted lifespan");
        Check(Add(c,owner,10712,6,9004).CurDurationRound==3,"Legacy restore failed to bind new lifespan");

        var caster=Unit(); var enemy=Unit(2);
        caster.X=5; caster.Y=5; enemy.X=6; enemy.Y=5;
        caster.Level=150; caster.SubRank=1; caster.Turn.Speed=1000;
        caster.Properties.Add(new FightPropertyValue {Id=1,Value=10001});
        caster.Properties.Add(new FightPropertyValue {Id=2,Value=10003});
        caster.Properties.Add(new FightPropertyValue {Id=3,Value=1000000});
        caster.Properties.Add(new FightPropertyValue {Id=4,Value=1000});
        caster.Statuses.Add(c.CreateStatus(25112,1,1,1,0,10603,5,20,1));
        var summonSkill=c.CreateSkill(10710,0,10019,5,20,1,null);
        caster.Skills.Add(summonSkill); caster.SkillState.Slots.Add(c.CreateSkillSlot(summonSkill,0));
        var real=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
            Grid=new FightBattleGrid {Width=20,Height=20}};
        real.Units.Add(caster); real.Units.Add(enemy);
        var execution=FightBattleExecutor.Begin(real,formula,new FightDamageSettlement(),c,items);
        execution.Step(0,1);
        var summon=real.Units.Find(x=>x.MasterId==1);
        Check(summon!=null && summon.MonsterId==3181,"Original 10710 summon did not spawn");
        var originalLife=summon.Statuses.Find(x=>x.ClassId==10712);
        Check(originalLife!=null && originalLife.CurDurationRound==3
            && summon.Statuses.Exists(x=>x.ClassId==24072),"Real 25112 creation did not shorten original lifespan");
    }
}
