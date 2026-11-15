using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattlePortableFlameHarness
{
    static void Check(bool value,string message)
    { if(!value)throw new InvalidOperationException("Portable flame: "+message); }
    static FightBattleUnitState Unit(int id,int side,int x,int y)
    {
        var unit=new FightBattleUnitState{Id=id,Side=side,CharacterType=1,Profession=6,Level=50,SubRank=1,
            X=x,Y=y,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=id==1?1000:10;
        unit.Properties.AddRange(new[]{new FightPropertyValue{Id=1,Value=id==1?1000:100},
            new FightPropertyValue{Id=2,Value=10},new FightPropertyValue{Id=3,Value=unit.MaxHp},
            new FightPropertyValue{Id=4,Value=unit.Turn.Speed}});
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(int kind in new[]{FightStatusTriggerRuntime.MoveNearEventStart,FightStatusTriggerRuntime.MoveNearEventRound,
            FightStatusTriggerRuntime.MoveNearEventMoved,FightStatusTriggerRuntime.MoveNearEventEnterRange})
        foreach(bool ally in new[]{false,true})foreach(bool cold in new[]{false,true})
        {
            var creator=Unit(1,1,5,5);var holder=ally?Unit(3,1,6,5):creator;
            var enemy=Unit(2,2,holder.X+1,holder.Y);
            var status=catalog.CreateStatus(24210,holder.Id,creator.Id,91,11200,10061,2,20,1,creator.Properties);
            holder.Statuses.Add(status);
            var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=6,RandomSeed=91,
                Grid=new FightBattleGrid{Width=14,Height=14},Units=ally?new List<FightBattleUnitState>{creator,holder,enemy}
                :new List<FightBattleUnitState>{creator,enemy}};
            if(cold){cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                creator=cp.Units.Single(u=>u.Id==1);holder=cp.Units.Single(u=>u.Id==(ally?3:1));
                enemy=cp.Units.Single(u=>u.Id==2);status=holder.Statuses.Single();}
            var moved=new FightBattleMovementEvent{UnitId=kind==FightStatusTriggerRuntime.MoveNearEventMoved?holder.Id:enemy.Id};
            moved.Path.Add(new FightGridPoint(enemy.X+2,enemy.Y));moved.Path.Add(new FightGridPoint(enemy.X,enemy.Y));
            var invocations=FightStatusTriggerRuntime.CollectMoveNearSkills(holder,cp.Units,kind,moved,new FightRandomStream(91),
                kind==FightStatusTriggerRuntime.MoveNearEventStart?status:null);
            var invocation=invocations.Single();
            Check(invocation.SourceUnitId==creator.Id&&invocation.TargetUnitId==enemy.Id
                &&invocation.TargetPositionUnitId==enemy.Id,"Original Source=Creator was remapped to the nearby enemy: "+kind+" ally="+ally);
            Check(invocation.Trigger.EntityId==10860&&invocation.Trigger.SourceSkillRank==2
                &&invocation.Trigger.SourceSkillLevel==20&&invocation.Trigger.SourceSubRank==1,
                "Original child or cultivation differs");
            cases++;
        }
        foreach(bool cold in new[]{false,true})
        {
            var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=6,RandomSeed=91,
                Grid=new FightBattleGrid{Width=14,Height=14}};
            var creator=Unit(1,1,6,6);var holder=Unit(3,1,6,5);var enemy=Unit(2,2,6,4);
            cp.Units.AddRange(new[]{creator,holder,enemy});
            var skill=catalog.CreateSkill(11200,0,10061,2,20,1,catalog.CalculateSkillProps(11200,2,20,1)
                .Select(value=>new FightPropertyValue{Id=value.Key,Value=value.Value}).ToList());
            skill.LimitedTimes=1;creator.Skills.Add(skill);creator.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            if(cold){cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                executor=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);}
            var step=executor.Step(0,1).Event;
            var damage=step.DamageEvents.Single(value=>value.SkillEntityId==10860);
            Check(damage.SourceId==1&&damage.TargetId==2&&damage.Damage<0
                &&damage.BeforeHp>damage.AfterHp&&damage.HitEntityId==10861&&damage.DamageEntityId==10862,
                "An ally-held ring must settle original child damage from its distant creator");
            Check(step.SkillCasts.Single(value=>value.SkillEntityId==10860).ActorId==1
                &&cp.Units.Single(value=>value.Id==3).Hp==holder.Hp,
                "Ally proximity cast changed its caster or damaged the friendly holder");
            cases++;
        }
        foreach(int count in new[]{0,1,2,3,4,6})foreach(int seed in new[]{13,91,117})
        {
            var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=6,RandomSeed=seed,Grid=new FightBattleGrid{Width=14,Height=14}};
            var actor=Unit(1,1,6,6);cp.Units.Add(actor);
            var positions=new[]{new[]{6,5},new[]{5,6},new[]{7,6},new[]{6,7},new[]{5,5},new[]{7,7}};
            for(int i=0;i<count;i++)cp.Units.Add(Unit(3+i,1,positions[i][0],positions[i][1]));
            cp.Units.Add(Unit(20,1,1,1));
            var enemy=Unit(2,2,11,11);cp.Units.Add(enemy);
            var skill=catalog.CreateSkill(11200,0,10061,2,20,1,catalog.CalculateSkillProps(11200,2,20,1)
                .Select(value=>new FightPropertyValue{Id=value.Key,Value=value.Value}).ToList());
            skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(0,1).Event;resume.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"Cold cast differs");
            var holders=cp.Units.Where(u=>u.Statuses.Any(s=>s.ClassId==24210)).ToArray();
            Check(holders.Length==1+Math.Min(3,count)&&holders.Any(u=>u.Id==1)&&holders.All(u=>u.Side==1&&u.Id!=20),
                "Original self plus up-to-three distinct friendly recipients differs: "+count
                    +" actual="+string.Join(",",holders.Select(u=>u.Id))+" seed="+seed);
            foreach(var holder in holders)
            {
                var status=holder.Statuses.Single(s=>s.ClassId==24210);
                Check(status.CreatorId==1&&status.ApplyingSkillItemId==10061&&status.SourceSkillRank==2
                    &&status.SourceSkillLevel==20&&!status.CanDispersed&&status.DurationRound==2,
                    "Recipient lost original duration, cultivation or creator");
                var start=step.StatusViewEvents.Single(view=>view.Kind==FightBattleStatusViewEventKind.Start
                    &&view.OwnerId==holder.Id&&view.Status.ClassId==24210);
                Check(Math.Abs(start.Delay-(holder.Id==1?.37f:.59f))<.00001f,
                    "Original self/random child scope delays differ: "+holder.Id+" delay="+start.Delay);
            }
            Check(actor.SkillState.Slots[0].ReleasedTimes==1&&step.SkillCasts.Count==1,"One buff consumed more than one cast");
            var saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0,1).Replayed&&FightBattleCheckpointSerializer.Save(cp)==saved,"Replayed receipt changed buff");
            var expiry=holders.ToDictionary(u=>u.Id,u=>u.Statuses.Single(s=>s.ClassId==24210).StartRound+2);
            var ended=new HashSet<int>();
            for(int i=0;i<96&&ended.Count<holders.Length;i++)
            {
                var next=executor.Step(cp.Sequence,cp.Counter+1).Event;
                resume.Step(restored.Sequence,restored.Counter+1);
                Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),
                    "Cold continuation differs during recipient lifetime");
                foreach(var holder in holders)
                    Check(holder.Statuses.Any(s=>s.ClassId==24210)==(holder.SkillState.Round<expiry[holder.Id]),
                        "Fire ring expiry must follow its holder's two personal rounds: "+holder.Id);
                foreach(var view in next.StatusViewEvents.Where(value=>value.Kind==FightBattleStatusViewEventKind.End
                    &&value.Status.ClassId==24210))
                    Check(next.ActorId==view.OwnerId&&next.ActorRound==expiry[view.OwnerId]&&ended.Add(view.OwnerId),
                        "Ring ended on another unit's clock or emitted duplicate end events");
                restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            }
            Check(ended.Count==holders.Length,"Not all original two-round rings naturally expired");
            cases++;
        }
        return "Portable flame passed: "+cases+" original recipient, creator, proximity event and cold/replay cases.";
    }
}
