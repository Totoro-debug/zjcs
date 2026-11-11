using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStatusSecondsHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Near(double actual, double expected, string message) {
        Check(Math.Abs(actual-expected)<1e-7, message+": "+actual+" != "+expected);
    }
    static FightBattleCheckpoint NewBattle(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items) {
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=12,
            Grid=new FightBattleGrid {Width=12,Height=12,DefaultGroundType=0}};
        for(int id=1;id<=2;id++) {
            var unit=new FightBattleUnitState {Id=id,Side=id,CharacterType=1,Level=20,
                Hp=1000000,MaxHp=1000000,LastMaxHp=1000000,X=id*3,Y=5};
            unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=10-id;
            foreach(var pair in new[]{Tuple.Create("MaxHp",1000000L),Tuple.Create("Attack",100L),Tuple.Create("Speed",(long)(10-id))})
                unit.Properties.Add(new FightPropertyValue {Id=catalog.PropertyType(pair.Item1),Value=pair.Item2});
            cp.Units.Add(unit);
        }
        FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        return cp;
    }
    static FightBattleStatusState Launch(FightOfficialCombatCatalog c) =>
        c.CreateStatus(24269,2,1,1,10580,10006,2,20,1);
    static FightBattleStatusState Only(FightBattleCheckpoint cp,int id) {
        var status=cp.Units[1].Statuses.Single();
        Check(status.ClassId==id,"Wrong airborne phase: "+status.ClassId);return status;
    }
    static FightBattleStatusSeconds Clock(FightBattleCheckpoint cp,FightOfficialCombatCatalog c) =>
        new FightBattleStatusSeconds(cp,c,new FightRandomStream(cp.RandomSeed));
    public static void Run(FightOfficialCombatCatalog c,FightDamageFormula f,FightOfficialSkillItemCatalog items) {
        var definition=Launch(c).Components.Seconds;
        Near(definition.DurationSeconds,.7,"Original airborne duration");
        Check(definition.ResetOnNonMoveDamage&&definition.EndOnBlockDamage&&!definition.Started,
            "Original airborne damage rules were not parsed");
        Check(definition.Successors.Single().EntityId==24270,"Original recovery successor");
        var recovery=c.CreateStatus(24270,2,1,2).Components.Seconds;
        Near(recovery.DurationSeconds,.8,"Original recovery duration");
        Check(!recovery.ResetOnNonMoveDamage&&recovery.EndOnBlockDamage,"Original recovery damage rules");
        var copied=definition.Copy();copied.Successors[0].EntityId=1;
        Check(definition.Successors[0].EntityId==24270,"Seconds definition aliases its copy");
        var cp=NewBattle(c,f,items);var clock=Clock(cp,c);var changes=new List<string>();
        clock.StatusChanged+=(owner,status,start)=>changes.Add(status.ClassId+":"+start);
        clock.Enqueue(.4,2,true,false,false,Launch(c));
        clock.Advance(.3);Check(cp.Units[1].Statuses.Count==0,"Launch ran before its hit delay");
        clock.Advance(.2);Near(Only(cp,24269).Components.Seconds.RemainingSeconds,.6,"First hit counted twice");
        clock.Advance(10,true);Near(cp.StatusSeconds.ElapsedSeconds,.5,"Pause advanced seconds");
        Near(Only(cp,24269).Components.Seconds.RemainingSeconds,.6,"Pause consumed airborne timer");
        var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        clock=Clock(saved,c);clock.Advance(.6);
        Near(Only(saved,24270).Components.Seconds.RemainingSeconds,.8,"Saved airborne timer restarted or successor lost residual time");
        var successor=Only(saved,24270);
        Check(successor.ApplyingSkillItemId==10006&&successor.SourceSkillRank==2&&successor.SourceSkillLevel==20
            &&successor.CreatorId==1,"Recovery lost launch skill provenance");
        clock.Advance(.8);Check(saved.Units[1].Statuses.Count==0,"Recovery did not expire");
        // A later successful non-displacement hit resets exactly once. Misses and
        // displacement signals never reset it, even when they share a timestamp.
        cp=NewBattle(c,f,items);clock=Clock(cp,c);
        clock.Enqueue(.2,2,true,false,false,Launch(c));
        clock.Enqueue(.5,2,true,false,true);
        clock.Enqueue(.6,2,false,false,false);
        clock.Enqueue(.7,2,true,false,false);
        clock.Advance(.8);Near(Only(cp,24269).Components.Seconds.RemainingSeconds,.6,"Later damage reset");
        clock.Advance(.6);Only(cp,24270);
        // Block ends the airborne phase immediately; its new recovery phase
        // must not consume that same causative hit a second time.
        cp=NewBattle(c,f,items);clock=Clock(cp,c);
        clock.Enqueue(0,2,true,false,false,Launch(c));clock.Enqueue(.2,2,true,true,true);
        clock.Advance(.2);Near(Only(cp,24270).Components.Seconds.RemainingSeconds,.8,"Block did not end airborne immediately");
        clock.Enqueue(.1,2,true,true,false);clock.Advance(.1);
        Check(cp.Units[1].Statuses.Count==0,"Original recovery block removal was lost");
        cp=NewBattle(c,f,items);clock=Clock(cp,c);
        clock.Enqueue(0,2,true,true,false,Launch(c));clock.Advance(0);Only(cp,24270);
        // Preserve pending commands and their final nested-cast offsets on disk.
        cp=NewBattle(c,f,items);clock=Clock(cp,c);
        clock.Enqueue(1.3,2,true,false,false,Launch(c));clock.Advance(.25);
        saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        Clock(saved,c).Advance(1.1);Near(Only(saved,24269).Components.Seconds.RemainingSeconds,.65,"Pending hit was replayed early or lost its offset");
        Check(saved.StatusSeconds.PendingHits.Count==0,"Consumed hit remained queued");
        // Results do not depend on frame partitioning or rendering frame rate.
        for(int seed=1;seed<=128;seed++) {
            cp=NewBattle(c,f,items);clock=Clock(cp,c);
            clock.Enqueue(.1,2,true,false,false,Launch(c));clock.Enqueue(.5,2,true,false,false);
            clock.Enqueue(.7,2,true,true,false);
            var rng=new Random(seed);double elapsed=0;
            while(elapsed<1.1) {double delta=Math.Min(1.1-elapsed,.01+rng.NextDouble()*.2);clock.Advance(delta);elapsed+=delta;}
            Near(Only(cp,24270).Components.Seconds.RemainingSeconds,.4,"Frame partition changed recovery lifetime");
        }
        bool rejected=false;try {clock.Advance(double.NaN);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"Nonfinite clock input accepted");
        // The real executor must carry the original hit setting through the
        // catalog/collector and queue its final presentation delay, once only.
        foreach(int itemId in new[]{10006,10009,10052}) {
            cp=NewBattle(c,f,items);var source=cp.Units[0];var target=cp.Units[1];
            source.X=5;source.Y=5;source.Direction=2;target.X=5;target.Y=6;
            int entity=items.Required(itemId).EntityId;
            var properties=c.CalculateSkillProps(entity,2,20,1)
                .Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList();
            var skill=c.CreateSkill(entity,0,itemId,2,20,1,properties);
            Check(FightBattleHitCollector.CollectAll(skill).Any(h=>h.AirCombo?.StatusId==24269),
                "Original launch setting was dropped: "+itemId);
            source.Skills.Add(skill);source.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
            var exec=FightBattleExecutor.Begin(cp,f,new FightDamageSettlement(),c,items);
            var result=exec.Step(0,1);
            var commands=cp.StatusSeconds?.PendingHits.Where(h=>h.LaunchStatus!=null).ToArray();
            Check(commands!=null&&commands.Length>0,"Original skill queued no launch: "+itemId);
            Check(target.Statuses.All(s=>s.ClassId!=24269),"Launch applied eagerly during whole-cast settlement");
            foreach(var command in commands) {
                Check(result.Event.DamageEvents.Any(d=>d.TargetId==command.TargetId
                    &&Math.Abs(d.Delay-command.DueSeconds)<1e-7),"Launch lost final hit delay");
                Check(command.LaunchStatus.ApplyingSkillItemId==itemId,"Launch source item changed");
            }
            int pending=cp.StatusSeconds.PendingHits.Count;
            exec.Step(0,1);Check(cp.StatusSeconds.PendingHits.Count==pending,"Receipt replay queued duplicate launches");
            saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored=FightBattleExecutor.Restore(saved,f,new FightDamageSettlement(),c,items);
            double first=saved.StatusSeconds.PendingHits.Where(h=>h.LaunchStatus!=null).Min(h=>h.DueSeconds);
            restored.AdvanceStatusSeconds(first);
            Check(saved.Units[1].Statuses.Any(s=>s.ClassId==24269&&s.Components.Seconds.Started),
                "Restored actual skill did not launch at its saved hit time");
            Check(saved.Units.All(u=>u.StatusStarted==null&&u.StatusEnded==null
                &&u.Statuses.All(s=>s.StackChanged==null)),
                "Seconds playback retained an executor callback in the checkpoint");
        }
    }
}
