using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattlePortableFlameUniqueHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Unique flame: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(bool cold in new[]{false,true})
        foreach(bool forbidden in new[]{false,true})
        foreach(int rank in new[]{1,3})
        {
            var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=6,RandomSeed=91,
                Grid=new FightBattleGrid{Width=14,Height=14}};
            foreach(int id in new[]{1,3,2})
            {
                var unit=new FightBattleUnitState{Id=id,Side=id==2?2:1,CharacterType=1,Profession=6,
                    Level=50,SubRank=1,X=id==1?5:6,Y=id==2?6:5,
                    Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
                unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=id==1?1000:id==3?900:10;
                unit.Properties.AddRange(new[]{new FightPropertyValue{Id=1,Value=id==3?1700:1000},
                    new FightPropertyValue{Id=2,Value=10},new FightPropertyValue{Id=3,Value=unit.MaxHp},
                    new FightPropertyValue{Id=4,Value=unit.Turn.Speed}});
                if(id!=2)
                {
                    int skillRank=id==3?rank:2, level=id==3?30:20;
                    var skill=catalog.CreateSkill(11200,0,10061,skillRank,level,1,
                        catalog.CalculateSkillProps(11200,skillRank,level,1)
                            .Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
                    skill.LimitedTimes=1;unit.Skills.Add(skill);unit.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
                }
                cp.Units.Add(unit);
            }
            var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            FightBattleStepEvent first=null;
            for(int i=0;i<8;i++)
            {
                first=executor.Step(cp.Sequence,cp.Counter+1).Event;
                if(first.SkillCasts.Any(c=>c.ActorId==1&&c.SkillEntityId==11200))break;
            }
            Check(first.SkillCasts.Any(c=>c.ActorId==1&&c.SkillEntityId==11200),"First caster did not release the original skill");
            var old=new Dictionary<int,int>();
            foreach(var unit in cp.Units.Where(u=>u.Side==1))
            {
                var ring=unit.Statuses.Single(s=>s.ClassId==24210);
                Check(ring.CreatorId==1,"First ring has the wrong creator");old.Add(unit.Id,ring.InstanceId);
                // Exercise replacement of an aged/disabled live status, not only a fresh entity.
                if(unit.Id==1)ring.CurDurationRound=1;
                if(forbidden)FightStatusLifecycle.SetForbidden(unit,ring,FightStatusLifecycle.ForbiddenStatusTrigger,true);
            }
            if(cold)
            {
                cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                executor=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
            }
            FightBattleStepEvent second=null;
            for(int i=0;i<8;i++)
            {
                second=executor.Step(cp.Sequence,cp.Counter+1).Event;
                if(second.SkillCasts.Any(c=>c.ActorId==3&&c.SkillEntityId==11200))break;
            }
            Check(second.SkillCasts.Any(c=>c.ActorId==3&&c.SkillEntityId==11200),"Second caster did not release the original skill");
            foreach(var unit in cp.Units.Where(u=>u.Side==1))
            {
                var ring=unit.Statuses.Single(s=>s.ClassId==24210);
                Check(ring.InstanceId!=old[unit.Id]&&ring.CreatorId==3&&ring.ApplyingSkillItemId==10061
                    &&ring.SourceSkillRank==rank&&ring.SourceSkillLevel==30&&ring.SourceSubRank==1
                    &&ring.ForbiddenType==0&&ring.DurationRound==2&&!ring.CanDispersed
                    &&FightStatusLifecycle.IconRemainingRound(ring,cp.Units)==2,
                    "New caster did not replace the entity, cultivation, forbidden state and two-round clock");
                var events=second.StatusViewEvents.Where(e=>e.OwnerId==unit.Id&&e.Status.ClassId==24210).ToArray();
                Check(events.Count(e=>e.Kind==FightBattleStatusViewEventKind.End&&e.Status.InstanceId==old[unit.Id])==1
                    &&events.Count(e=>e.Kind==FightBattleStatusViewEventKind.Start&&e.Status.InstanceId==ring.InstanceId)==1,
                    "Replacement must retire the old native view and start the new one once");
            }
            var casts=second.SkillCasts.Where(c=>c.SkillEntityId==10860&&c.ActorId==3).ToArray();
            var hits=second.DamageEvents.Where(d=>d.SkillEntityId==10860&&d.SourceId==3).ToArray();
            Check(casts.Length==2&&hits.Length==2&&hits.All(d=>d.TargetId==2&&d.Damage<0
                &&d.BeforeHp-d.AfterHp==-d.Damage&&d.HitEntityId==10861&&d.DamageEntityId==10862),
                "Each newly acquired holder must trigger one creator-owned original damage hit");
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(cp.Sequence,cp.Counter).Replayed&&FightBattleCheckpointSerializer.Save(cp)==saved,
                "Replaying a replacement duplicated acquisition damage");
            var restored=FightBattleCheckpointSerializer.Load(saved);
            var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            Check(resumed.Step(restored.Sequence,restored.Counter).Replayed
                &&FightBattleCheckpointSerializer.Save(restored)==saved,"Restoring a replacement replayed acquisition damage");
            cases++;
        }
        return "Portable flame uniqueness passed: "+cases+" two-caster, cultivation, disabled, lifetime and cold cases.";
    }
}
