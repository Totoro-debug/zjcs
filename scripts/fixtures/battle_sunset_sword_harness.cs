using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSunsetSwordHarness
{
    static void Check(bool value,string message)
    { if(!value)throw new InvalidOperationException("Sunset sword: "+message); }
    static FightBattleUnitState Unit(int id,int side,int x,int y,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState{Id=id,Side=side,CharacterType=1,Profession=6,Level=50,SubRank=5,
            X=x,Y=y,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=id==1?1000:10;
        foreach(var value in new Dictionary<int,long>{{1,1000},{2,10},{3,unit.MaxHp},{4,unit.Turn.Speed}})
            unit.Properties.Add(new FightPropertyValue{Id=value.Key,Value=value.Value});
        foreach(var name in new[]{"BaseBlockPercentValue","BaseBlockAvoidPercentValue","BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue","BaseEffectRate","BaseEffectDodge"})
            unit.Properties.Add(new FightPropertyValue{Id=formula.PropertyId(name),Value=10000});
        unit.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("CritAvoidPercent"),Value=10000});
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(var direction in new[]{new[]{0,1},new[]{0,-1},new[]{-1,0},new[]{1,0}})
        foreach(var growth in new[]{new[]{2,20,1},new[]{7,30,5}})
        foreach(string mode in new[]{"single","six-body-cells","two-targets"})
        {
            int dx=direction[0],dy=direction[1];
            var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=5,RandomSeed=91,
                Grid=new FightBattleGrid{Width=14,Height=14}};
            var actor=Unit(1,1,6,6,formula);var target=Unit(2,2,6+dx,6+dy,formula);
            if(mode=="six-body-cells")
                foreach(int x in new[]{-1,0,1})foreach(int y in new[]{1,2})
                    target.BodyOffsets.Add(new FightGridPoint(dx*y+dy*x-dx,dy*y-dx*x-dy));
            var skill=catalog.CreateSkill(11250,0,10053,growth[0],growth[1],growth[2],
                catalog.CalculateSkillProps(11250,growth[0],growth[1],growth[2])
                    .Select(value=>new FightPropertyValue{Id=value.Key,Value=value.Value}).ToList());
            var roots=skill.HitGraphRoots;
            Check(roots.Count==5&&roots.All(root=>root.EntityId==11251),"Original repeated roots changed");
            Check(skill.HitGraphEntities.Single(hit=>hit.EntityId==11251).ScopeEntries.Count==6,
                "Original six-cell scope changed");
            skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            cp.Units.AddRange(new[]{actor,target});
            if(mode=="two-targets")cp.Units.Add(Unit(3,2,6+dx*2,6+dy*2,formula));
            var wave=new FightBattleWaveState();wave.UnitIds.AddRange(cp.Units.Where(unit=>unit.Side==2).Select(unit=>unit.Id));cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(0,1).Event;resume.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"Cold continuation diverged");
            var cast=step.SkillCasts.Single();
            int each=mode=="single"?1:mode=="two-targets"?2:6;
            Check(cast.SkillItemId==10053&&cast.SkillEntityId==11250&&actor.SkillState.Slots[0].ReleasedTimes==1,
                "Equipment identity or one-cast consumption differs");
            Check(step.DamageEvents.Count==5*each&&cast.HitSelections.Count==5&&step.Movements.Count==0,
                "Five authored occurrences were collapsed or body cells were lost: "+mode+" count="+step.DamageEvents.Count);
            for(int i=0;i<5;i++)
            {
                var hits=step.DamageEvents.Skip(i*each).Take(each).ToArray();
                Check(hits.All(hit=>hit.HitEntityId==11251&&hit.DamageEntityId==10022&&hit.Damage<0
                    &&hit.BeforeHp-hit.AfterHp==-hit.Damage&&hit.ElementType==0&&!hit.TrueDamage
                    &&Math.Abs(hit.Delay-cast.Delay-roots[i].Delay)<.00001f),"Original hit identity, damage or delay differs");
                Check(cast.HitSelections[i].Targets.Count==each,"A repeated root lost its independently selected occurrences");
                if(mode=="six-body-cells")Check(hits.Select(hit=>hit.HitGridX+":"+hit.HitGridY).Distinct().Count()==6,
                    "Large target did not receive six distinct cell hits");
            }
            foreach(var victim in cp.Units.Where(unit=>unit.Side==2))
                Check(victim.Hp==1000000+step.DamageEvents.Where(hit=>hit.TargetId==victim.Id).Sum(hit=>hit.Damage),
                    "Per-target total HP loss differs");
            var saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0,1).Replayed&&FightBattleCheckpointSerializer.Save(cp)==saved,"Receipt replay repeated damage");
            cases++;
        }
        return "Sunset sword passed: "+cases+" original five-hit, six-cell, multi-target, growth and cold/replay flows.";
    }
}
