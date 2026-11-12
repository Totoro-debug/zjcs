using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

public static class BattleUnitEndHarness
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        foreach (int item in new[] {10019,10025,10058}) Natural(catalog,items,formula,item,false);
        Natural(catalog,items,formula,10019,true);
        for (int count = 1; count <= 128; count++) Clock(count);
    }

    private static void Natural(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, int item, bool reduced)
    {
        var cp = new FightBattleCheckpoint {StageId=1001,MaxRound=64,RandomSeed=13,
            Grid=new FightBattleGrid {Width=20,Height=20}};
        for (int id=1;id<=2;id++)
        {
            var u=new FightBattleUnitState {Id=id,Side=id,CharacterType=id==1?1:2,X=5,Y=id==1?5:8,
                Hp=1000000000,MaxHp=1000000000,LastMaxHp=1000000000,Level=150,SubRank=1,MoveMask=5};
            int speed=id==1?1000:1;u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=speed;
            foreach(var pair in new[]{new[]{1,10001},new[]{2,10003},new[]{3,1000000000},new[]{4,speed}})
                u.Properties.Add(new FightPropertyValue {Id=pair[0],Value=pair[1]});
            cp.Units.Add(u);
        }
        FightOfficialSkillItem binding;
        Check(items.TryGet(item,out binding),"Missing original skill item");
        var caster=cp.Units[0];
        if(reduced) caster.Statuses.Add(catalog.CreateStatus(25112,1,1,1,0,10603,5,20,1));
        var skill=catalog.CreateSkill(binding.EntityId,0,item,5,20,1,null);skill.LimitedTimes=1;
        caster.Skills.Add(skill);caster.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        runner.Step(0,1);
        var summons=cp.Units.Where(u=>u.MasterId==1).ToArray();
        Check(summons.Length==(item==10058?2:1),"Original summon count differs");
        var ends=new HashSet<int>();
        for(int step=0;step<512 && ends.Count<summons.Length;step++)
        {
            var result=runner.Step(cp.Sequence,cp.Counter+1);
            foreach(var e in result.Event.UnitEndEvents)
            {
                var unit=summons.Single(u=>u.Id==e.UnitId);
                Check(unit.Ended && unit.Hp>0 && unit.SkillState.Round==(reduced?3:item==10058?2:5),
                    "Removal changed the original status clock");
                Check(Math.Abs(e.Delay-Math.Max(0f,result.Event.ActionDuration))<0.00001f,
                    "Final-round entity removal did not use action end");
                Check(e.StatusId==(item==10019?10712:item==10025?10772:11142) && ends.Add(unit.Id),
                    "Missing/duplicate StatusEnd removal receipt");
                Check(object.ReferenceEquals(cp.Receipts.Last().Event,result.Event),"Receipt was detached from the step");
                var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                var saved=restored.Receipts.Last().Event.UnitEndEvents.Single(v=>v.UnitId==unit.Id);
                Check(saved.Delay==e.Delay && saved.StatusId==e.StatusId,"Disk restore lost exact unit-removal time");
                var replay=runner.Step(cp.Sequence,cp.Counter);
                Check(replay.Replayed && replay.Event.UnitEndEvents.Count==result.Event.UnitEndEvents.Count,
                    "Receipt retry duplicated removals");
                Console.WriteLine("UnitEnd original "+item+" unit="+unit.Id+" delay="+e.Delay+" duration="+result.Event.Duration);
            }
        }
        Check(ends.Count==summons.Length,"Natural original summon expiry did not finish");
    }

    private static void Clock(int subdivisions)
    {
        var visible=new HashSet<int>{3,4,5};var removed=new List<int>();
        var playback=new OfficialBattleUnitEndPlayback(id=>{visible.Remove(id);removed.Add(id);},visible.Contains);
        var events=new[]{new FightBattleUnitEndEvent {UnitId=3,Delay=1},
            new FightBattleUnitEndEvent {UnitId=4,Delay=1},new FightBattleUnitEndEvent {UnitId=5,Delay=2}};
        playback.Enqueue(events);playback.AdvanceTimers(.25f);playback.Enqueue(events);
        Check(playback.Retains(3) && playback.NextDelay==.75f,"Refresh restarted the removal clock");
        for(int n=0;n<subdivisions;n++)
        {
            float elapsed=Math.Min(playback.NextDelay,.75f/subdivisions);
            playback.AdvanceTimers(elapsed);
            if(n<subdivisions-1) Check(visible.Contains(3),"Final action model disappeared early");
            playback.DispatchDue();
        }
        // Consume any representational remainder at the exact next event boundary.
        if(playback.Retains(3)){playback.AdvanceTimers(playback.NextDelay);playback.DispatchDue();}
        Check(removed.SequenceEqual(new[]{3,4}) && visible.Contains(5),"Simultaneous removals lost original order");
        playback.AdvanceTimers(playback.NextDelay);playback.DispatchDue();
        playback.Enqueue(events);
        Check(removed.SequenceEqual(new[]{3,4,5}) && float.IsPositiveInfinity(playback.NextDelay),
            "Cold/repeated receipt resurrected an ended unit");
        playback.Clear();
    }
}
