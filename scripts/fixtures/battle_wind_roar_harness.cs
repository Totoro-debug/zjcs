using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWindRoarHarness
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException("Wind roar: "+message);}
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(bool large in new[]{false,true})
        foreach(int rank in new[]{3,6,7})
        foreach(int level in new[]{1,2,20})
        foreach(int seed in new[]{104729,209458,314187,418916})
        {
            var cp=new FightBattleCheckpoint {StageId=1,MaxRound=4,RandomSeed=seed,
                Grid=new FightBattleGrid {Width=24,Height=24,DefaultGroundType=2}};
            var actor=Unit(1,1,10,formula);var first=Unit(2,2,12,formula);
            var second=Unit(3,2,13,formula);var outside=Unit(4,2,20,formula);
            if(large)second.BodyOffsets.AddRange(new[]{new FightGridPoint(0,0),new FightGridPoint(1,0),
                new FightGridPoint(0,1),new FightGridPoint(1,1)});
            cp.Units.AddRange(new[]{actor,first,second,outside});
            var wave=new FightBattleWaveState();wave.UnitIds.AddRange(new[]{2,3,4});cp.Waves.Add(wave);
            var props=catalog.CalculateSkillProps(11130,rank,level,8);
            var skill=catalog.CreateSkill(11130,0,10057,rank,level,8,
                props.Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList());
            actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var step=runner.Step(cp.Sequence,cp.Counter+1).Event;
            var cast=step.SkillCasts.Single(c=>c.ActorId==1 && c.SkillEntityId==11130);
            float[] delays={.77f,.83f,.97f,1.21f,1.43f};
            Check(cast.HitSelections.Count==5 && cast.HitSelections.Select(h=>h.Delay).Zip(delays,(a,b)=>Math.Abs(a-b)<.0001f).All(v=>v),
                "the five hits lost their authored order or timing");
            var damage=step.DamageEvents.Where(h=>h.SourceId==1).ToArray();
            long expected=-(long)((1000m*props[200]/10000m+props[190])*1000m/1010m);
            var center=cast.HitSelections[0].EffectCenters.Single();
            var bodyCounts=new[]{first,second,outside}.ToDictionary(target=>target.Id,target=>
                new[]{new FightGridPoint(0,0)}.Concat(target.BodyOffsets).Distinct().Count(offset=>
                    Math.Abs(target.X+offset.X-center.X)+Math.Abs(target.Y+offset.Y-center.Y)<=2));
            var expectedTargets=bodyCounts.Where(pair=>pair.Value>0).Select(pair=>pair.Key).ToArray();
            Check((large || expectedTargets.Length==2) && expectedTargets.Contains(3)
                && !expectedTargets.Contains(4) && damage.Length==5*bodyCounts.Values.Sum()
                && damage.All(h=>expectedTargets.Contains(h.TargetId))
                && damage.GroupBy(h=>h.TargetId).All(g=>g.Count()==5*bodyCounts[g.Key]),
                "the original radius lost its body-cell occurrences or hit an outside unit: large="+large
                +" center="+center.X+","+center.Y+" expected="+string.Join(",",expectedTargets)
                +" actual="+string.Join(",",damage.GroupBy(h=>h.TargetId).Select(g=>g.Key+":"+g.Count())));
            Check(damage.All(h=>h.SkillItemId==10057 && h.SkillEntityId==11130 && h.HitEntityId==11131
                && h.DamageEntityId==11132 && h.DamageType==1 && h.ElementType==100 && h.Damage==expected
                && !h.IsCure && !h.TrueDamage && !h.Critical && !h.Block),
                "the cultivated wind hits lost their original fixed/percentage damage or element: "
                +damage[0].Damage+"/"+expected+" element="+damage[0].ElementType+" type="+damage[0].DamageType
                +" critical="+damage[0].Critical+" block="+damage[0].Block);
            Check(cp.StatusSeconds.PendingHits.Count==damage.Length,"an original impact lost its scheduled airborne transition");
            runner.AdvanceStatusSeconds(2d);
            foreach(var target in new[]{first,second})
            {
                var launch=target.Statuses.Single(s=>s.ClassId==24269);
                Check(launch.CreatorId==1 && launch.ApplyingSkillItemId==10057 && launch.RootSkillId==11130
                    && launch.SourceSkillRank==rank && launch.SourceSkillLevel==level && launch.SourceSubRank==8
                    && launch.Components.Seconds.Started && launch.Components.Seconds.RemainingSeconds>0d,
                    "the launch phase lost its timer, source, cultivation or hit resets");
            }
            var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored=FightBattleExecutor.Restore(saved,formula,new FightDamageSettlement(),catalog,items);
            Check(restored.Step(saved.Sequence,saved.Counter).Replayed,"the restored cast was not idempotent");
            double next=runner.NextStatusSecondsDelay;
            runner.AdvanceStatusSeconds(next);restored.AdvanceStatusSeconds(next);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(saved),
                "restoration changed the launch expiry or recovery successor");
            foreach(var target in new[]{first,second})
            {
                Check(target.Statuses.All(s=>s.ClassId!=24269),"the launch phase outlived its reset deadline");
                var recovery=target.Statuses.Single(s=>s.ClassId==24270);
                Check(recovery.CreatorId==1 && recovery.ApplyingSkillItemId==10057
                    && recovery.SourceSkillRank==rank && recovery.SourceSkillLevel==level,
                    "the recovery successor lost its original source or cultivation");
            }
            next=runner.NextStatusSecondsDelay;
            runner.AdvanceStatusSeconds(next);restored.AdvanceStatusSeconds(next);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(saved)
                && cp.Units.All(u=>u.Statuses.All(s=>s.ClassId!=24269 && s.ClassId!=24270)),
                "the recovery phase failed to expire identically after restoration");
            cases++;
        }
        return "Wind roar original five wind hits, body-aware area, cultivated damage and restored airborne phases passed: "+cases;
    }

    static FightBattleUnitState Unit(int id,int side,int x,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState {Id=id,Side=side,CharacterType=1,Profession=12,Level=70,
            SubRank=8,X=x,Y=10,Hp=1000000000,MaxHp=1000000000,LastMaxHp=1000000000};
        unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=id==1?1000:900;
        foreach(var p in new Dictionary<string,int>{{"Attack",1000},{"Defence",10},{"MaxHp",1000000000},
            {"Speed",unit.Turn.Speed},{"BaseCritRatePercentValue",10000},{"BaseCritAvoidPercentValue",10000},
            {"CritAvoidPercent",10000},{"BaseBlockPercentValue",10000},{"BaseBlockAvoidPercentValue",10000},
            {"BaseEffectRate",10000},{"BaseEffectDodge",10000},{"BaseElementMaster",10000},
            {"BaseElementResistance",10000},{"BaseElementAdd",10000},{"BaseElementReduce",10000}})
            unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId(p.Key),Value=p.Value});
        unit.BaseProperties=unit.Properties.Select(p=>new FightPropertyValue {Id=p.Id,Value=p.Value}).ToList();
        return unit;
    }
}
