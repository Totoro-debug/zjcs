using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattlePortableFlameMovementHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Moving fire ring: " + message); }

    static FightBattleUnitState Unit(int id, int side, int x, int y, bool moving)
    {
        var unit = new FightBattleUnitState { Id=id, Side=side, CharacterType=1,
            Profession=6, Level=50, SubRank=1, X=x, Y=y,
            Hp=1000000, MaxHp=1000000, LastMaxHp=1000000 };
        unit.Turn.Id=id; unit.Turn.Hp=unit.Hp; unit.Turn.Speed=moving?1000:10;
        unit.Properties.AddRange(new[] { new FightPropertyValue { Id=1, Value=id==1?1000:100 },
            new FightPropertyValue { Id=2, Value=10 }, new FightPropertyValue { Id=3, Value=unit.MaxHp },
            new FightPropertyValue { Id=4, Value=unit.Turn.Speed },
            new FightPropertyValue { Id=100, Value=moving?1:0 } });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach (bool entry in new[] {false,true})
        foreach (bool ally in new[] {false,true})
        foreach (bool cold in new[] {false,true})
        foreach (int timing in new[] {0,1,2})
        {
            int holderId=ally?3:1;
            var creator=Unit(1,1,ally?1:6,ally?1:4,!ally&&!entry);
            var holder=ally?Unit(3,1,6,4,!entry):creator;
            var enemy=Unit(2,2,6,6,entry);
            var mover=entry?enemy:holder;
            var cp=new FightBattleCheckpoint { StageId=1001, MaxRound=6, RandomSeed=91,
                Grid=new FightBattleGrid { Width=14, Height=14 },
                Units=ally?new List<FightBattleUnitState>{creator,holder,enemy}
                    :new List<FightBattleUnitState>{creator,enemy} };
            var ring=catalog.CreateStatus(24210,holderId,1,91,11200,10061,2,20,1,creator.Properties);
            if(timing>0) ring.MoveNearSkillTriggerDelayTime=.17f;
            if(timing==2) ring.MoveNearSkillTriggerImmediately=true;
            holder.Statuses.Add(ring);
            var skill=catalog.CreateSkill(10600,0,10008,2,20,1,
                catalog.CalculateSkillProps(10600,2,20,1)
                    .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList());
            skill.LimitedTimes=1; mover.Skills.Add(skill);
            mover.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            var wave=new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            if(cold) { cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                executor=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items); }
            FightBattleStepEvent step=null;
            for(int index=0;index<12;index++)
            {
                step=executor.Step(cp.Sequence,cp.Counter+1).Event;
                if(step.Movements.Any(value=>value.UnitId==mover.Id)) break;
            }
            var movement=step.Movements.Single(value=>value.UnitId==mover.Id);
            var start=movement.Path.First(); var end=movement.Path.Last();
            Check(start.X==6&&start.Y==(entry?6:4)&&end.X==6&&end.Y==5,
                "Original ordinary landing did not cross the eight-cell boundary");
            var casts=step.SkillCasts.Where(value=>value.SkillEntityId==10860).ToArray();
            var hits=step.DamageEvents.Where(value=>value.SkillEntityId==10860).ToArray();
            Check(casts.Length==1&&casts[0].ActorId==1&&casts[0].Slot<0
                &&casts[0].TargetIds.SequenceEqual(new[]{2})&&hits.Length==1,
                "Movement must invoke one original child from the creator, including a distant creator");
            var hit=hits.Single();
            float travel=0f;
            for(int index=1;index<movement.Path.Count;index++)
            {
                var left=movement.Path[index-1]; var right=movement.Path[index];
                float dx=right.X-left.X, dy=right.Y-left.Y;
                travel+=movement.MoveDuration>=0f?movement.MoveDuration
                    :(float)Math.Sqrt(dx*dx+dy*dy)/movement.MoveSpeed;
            }
            float expectedDelay=timing==2?0f:Math.Max(0f,movement.Delay)+travel+(timing==1?.17f:0f);
            Check(travel>0f&&Math.Abs(casts[0].Delay-expectedDelay)<.00001f&&hit.Delay>=casts[0].Delay,
                "Movement child started before travel ended or lost the authored/immediate delay: expected="
                    +expectedDelay+" actual="+casts[0].Delay);
            Check(hit.SourceId==1&&hit.TargetId==2&&hit.HitEntityId==10861&&hit.DamageEntityId==10862
                &&hit.Damage<0&&hit.BeforeHp-hit.AfterHp==-hit.Damage,
                "Movement child lost original damage identity or actual HP settlement");
            var status=cp.Units.Single(value=>value.Id==holderId).Statuses.Single(value=>value.ClassId==24210);
            Check(status.CreatorId==1&&status.ApplyingSkillItemId==10061&&status.SourceSkillRank==2
                &&status.SourceSkillLevel==20&&status.DurationRound==2&&!status.CanDispersed,
                "Movement changed the ring's original source, cultivation or lifetime");
            var saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(cp.Sequence,cp.Counter).Replayed&&FightBattleCheckpointSerializer.Save(cp)==saved,
                "Movement receipt replay duplicated damage or changed the ring");
            var restored=FightBattleCheckpointSerializer.Load(saved);
            var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            Check(resumed.Step(restored.Sequence,restored.Counter).Replayed
                &&FightBattleCheckpointSerializer.Save(restored)==saved,
                "Cold movement receipt replay changed damage or state");
            Console.WriteLine("Moving fire ring case: entry="+entry+" ally="+ally+" cold="+cold+" timing="+timing
                +" childDelay="+casts[0].Delay+" movementDelay="+movement.Delay);
            cases++;
        }
        return "Portable flame movement passed: "+cases+" ordinary landing, creator, entry, cold and receipt cases.";
    }
}
