using System;
using System.Linq;
using System.Reflection;
using Zjcs.Combat.Official;

public static class BattleDarkHitHarness
{
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    static FightBattleUnitState Unit(FightOfficialCombatCatalog c,int id,int side,int x,int y) {
        var u=new FightBattleUnitState {Id=id,Side=side,X=x,Y=y,Hp=1000000000,MaxHp=1000000000,LastMaxHp=1000000000,Level=150,SubRank=1};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:1;
        foreach(var p in new[]{new FightPropertyValue{Id=1,Value=10001},new FightPropertyValue{Id=2,Value=0},
            new FightPropertyValue{Id=3,Value=1000000000},new FightPropertyValue{Id=4,Value=id==1?1000:1}})u.Properties.Add(p);
        foreach(string name in new[]{"BaseEffectRate","BaseEffectDodge"})
            u.Properties.Add(new FightPropertyValue {Id=c.PropertyType(name),Value=10000});
        return u;
    }
    public static void Run(FightOfficialCombatCatalog c,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        var owner=Unit(c,1,1,5,5);var target=Unit(c,2,2,6,5);
        var status=c.CreateStatus(25031,1,1,1,0,10565,2,20,1);owner.Statuses.Add(status);
        var def=status.Components.DarkHit;
        Check(def.DefinitionResolved&&def.Supported&&def.Elements.SequenceEqual(new[]{104})
            &&def.IgnoredSkills.SequenceEqual(new[]{11360})&&def.Statuses.Single().EntityId==24222
            &&Math.Abs(def.Statuses[0].BasePercent-.4)<1e-7&&def.Statuses[0].AffectedByProp,"Original dark-hit definition differs");
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(owner);cp.Units.Add(target);
        var exec=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items);
        var method=typeof(FightBattleExecutor).GetMethod("ApplyDarkHitStatuses",BindingFlags.Instance|BindingFlags.NonPublic);
        var rng=(FightRandomStream)typeof(FightBattleExecutor).GetField("random",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(exec);
        var skill=c.CreateSkill(10810,0,10029,2,20,1,null);
        var hit=new FightBattleHitResult();int successes=0;
        for(int i=0;i<128;i++) {
            target.Statuses.Clear();
            var expected=new FightRandomStream(rng.Seed);
            double chance=c.CalculateStatusApplicationRate(owner,target,24222,def.Statuses[0].BasePercent,true,formula);
            bool applies=expected.Next()<=chance;
            method.Invoke(exec,new object[]{owner,target,skill,hit});
            Check(target.Statuses.Any(s=>s.ClassId==24222)==applies,"Original probability/resistance diverged at "+i);
            if(applies) {
                successes++;var applied=target.Statuses.Single(s=>s.ClassId==24222);
                Check(applied.ApplyingSkillItemId==10565&&applied.SourceSkillRank==2&&applied.SourceSkillLevel==20
                    &&applied.CreatorId==owner.Id,"Dark status lost passive source properties");
            }
        }
        Check(successes>0&&successes<128&&def.TriggerCount==128,"Each successful occurrence must count once: successes="+successes+" count="+def.TriggerCount
            +" active="+status.ForbiddenType+"/"+status.StackCount+" chance="+c.CalculateStatusApplicationRate(owner,target,24222,def.Statuses[0].BasePercent,true,formula));
        int count=def.TriggerCount;
        Action invoke=()=>method.Invoke(exec,new object[]{owner,target,skill,hit});
        hit.Type.Dodge=true;invoke();hit.Type.Dodge=false;
        hit.Type.Blinding=true;invoke();hit.Type.Blinding=false;
        hit.Type.Immunity=true;invoke();hit.Type.Immunity=false;
        target.Side=owner.Side;invoke();target.Side=2;
        skill.ElementType=102;invoke();skill.ElementType=104;
        skill.EntityId=11360;invoke();skill.EntityId=10810;
        status.ForbiddenType=1;invoke();status.ForbiddenType=0;
        status.StackCount=0;invoke();status.StackCount=1;
        skill.CanTriggerChild=false;invoke();
        Check(def.TriggerCount==count,"Excluded occurrence changed trigger count");
        skill.CanTriggerStatusEntityIds.Add(25031);invoke();
        Check(def.TriggerCount==++count,"Explicit status whitelist was ignored");
        skill.CanTriggerChild=true;
        hit.Type.Block=true;invoke();hit.Type.Block=false;
        Check(def.TriggerCount==++count,"Successful blocked zero-damage hit must count");
        hit.ShieldDamage=123;hit.Formula=new FightDamageFormulaResult {Damage=-123};invoke();
        Check(def.TriggerCount==++count,"Full shield absorption must not require HP damage");
        hit.ShieldDamage=0;hit.Formula=null;
        def.MaxTriggerCount=count;invoke();Check(def.TriggerCount==count,"Maximum count ignored");def.MaxTriggerCount=-1;
        var copy=status.Components.Copy();copy.DarkHit.Statuses[0].BasePercent=0;copy.DarkHit.Elements.Clear();
        Check(def.Statuses[0].BasePercent>0&&def.Elements.Count==1,"Definition copy shares mutable data");
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),c,items);
        var restored=cp.Units[0].Statuses.Single(s=>s.ClassId==25031).Components.DarkHit;
        Check(restored.TriggerCount==count,"Checkpoint replayed or lost counter");
        restored.DefinitionResolved=false;
        FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),c,items);
        restored=cp.Units[0].Statuses.Single(s=>s.ClassId==25031).Components.DarkHit;
        Check(restored.Supported&&restored.DefinitionResolved&&restored.TriggerCount==count,"Legacy definition restoration lost counter");

        foreach(int entityId in new[]{10810,12120}) {
        owner=Unit(c,1,1,5,5);target=Unit(c,2,2,6,5);var other=Unit(c,3,2,7,5);
        status=c.CreateStatus(25031,1,1,1,0,10565,2,20,1);owner.Statuses.Add(status);
        status.Components.DarkHit.Statuses[0].BasePercent=1;status.Components.DarkHit.Statuses[0].AffectedByProp=false;
        skill=c.CreateSkill(entityId,0,entityId==10810?10029:0,2,20,1,c.CalculateSkillProps(entityId,2,20,1)
            .Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList());
        owner.Skills.Add(skill);owner.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
        cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(owner);cp.Units.Add(target);cp.Units.Add(other);
        exec=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items);
        var step=exec.Step(0,1).Event;
        int hits=step.DamageEvents.Count(h=>h.SourceId==1&&h.SkillEntityId==entityId&&!h.Dodge&&!h.Immunity&&!h.Blinding);
        Check(hits>=(entityId==10810?2:3)&&status.Components.DarkHit.TriggerCount==hits
            &&(entityId==10810?new[]{target,other}:new[]{target}).All(u=>u.Statuses.Any(s=>s.ClassId==24222&&s.ApplyingSkillItemId==10565)),
            "Actual original multi-target/segment dark attack did not dispatch each successful hit: "+entityId+"/"+hits);
        var views=step.StatusViewEvents.Where(v=>v.Status.ClassId==24222).ToArray();
        var starts=views.Where(v=>v.Kind==FightBattleStatusViewEventKind.Start).ToArray();
        Check(starts.Length==(entityId==10810?2:1),"Dark status views lost distinct target/instance starts");
        foreach(var start in starts) {
            Check(start.Status.HasRoundStart&&start.Status.RoundStartRemainingCount==3
                &&step.DamageEvents.Any(d=>d.SourceId==1&&d.TargetId==start.OwnerId
                    &&d.SkillEntityId==entityId&&Math.Abs(d.Delay-start.Delay)<1e-5),
                "Dark status visual must begin at its actual original hit, after approach/nested cast offsets");
        }
        if(entityId==12120) Check(views.Any(v=>v.Kind==FightBattleStatusViewEventKind.Update
            &&v.Status.RoundStartRemainingCount>=6),"A nonstacking duration merge lost original custom-count updates");
        int receipts=views.Length;
        Check(exec.Step(0,1).Event.StatusViewEvents.Count(v=>v.Status.ClassId==24222)==receipts,
            "Replaying a step duplicated native status transitions");
        var restoredCheckpoint=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var replay=FightBattleExecutor.Restore(restoredCheckpoint,formula,new FightDamageSettlement(),c,items).Step(0,1).Event;
        Check(replay.StatusViewEvents.Where(v=>v.Status.ClassId==24222).Select(v=>
                v.Kind+":"+v.OwnerId+":"+v.Delay+":"+v.Status.RoundStartRemainingCount)
            .SequenceEqual(views.Select(v=>v.Kind+":"+v.OwnerId+":"+v.Delay+":"+v.Status.RoundStartRemainingCount)),
            "Cold checkpoints lost original status transition times/counters");
        // A receipt must keep the original start count after the live component changes.
        var firstTarget=cp.Units.Single(u=>u.Id==starts[0].OwnerId);
        firstTarget.Statuses.Single(s=>s.InstanceId==starts[0].Status.InstanceId).RoundStartTriggeredCount++;
        Check(starts[0].Status.RoundStartRemainingCount==3,"A start receipt retained mutable live component state");
        }

        // Start with the actual authored three-invocation status, then run the
        // ordinary turn executor through count exhaustion and its final child skill.
        owner=Unit(c,1,1,5,5);target=Unit(c,2,2,6,5);
        target.Turn.Speed=1000;target.Properties.Single(p=>p.Id==4).Value=1000;
        target.Statuses.Add(c.CreateStatus(24222,2,1,1,10810,10565,2,20,1,owner.Properties));
        cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,
            Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(owner);cp.Units.Add(target);
        exec=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items);
        var counters=new System.Collections.Generic.List<int>();int ended=0,children=0;
        for(int counter=1;counter<=16&&target.Statuses.Any(s=>s.ClassId==24222);counter++) {
            var step=exec.Step(0,counter).Event;
            counters.AddRange(step.StatusViewEvents.Where(v=>v.Status.ClassId==24222
                &&v.Kind==FightBattleStatusViewEventKind.Update).Select(v=>v.Status.RoundStartRemainingCount));
            ended+=step.StatusViewEvents.Count(v=>v.Status.ClassId==24222&&v.Kind==FightBattleStatusViewEventKind.End);
            children+=step.SkillCasts.Count(v=>v.SkillEntityId==11360);
            cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            // Unity JsonUtility materializes null inline component objects. A default,
            // unresolved seconds payload must not hide an authored round-based view.
            foreach(var restoredStatus in cp.Units.SelectMany(u=>u.Statuses).Where(s=>s.ClassId==24222))
                restoredStatus.Components.Seconds=new FightBattleStatusSecondsState();
            exec=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),c,items);
            target=cp.Units.Single(u=>u.Id==2);
        }
        Check(counters.SequenceEqual(new[]{2,1,0})&&ended==1&&children==3&&!target.Statuses.Any(s=>s.ClassId==24222),
            "Original status counter/lifetime events diverged across actual turns and cold restores: "
            +string.Join(",",counters)+" ends="+ended+" children="+children);
    }
}
