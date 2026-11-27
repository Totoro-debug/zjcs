using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;
public static class BattleSwiftWindDanceHarness
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException("Swift Wind Dance: "+message);}
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        Check(items.Required(10086).TryAtStartType!=0,"original opening attempt is missing");
        foreach(int rank in new[]{3,6,7})
        foreach(int level in new[]{1,2,20})
        foreach(int seed in new[]{13,104729})
        foreach(bool opening in new[]{false,true})
        foreach(bool largeActor in new[]{false,true})
        foreach(bool largeAlly in new[]{false,true})
        {
            var cp=new FightBattleCheckpoint{StageId=1,MaxRound=50,RandomSeed=seed,
                Grid=new FightBattleGrid{Width=24,Height=24,DefaultGroundType=2}};
            cp.Units.AddRange(new[]{Unit(1,1,1,10,10,1000,formula),Unit(2,2,2,11,11,100,formula),
                Unit(3,1,4,10,13,200,formula),Unit(4,1,1,14,10,200,formula),
                Unit(5,1,3,9,11,100,formula),Unit(6,1,7,9,9,100,formula)});
            var actor=cp.Units[0];
            if(largeActor)actor.BodyOffsets.AddRange(new[]{new FightGridPoint(),new FightGridPoint(1,0)});
            if(largeAlly){cp.Units[2].BodyOffsets.AddRange(new[]{new FightGridPoint(),new FightGridPoint(0,-1)});
                cp.Units[3].BodyOffsets.AddRange(new[]{new FightGridPoint(),new FightGridPoint(-1,0)});}
            var skill=catalog.CreateSkill(11930,0,10086,rank,level,5,
                catalog.CalculateSkillProps(11930,rank,level,5).Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
            skill.LimitedTimes=1;actor.Skills.Add(skill);var slot=catalog.CreateSkillSlot(skill,0);
            slot.AutoStartChecked=!opening;slot.LimitedTimes=1;actor.SkillState.Slots.Add(slot);
            var run=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored=FightBattleExecutor.Restore(saved,formula,new FightDamageSettlement(),catalog,items);
            var step=run.Step(cp.Sequence,cp.Counter+1).Event;restored.Step(saved.Sequence,saved.Counter+1);
            Check(step.SkillCasts.Count(c=>c.SkillEntityId==11930)==1,"opening or ordinary attempt did not cast once");
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(saved),"restored opening/ordinary cast diverged");
            var speedProps=catalog.CalculateSkillProps(24373,rank,level,5);
            long bonus=(long)(1000m*speedProps[2000]/10000m+speedProps[2010]);
            foreach(var unit in cp.Units)
            {
                bool eligible=unit.Id==1 || unit.Id==3 || unit.Id==4 && largeAlly;
                var speed=unit.Statuses.Where(s=>s.ClassId==24373).ToArray();
                var movement=unit.Statuses.Where(s=>s.ClassId==24374).ToArray();
                Check(speed.Length==(eligible?1:0) && movement.Length==(eligible?1:0),"original role/range or unique status identity changed: "+unit.Id);
                long original=unit.Id==1?1000:unit.Id==3 || unit.Id==4?200:100;
                Check(unit.Properties.Single(p=>p.Id==4).Value==original+(eligible?bonus:0),"speed snapshot or unique application differs: "+unit.Id
                    +" properties="+unit.Properties.Single(p=>p.Id==4).Value+" turn="+unit.Turn.Speed+" expected="+(original+(eligible?bonus:0))
                    +" resolved="+string.Join(";",speed.SelectMany(s=>s.ResolvedProperties).Select(p=>p.Id+":"+p.Value)));
                Check(unit.Properties.Single(p=>p.Id==formula.PropertyId("MoveDist")).Value==2+(eligible?2:0),"original movement addition changed: "+unit.Id);
                foreach(var status in speed.Concat(movement))
                    Check(status.CanDispersed && status.DurationRound==2 && status.RoundTarget==2 && status.RoundUpdateTiming==1
                        && status.CreatorId==actor.Id && status.SourceSkillRank==rank && status.SourceSkillLevel==level
                        && status.SourceSubRank==5 && status.ApplyingSkillItemId==10086,
                        "two-round beneficiary clock or cultivated source changed");
                if(eligible)Check(speed[0].ResolvedProperties.Single(p=>p.Id==4).Value==bonus,
                    "fixed and percentage speed terms did not share the original caster snapshot");
            }
            Check(cp.Units.All(u=>u.Hp==1000000),"a pure enhancement dealt damage or healing");
            string after=FightBattleCheckpointSerializer.Save(cp);
            Check(run.Step(cp.Sequence,cp.Counter).Replayed && after==FightBattleCheckpointSerializer.Save(cp),"replay repeated the enhancement");
            var expectedBeneficiaries=new HashSet<int>(largeAlly?new[]{1,3,4}:new[]{1,3});
            var requeuedAfterExpiry=new HashSet<int>();
            for(int i=0;i<300 && !cp.Finished;i++)
            {
                var beforeSpeeds=cp.Units.ToDictionary(u=>u.Id,u=>u.Properties.Single(p=>p.Id==4).Value);
                var expiredBefore=cp.Units.Where(u=>u.Statuses.All(s=>s.ClassId!=24373)).Select(u=>u.Id).ToArray();
                var next=run.Step(cp.Sequence,cp.Counter+1).Event;restored.Step(saved.Sequence,saved.Counter+1);
                Check(!next.SkillCasts.Any(c=>c.SkillEntityId==11930),"opening attempt or limited skill cast repeated");
                var acting=cp.Units.Single(u=>u.Id==next.ActorId);
                // These original statuses expire at TimingEnd, after the queue's
                // existing Finish operation has inserted the next interval.
                long effective=beforeSpeeds[acting.Id];
                long expectedInterval=Math.Max(1L,(long)Math.Floor(100000000d/Math.Sqrt(Math.Max(1,effective)
                    *(double)Math.Max(1,acting.Turn.RankSpeedScale))+.5d));
                Check(acting.Turn.IntervalTicks==expectedInterval,
                    "the next queued interval did not read the current final speed: "+acting.Turn.IntervalTicks+" / "+expectedInterval);
                Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(saved),"status expiry or speed timeline diverged after restore");
                if(expiredBefore.Contains(acting.Id))requeuedAfterExpiry.Add(acting.Id);
                if(expectedBeneficiaries.All(requeuedAfterExpiry.Contains))break;
            }
            Check(expectedBeneficiaries.All(requeuedAfterExpiry.Contains),"expired enhancements were not followed by the restored base-speed interval");
            Check(cp.Units.SelectMany(u=>u.Statuses).All(s=>s.ClassId!=24373 && s.ClassId!=24374),"two-round enhancements did not expire");
            Check(cp.Units.All(u=>u.Properties.Single(p=>p.Id==formula.PropertyId("MoveDist")).Value==2),"movement was not restored after expiry");
            cases++;
        }
        return "Swift Wind Dance passed: "+cases+" cultivated opening/ordinary, full bodies, original role scope, movement/speed snapshots, expiry and restore cases.";
    }
    static FightBattleUnitState Unit(int id,int side,int type,int x,int y,int speed,FightDamageFormula formula)
    {
        var u=new FightBattleUnitState{Id=id,Side=side,CharacterType=type,Profession=7,Level=50,SubRank=5,
            X=x,Y=y,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000,MoveMask=5};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=speed;
        foreach(var p in new Dictionary<string,int>{{"Attack",1000},{"Defence",10},{"MaxHp",1000000},
            {"Speed",speed},{"MoveDist",2},{"BaseEffectRate",10000},{"BaseEffectDodge",10000}})
            u.Properties.Add(new FightPropertyValue{Id=formula.PropertyId(p.Key),Value=p.Value});
        return u;
    }
}
