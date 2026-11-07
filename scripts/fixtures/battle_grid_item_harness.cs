using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleGridItemHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static FightBattleUnitState Unit(int id, int side, int x, int y, int speed)
    {
        var u = new FightBattleUnitState {Id=id,Side=side,CharacterType=side,Hp=1000000,
            MaxHp=1000000,LastMaxHp=1000000,Level=150,SubRank=1,MoveMask=3,X=x,Y=y,
            Turn=new FightTurnUnit {Id=id,Hp=1000000,Speed=speed}};
        u.Properties.Add(new FightPropertyValue {Id=1,Value=10001});
        u.Properties.Add(new FightPropertyValue {Id=2,Value=10003});
        u.Properties.Add(new FightPropertyValue {Id=3,Value=1000000});
        u.Properties.Add(new FightPropertyValue {Id=4,Value=speed});
        return u;
    }
    public static FightBattleCheckpoint Run(FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        Boundaries(catalog,items,formula);
        MultiCell(catalog,items,formula);
        var creator=Unit(1,1,5,5,1000);var reference=Unit(2,2,6,5,1);
        var item=FightGridItemRuntime.Create(3,3320,creator,reference,new FightGridPoint(6,5),0.78f);
        Check(item.OwnerId==1 && item.ReferenceUnitId==2 && item.Runtime.Side==2
            && item.Runtime.Level==150 && item.Runtime.SubRank==1 && item.Runtime.CharacterType==6
            && !item.Runtime.Targetable && item.Runtime.Hp==0,"Grid snapshot identity/actor kind changed.");
        long attack=item.Runtime.Properties[0].Value;
        reference.Properties[0].Value=1;reference.Level=1;reference.Side=1;
        Check(item.Runtime.Properties[0].Value==attack && item.Runtime.BaseProperties[0].Value==attack
            && item.Runtime.Level==150 && item.Runtime.Side==2,"Grid properties/faction followed the reference.");
        item.Runtime.Properties[0].Value=3;
        Check(item.Runtime.BaseProperties[0].Value==attack && reference.Properties[0].Value==1,
            "Grid final/base/reference property lists alias each other.");
        Check(FightOfficialGridItemRules.Get(3320).MoveMask==3
            && FightOfficialGridItemRules.Get(3328).MoveMask==75,"Original grid terrain mask changed.");

        var actor=Unit(1,1,5,5,1000);var enemy=Unit(2,2,6,5,1);
        var skill=catalog.CreateSkill(13050,0,10130,5,20,1,null);
        foreach(var hit in skill.HitGraphEntities)
            if(hit.SummonConfigType=="FightHitSummonGridItemCfg")
            {
                Check(hit.GridSummonCells.Count==9,"Original summon cells were not decoded.");
                // A pre-migration checkpoint has the old graph without these added fields.
                hit.GridSummonCells.Clear();hit.SummonGridItemClassId=0;
            }
        actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
            Grid=new FightBattleGrid {Width=20,Height=20,DefaultGroundType=0},
            ResultRules=new FightBattleResultRules()};
        cp.Units.Add(actor);cp.Units.Add(enemy);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        FightBattleStepEvent step=null;
        for(int i=0;i<10 && cp.GridItems.Count==0;i++) step=runner.Step(0,cp.Counter+1).Event;
        Check(cp.GridItems.Count==9,"Original 13050/13051 did not create its nine burning cells.");
        Check(cp.Units.Count==2,"Grid items entered the ordinary turn/victory roster.");
        var underEnemy=cp.GridItems.Find(g=>g.X==enemy.X && g.Y==enemy.Y);
        Check(underEnemy!=null && underEnemy.Runtime.Statuses.Count==1,
            "Ordinary character occupancy incorrectly blocked burning ground.");
        var status=underEnemy.Runtime.Statuses[0];
        Check(status.ClassId==13052 && status.CreatorId==actor.Id && status.OwnerId==underEnemy.Id
            && status.RoundTarget==1 && status.DurationRound==3 && status.RemoveAtRoundTargetDie
            && status.RemoveApplyEntityOnEnd,"Burning status creator/applicator/lifetime configuration changed.");
        Check(Math.Abs(underEnemy.CreationDelay-0.78f)<0.0001f,
            "Original root and per-cell delays were lost.");
        Check(step.SkillCasts.Exists(c=>c.ActorId==underEnemy.Id && c.SkillEntityId==13055),
            "Grid startup did not release original 13055 from the grid actor.");
        Check(step.DamageEvents.Exists(d=>d.SourceId==underEnemy.Id && d.TargetId==enemy.Id && d.DamageValue<0),
            "Burning ground did not settle an actual original-formula hit.");
        Check(step.DamageEvents.Exists(d=>d.SourceId==underEnemy.Id && Math.Abs(d.Delay-0.78f)<0.0001f),
            "Grid startup settled before the original summon delay.");
        Check(status.RootSkillId==13050 && status.SourceSkillRank==5 && status.SourceSkillLevel==20,
            "Grid status lost original root cultivation.");
        actor.Properties[0].Value=7;
        Check(underEnemy.Runtime.Properties[0].Value!=7,"A live grid followed subsequent caster properties.");
        foreach(var s in actor.SkillState.Slots) s.LimitedTimes=s.ReleasedTimes;
        // Unity serializes null inline classes as their empty field defaults.
        // Exercise that actual checkpoint representation in both test hosts.
        actor.VoodooDoll=new FightVoodooDollLink();enemy.VoodooDoll=new FightVoodooDollLink();
        Check(!actor.IsVoodooDoll && !enemy.IsVoodooDoll,
            "An empty serialized link was mistaken for a real substitute.");
        return cp;
    }

    public static void Restore(FightBattleCheckpoint cp,FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        int count=cp.GridItems.Count;
        var runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        for(int i=0;i<30 && cp.GridItems.Exists(g=>!g.Ended);i++)
            runner.Step(0,cp.Counter+1);
        Check(cp.GridItems.Count==count && cp.GridItems.TrueForAll(g=>g.Ended && g.Runtime.Ended
            && g.Runtime.Statuses.Count==0),"Restored grids failed to expire and end their statuses.");
    }

    public static void CreatorDeath(FightBattleCheckpoint cp,FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        cp.Units[0].Ended=true;
        FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        Check(cp.GridItems.TrueForAll(g=>!g.Ended && g.Runtime.Statuses.Count==1),
            "A living creator's removal was mistaken for death despite RemoveAtRoundTargetDestroy=false.");
        cp.Units[0].Hp=cp.Units[0].Turn.Hp=0;
        FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        Check(cp.GridItems.TrueForAll(g=>g.Ended && g.Runtime.Statuses.Count==0),
            "Creator death failed to remove its statuses and burning grid items.");
    }

    public static void OfficialStage(FightBattleCheckpoint cp,FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        Check(cp.Grid!=null && cp.Grid.Cells.Count>0,"Original stage geometry was not supplied.");
        var actor=cp.Units.Find(u=>u.Id==1);
        actor.Skills.Clear();actor.SkillState.Slots.Clear();actor.Statuses.Clear();
        var skill=catalog.CreateSkill(13050,0,10130,5,20,actor.SubRank,null);
        actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        var runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        FightBattleStepEvent step=null;
        for(int i=0;i<50 && cp.GridItems.Count==0 && !cp.Finished;i++)
            step=runner.Step(cp.Sequence,cp.Counter+1).Event;
        Check(cp.GridItems.Count>0 && cp.GridItems.TrueForAll(g=>cp.Grid.Contains(new FightGridPoint(g.X,g.Y))
            && (cp.Grid.GroundAt(new FightGridPoint(g.X,g.Y))==0 || cp.Grid.GroundAt(new FightGridPoint(g.X,g.Y))==1)),
            "Original stage burning ground did not respect its real terrain/bounds.");
        Check(step.DamageEvents.Exists(d=>d.DamageValue<0 && cp.GridItems.Exists(g=>g.Id==d.SourceId)),
            "Original stage geometry produced no actual burning-grid damage.");
        Console.WriteLine("Original stage grid replay passed: stage "+cp.StageId+", cells "+cp.GridItems.Count+".");
    }

    static void Boundaries(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var actor=Unit(1,1,1,1,1000);var enemy=Unit(2,2,2,1,1);var friend=Unit(3,1,3,1,1);
        var skill=catalog.CreateSkill(13050,0,10130,5,20,1,null);
        actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
            Grid=new FightBattleGrid {Width=4,Height=4,DefaultGroundType=0},
            ResultRules=new FightBattleResultRules()};
        cp.Units.AddRange(new[] {actor,enemy,friend});
        cp.Grid.Cells.Add(new FightBattleGridCell {X=3,Y=2,GroundType=3});
        cp.Grid.BlockedPositions.Add(new FightGridPoint(2,1));
        var previous=FightGridItemRuntime.Create(50,3328,enemy,enemy,new FightGridPoint(2,1),0);
        previous.Runtime.Statuses.Add(catalog.CreateStatus(13052,50,2,51,13480));
        cp.GridItems.Add(previous);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        for(int i=0;i<10 && cp.GridItems.Count==1;i++) runner.Step(0,cp.Counter+1);
        Check(cp.GridItems.FindAll(g=>!g.Ended).Count==8 && cp.GridItems.Count==9,
            "Burning ground ignored the original terrain mask or failed to override an opposing item.");
        Check(previous.Ended && previous.Runtime.Statuses.Count==0,
            "Opposing 3328 or its old status survived the 3320 replacement.");
        Check(!cp.GridItems.Exists(g=>!g.Ended && g.X==3 && g.Y==2)
            && cp.GridItems.Exists(g=>!g.Ended && g.X==2 && g.Y==1)
            && cp.GridItems.Exists(g=>!g.Ended && g.X==3 && g.Y==1),
            "Ground-mask rejection, IgnoreGridBlocked or ordinary occupancy changed.");

        actor=Unit(1,1,1,0,1000);enemy=Unit(2,2,0,0,1);
        skill=catalog.CreateSkill(13050,0,10130,5,20,1,null);
        actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
            Grid=new FightBattleGrid {Width=4,Height=4,DefaultGroundType=0},
            ResultRules=new FightBattleResultRules()};cp.Units.AddRange(new[] {actor,enemy});
        runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        for(int i=0;i<10 && cp.GridItems.Count==0;i++) runner.Step(0,cp.Counter+1);
        Check(cp.GridItems.Count==4 && cp.GridItems.TrueForAll(g=>g.X>=0 && g.Y>=0),
            "Grid generation escaped the battlefield or searched for fallback cells.");
    }

    static void MultiCell(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var actor=Unit(1,1,3,5,1000);var boss=Unit(2,2,6,5,1);
        for(int x=-2;x<=2;x++) for(int y=-2;y<=2;y++) boss.BodyOffsets.Add(new FightGridPoint(x,y));
        var skill=catalog.CreateSkill(13050,0,10130,5,20,1,null);
        actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
            Grid=new FightBattleGrid {Width=20,Height=20,DefaultGroundType=0},
            ResultRules=new FightBattleResultRules()};cp.Units.AddRange(new[] {actor,boss});
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        FightBattleStepEvent step=null;
        for(int i=0;i<10 && cp.GridItems.Count==0;i++) step=runner.Step(0,cp.Counter+1).Event;
        Check(cp.GridItems.Count==9 && step.SkillCasts.FindAll(c=>c.SkillEntityId==13055).Count==9
            && step.DamageEvents.FindAll(d=>d.TargetId==2 && d.DamageValue<0).Count==9,
            "Nine burning cells under one large body did not settle nine independent original hits.");
        var owners=new List<FightBattleUnitState>(cp.Units);
        foreach(var g in cp.GridItems) owners.Add(g.Runtime);
        var invocations=FightStatusTriggerRuntime.CollectMoveNearSkills(boss,owners,
            FightStatusTriggerRuntime.MoveNearEventStandRound,null,new FightRandomStream(13));
        Check(invocations.Count==9,"Standing on nine burning cells merged a large target's grid triggers.");
        var moved=new FightBattleMovementEvent {UnitId=2};
        moved.Path.Add(new FightGridPoint(15,5));moved.Path.Add(new FightGridPoint(6,5));
        invocations=FightStatusTriggerRuntime.CollectMoveNearSkills(boss,owners,
            FightStatusTriggerRuntime.MoveNearEventEnterRange,moved,new FightRandomStream(13));
        Check(invocations.Count==9,"Body entry did not independently enter each overlapping grid.");
    }
}
