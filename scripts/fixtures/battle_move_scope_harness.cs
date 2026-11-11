using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zjcs.Combat.Official;

public static class BattleMoveScopeHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static FightBattleUnitState Unit(int id, int side, int x) {
        var unit = new FightBattleUnitState { Id=id, Side=side, CharacterType=1,
            Level=20, SubRank=1, X=x, Y=5, Hp=1000000, MaxHp=1000000, LastMaxHp=1000000,
            MoveMask=~0, MoveDist=4 };
        unit.Turn.Id=id; unit.Turn.Hp=unit.Hp; unit.Turn.Speed=id==1?1000:1;
        foreach (var p in new[] { (1,100L),(2,0L),(3,1000000L),(4,id==1?1000L:1L),(32,10000L),(33,10000L) })
            unit.Properties.Add(new FightPropertyValue {Id=p.Item1, Value=p.Item2});
        return unit;
    }
    static FightBattleCheckpoint Checkpoint(params FightBattleUnitState[] units) {
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=12,
            Grid=new FightBattleGrid {Width=24,Height=16,DefaultGroundType=0}};
        cp.Units.AddRange(units);return cp;
    }
    public static void Run(FightOfficialCombatCatalog c, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var owner=Unit(1,1,5);var enemy=Unit(2,2,20);
        var status=c.CreateStatus(25027,1,1,1,0,10561,2,20,1);owner.Statuses.Add(status);
        var def=status.Components.MoveScope;
        Check(def.DefinitionResolved && def.TryAtStart && def.GridItemId==3320 && def.MaxCount==-1
            && def.StatusIds.SequenceEqual(new[]{11632}) && def.Cells.Count==5
            && def.Cells.All(cell=>Math.Abs(cell.Rate-.3)<1e-7),"Original move-scope definition differs.");
        var cp=Checkpoint(owner,enemy);var exec=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items);
        var random=(FightRandomStream)typeof(FightBattleExecutor).GetField("random",Private).GetValue(exec);
        // Compare the original five independent rolls, including their order, against
        // the saved random stream. Facing must not reflect world-grid offsets.
        for (int i=0;i<128;i++) {
            owner.Direction=i%2;int before=cp.GridItems.Count;
            var oracle=new FightRandomStream(random.Seed);
            var expected=def.Cells.Where(cell=>oracle.Next()<cell.Rate).Select(cell=>cell.Position+new FightGridPoint(5,5)).ToArray();
            Call(exec,"ApplyMoveScope",owner,status,new FightGridPoint(5,5),.25f);
            var created=cp.GridItems.Skip(before).ToArray();
            Check(created.Select(item=>new FightGridPoint(item.X,item.Y)).SequenceEqual(expected)
                &&random.Seed==oracle.Seed,"Original per-cell random stream or world offsets differ.");
            Check(created.All(item=>item.CreationDelay==.25f && item.Runtime.Statuses.Single().ClassId==11632
                &&item.Runtime.Statuses[0].ApplyingSkillItemId==10561 &&item.Runtime.Statuses[0].SourceSkillRank==2),
                "Burning terrain lost delay, original status or passive source.");
        }
        Check(def.TriggeredCount==128,"Move-scope count differs.");
        var copy=status.Components.Copy();copy.MoveScope.Cells[0].Rate=1;
        Check(def.Cells[0].Rate<1,"Move-scope definition aliases its copy.");
        status.ForbiddenType=1;Call(exec,"ApplyMoveScope",owner,status,new FightGridPoint(5,5),0f);
        status.ForbiddenType=0;status.StackCount=0;Call(exec,"ApplyMoveScope",owner,status,new FightGridPoint(5,5),0f);
        status.StackCount=1;def.MaxCount=128;Call(exec,"ApplyMoveScope",owner,status,new FightGridPoint(5,5),0f);
        Check(def.TriggeredCount==128,"Forbidden, zero-stack or exhausted status triggered.");def.MaxCount=-1;
        // Bind the real action-start dispatcher: active path cells count separately;
        // forced movement reaches the same dispatcher without creating terrain.
        var contextType=typeof(FightBattleExecutor).GetNestedType("MovementStartContext",BindingFlags.NonPublic);
        var context=Activator.CreateInstance(contextType,true);
        contextType.GetField("Casts").SetValue(context,new List<FightBattleSkillCastEvent>());
        contextType.GetField("DamageEvents").SetValue(context,new List<FightBattleDamageEvent>());
        contextType.GetField("Movements").SetValue(context,new List<FightBattleMovementEvent>());
        typeof(FightBattleExecutor).GetField("movementStartContext",Private).SetValue(exec,context);
        def.StartChecked=true;foreach(var cell in def.Cells)cell.Rate=1;
        var path=new FightBattleMovementEvent {UnitId=1,MoveDuration=.2f,Delay=.1f};
        path.Path.AddRange(new[]{new FightGridPoint(5,5),new FightGridPoint(6,5),new FightGridPoint(7,5)});
        int first=cp.GridItems.Count;
        Call(exec,"NotifyMovementStart",path,1,new List<FightBattleMovementEvent>(),null,true);
        Check(def.TriggeredCount==130&&cp.GridItems.Count==first+10
            &&Math.Abs(cp.GridItems[first].CreationDelay-.3f)<1e-6
            &&Math.Abs(cp.GridItems[first+5].CreationDelay-.5f)<1e-6,"Active path cells or arrival delays differ.");
        Call(exec,"NotifyMovementStart",path,1,new List<FightBattleMovementEvent>(),null,false);
        Check(def.TriggeredCount==130,"Forced movement triggered burning terrain.");
        var landing=new FightBattleMovementEvent {UnitId=1,MoveDuration=.3f};
        landing.Path.AddRange(new[]{new FightGridPoint(5,5),new FightGridPoint(9,8)});
        Call(exec,"NotifyMovementStart",landing,1,new List<FightBattleMovementEvent>(),null,true);
        Check(def.TriggeredCount==131,"A single jump sampled airborne cells.");
        FightStatusLifecycle.Apply(owner,c.CreateStatus(25027,1,1,2,0,10561,2,20,1),0,2);
        Check(def.TriggeredCount==131&&ReferenceEquals(status,owner.Statuses.Single()),"Renewal retriggered movement burning.");
        typeof(FightBattleExecutor).GetField("movementStartContext",Private).SetValue(exec,null);
        var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var savedStatus=saved.Units[0].Statuses.Single();savedStatus.Components.MoveScope.StartChecked=false;
        FightBattleExecutor.Restore(saved,formula,new FightDamageSettlement(),c,items);
        Check(savedStatus.Components.MoveScope.StartChecked&&savedStatus.Components.MoveScope.TriggeredCount==131,
            "Checkpoint restoration replayed activation or lost its count.");
        savedStatus.Components.MoveScope=null;
        FightBattleExecutor.Restore(saved,formula,new FightDamageSettlement(),c,items);
        Check(savedStatus.Components.MoveScope.StartChecked&&savedStatus.Components.MoveScope.Cells.Count==5,
            "Legacy checkpoint definition was not restored without activation.");
        // Original AI small-jump path and independent round-start 11630.
        owner=Unit(1,1,5);enemy=Unit(2,2,14);status=c.CreateStatus(25027,1,1,1,0,10561,2,20,1);
        owner.Statuses.Add(status);def=status.Components.MoveScope;foreach(var cell in def.Cells)cell.Rate=1;
        var skill=c.CreateSkill(10810,0,10029,2,20,1,c.CalculateSkillProps(10810,2,20,1)
            .Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
        owner.Skills.Add(skill);owner.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
        owner.Properties.Add(new FightPropertyValue{Id=c.PropertyType("MoveDist"),Value=4});
        cp=Checkpoint(owner,enemy);exec=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items);
        var activation=exec.Step(0,1).Event;
        Check(def.StartChecked&&def.TriggeredCount==1
            &&activation.GridViewEvents.Count(v=>v.Kind==FightBattleGridViewEventKind.Create)==5,
            "First activation did not emit its terrain creation events.");
        int count=def.TriggeredCount;int beforeRound=cp.GridItems.Count;
        FightBattleStepEvent step=null;
        for(int i=1;i<8;i++) {step=exec.Step(0,i+1).Event;if(step.Movements.Any(m=>m.UnitId==1))break;}
        Check(step.Movements.Any(m=>m.UnitId==1),"Original AI produced no movement: owner="+owner.X+" moveDist="+owner.MoveDist+" casts="+string.Join(",",step.SkillCasts.Select(v=>v.SkillEntityId)));
        var jump=step.Movements.Single(m=>m.UnitId==1);
        Check(jump.Path.Count==2&&def.TriggeredCount==count+1,"Original small jump did not check its single landing.");
        Check(step.SkillCasts.Any(cast=>cast.SkillEntityId==11630),"Independent original round-start skill was suppressed.");
        Check(cp.GridItems.Count>beforeRound&&step.GridViewEvents.Count>0,"Original terrain/view creation was not routed.");
    }
}
