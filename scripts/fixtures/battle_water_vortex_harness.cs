using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWaterVortexHarness
{
    static void Check(bool value,string message)
    {if(!value)throw new InvalidOperationException("Water vortex: "+message);}
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0,wet=0,dry=0,moved=0;
        foreach(bool large in new[]{false,true})
        foreach(int rank in new[]{3,6,7})
        foreach(int level in new[]{1,2,20})
        foreach(int chance in new[]{-1,0,1})
        foreach(int seed in new[]{104729,209458,314187,418916})
        {
            var cp=new FightBattleCheckpoint {StageId=1,MaxRound=8,RandomSeed=seed,
                Grid=new FightBattleGrid {Width=24,Height=24,DefaultGroundType=2}};
            var actor=Unit(1,1,10,10,formula);var first=Unit(2,2,12,10,formula);
            var second=Unit(3,2,14,10,formula);var outside=Unit(4,2,20,10,formula);
            var ally=Unit(5,1,12,9,formula);
            if(large)second.BodyOffsets.AddRange(new[]{new FightGridPoint(0,0),new FightGridPoint(1,0),
                new FightGridPoint(0,1),new FightGridPoint(1,1)});
            actor.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("EffectRateValue"),Value=chance*200000});
            actor.BaseProperties=actor.Properties.Select(p=>new FightPropertyValue {Id=p.Id,Value=p.Value}).ToList();
            cp.Units.AddRange(new[]{actor,first,second,outside,ally});
            var positions=cp.Units.ToDictionary(u=>u.Id,u=>new FightGridPoint(u.X,u.Y));
            var wave=new FightBattleWaveState();wave.UnitIds.AddRange(new[]{2,3,4});cp.Waves.Add(wave);
            var props=catalog.CalculateSkillProps(11340,rank,level,8);
            var skill=catalog.CreateSkill(11340,0,10062,rank,level,8,
                props.Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList());
            actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var copy=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored=FightBattleExecutor.Restore(copy,formula,new FightDamageSettlement(),catalog,items);
            var step=runner.Step(cp.Sequence,cp.Counter+1).Event;restored.Step(copy.Sequence,copy.Counter+1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(copy),
                "cold restoration changed water damage, wet rolls or inward movement");
            var cast=step.SkillCasts.Single(c=>c.ActorId==1&&c.SkillEntityId==11340);
            Check(cast.HitSelections.Count==1&&cast.HitSelections[0].HitEntityId==11341
                &&Math.Abs(cast.HitSelections[0].Delay-.6f)<.0001f,"the original impact timing changed");
            var center=cast.HitSelections[0].EffectCenters.Single();
            Check(Math.Abs(center.X-10)+Math.Abs(center.Y-10)<=4,"the chosen tile exceeded the original cast range");
            var counts=new[]{first,second,outside}.ToDictionary(u=>u.Id,u=>new[]{new FightGridPoint(0,0)}
                .Concat(u.BodyOffsets).Distinct().Count(p=>Math.Abs(positions[u.Id].X+p.X-center.X)
                    +Math.Abs(positions[u.Id].Y+p.Y-center.Y)<=2));
            var damage=step.DamageEvents.Where(h=>h.SourceId==1).ToArray();
            long expected=-(long)((1000m*props[200]/10000m+props[190])*1000m/1010m);
            Check(damage.Length==counts.Values.Sum()&&damage.Length>0
                &&damage.GroupBy(h=>h.TargetId).All(g=>counts.ContainsKey(g.Key)&&g.Count()==counts[g.Key])
                &&damage.All(h=>h.SkillItemId==10062&&h.SkillEntityId==11340&&h.HitEntityId==11341
                    &&h.DamageEntityId==11342&&h.ElementType==101&&h.DamageType==1&&h.Damage==expected
                    &&!h.TrueDamage&&!h.IsCure&&!h.Critical&&!h.Block),
                "cultivated water damage, body-cell area or friendly/outside exclusion changed");
            Check(outside.X==20&&outside.Y==10&&ally.X==12&&ally.Y==9,"an excluded unit moved");
            foreach(var target in new[]{first,second}.Where(u=>counts[u.Id]>0))
            {
                var status=target.Statuses.SingleOrDefault(s=>s.ClassId==24012);
                if(status==null)dry++;
                else {wet++;Check(status.DurationRound==2&&status.CreatorId==1&&status.ApplyingSkillItemId==10062
                    &&status.SourceSkillRank==rank&&status.SourceSkillLevel==level&&status.SourceSubRank==8
                    &&status.RootSkillId==11340,"wet lost its two rounds, source or cultivation");}
                if(chance<0)Check(status==null,"a resisted wet application succeeded");
                if(chance>0)Check(status!=null,"a guaranteed wet application failed");
                int before=Math.Abs(positions[target.Id].X-center.X)+Math.Abs(positions[target.Id].Y-center.Y);
                int after=Math.Abs(target.X-center.X)+Math.Abs(target.Y-center.Y);
                Check(after<=before,"inward movement increased the distance from the vortex center");
            }
            foreach(var move in step.Movements.Where(m=>m.MoveBehaviourId==11343))
            {moved++;Check(move.UnitId!=1&&move.UnitId!=4&&move.UnitId!=5&&move.Path.Count>1
                &&Math.Abs(move.MoveDuration-.4f)<.0001f,"the original inward move changed its target, path or duration");}
            string afterState=FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(cp.Sequence,cp.Counter).Replayed&&FightBattleCheckpointSerializer.Save(cp)==afterState,
                "receipt replay repeated damage, wet application or movement");
            cases++;
        }
        var source=Unit(1,1,10,10,formula);var wetTarget=Unit(2,2,12,10,formula);
        double baseCold=catalog.CalculateStatusApplicationRate(source,wetTarget,24215,.3,true,formula);
        double baseFrostbind=catalog.CalculateStatusApplicationRate(source,wetTarget,24149,.3,true,formula);
        double baseBlinding=catalog.CalculateStatusApplicationRate(source,wetTarget,24234,.3,true,formula);
        var wetStatus=catalog.CreateStatus(24012,2,1,1,11340,10062,7,2,8,source.Properties);
        wetTarget.Statuses.Add(wetStatus);
        long wetModifier=catalog.CalculateSkillProps(24012,7,2,8)[2000];
        Check(wetModifier==3000&&Math.Abs(catalog.CalculateStatusApplicationRate(source,wetTarget,24215,.3,true,formula)
            -baseCold*1.3)<1e-7,"wet did not retain its original 30 percent increase to cold application: "+wetModifier);
        Check(Math.Abs(catalog.CalculateStatusApplicationRate(source,wetTarget,24149,.3,true,formula)-baseFrostbind*1.3)<1e-7
            &&Math.Abs(catalog.CalculateStatusApplicationRate(source,wetTarget,24234,.3,true,formula)-baseBlinding)<1e-7,
            "wet changed its original frostbind and unrelated-status probability filters");
        Check(Math.Abs(catalog.CalculateStatusApplicationRate(source,wetTarget,24215,.3,false,formula)-.3)<1e-7,
            "wet changed a probability explicitly independent of target properties");
        Check(wet>0&&dry>0&&moved>0,"the original wet and inward movement branches were not exercised");
        return "Water vortex cultivated water damage, body-cell area, wet chance, inward movement and restore passed: "+cases;
    }
    static FightBattleUnitState Unit(int id,int side,int x,int y,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState {Id=id,Side=side,CharacterType=1,Profession=12,Level=70,
            SubRank=8,X=x,Y=y,Hp=1000000000,MaxHp=1000000000,LastMaxHp=1000000000};
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
