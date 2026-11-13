using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Zjcs.Combat.Official;

public static class BattleArcShieldGrowthHarness
{
    static void Check(bool value,string message)
    { if(!value) throw new InvalidOperationException("Arc growth: "+message); }
    static bool Equal(IReadOnlyDictionary<int,long> left,IReadOnlyDictionary<int,long> right)
        => left.Count==right.Count && left.All(p=>right.TryGetValue(p.Key,out long v)&&p.Value==v);
    static long Prop(FightBattleUnitState unit,int id)=>unit.Properties.Where(p=>p.Id==id).Sum(p=>p.Value);

    public static string Run(string input,FightOfficialCombatCatalog catalog,FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        SourceGrowthReference.Reference.Load(input);
        var root=JsonDocument.Parse(input).RootElement;
        var seen=new HashSet<int>();int values=0,lifecycles=0,aliases=0;
        foreach(var group in root.GetProperty("groups").EnumerateArray())
        {
            int subRank=group.GetProperty("SubRank").GetInt32(),table=group.GetProperty("LevelPropId").GetInt32();
            var rows=root.GetProperty("props").GetProperty(table.ToString()).GetProperty("rows");
            int max=rows.EnumerateObject().Max(p=>int.Parse(p.Name));
            bool first=seen.Add(table);
            int[] levels=first ? rows.EnumerateObject().Select(p=>int.Parse(p.Name)).Concat(new[]{max+1}).OrderBy(v=>v).ToArray()
                : new[]{0,1,20,max,max+1}.Distinct().ToArray();
            foreach(int rank in Enumerable.Range(1,34))
            foreach(int level in levels)
            foreach(int entity in new[]{10760,24030})
            {
                var expected=SourceGrowthReference.Reference.Evaluate(entity,rank,level,subRank);
                var actual=catalog.CalculateSkillProps(entity,rank,level,subRank);
                Check(Equal(actual,expected),entity+" rank="+rank+" level="+level+" subRank="+subRank);
                if(entity==10760) Check(expected.Count==1&&expected[150]==5,"original CD was changed by cultivation");
                else Check(expected.ContainsKey(62)&&expected.Keys.All(p=>p==62||p==110),"property buff keys changed");
                values++;if(!first)aliases++;
            }
            // Every skill rank at level 20 on each distinct table; additional
            // level boundaries at low/middle/high rank and aliases at rank 2.
            foreach(int rank in first ? Enumerable.Range(1,34) : new[]{2})
            foreach(int level in (first&&new[]{1,17,34}.Contains(rank)?new[]{1,20,max}:new[]{20}).Distinct())
            { Lifecycle(catalog,formula,items,rank,level,subRank);lifecycles++; }
        }
        Check(seen.Count==7,"expected seven original level tables");
        return "Arc growth passed: "+values+" original active/status comparisons ("+aliases
            +" alias/boundary comparisons), "+lifecycles+" actual cast/restore/expiry flows; seven level tables, 34 ranks, 40 subranks.";
    }

    static FightBattleUnitState Unit(int id,int subRank,int level)
    {
        var unit=new FightBattleUnitState {Id=id,Side=id,CharacterType=1,Profession=1,Level=level,SubRank=subRank,
            X=5+id,Y=5,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=id==1?1000:900;
        foreach(var pair in new Dictionary<int,long>{{1,1000},{2,10},{3,unit.MaxHp},{4,unit.Turn.Speed},{62,170},{110,7}})
            unit.Properties.Add(new FightPropertyValue{Id=pair.Key,Value=pair.Value});
        return unit;
    }

    static void Lifecycle(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items,
        int rank,int level,int subRank)
    {
        var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=20,RandomSeed=73,
            Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        var owner=Unit(1,subRank,level);cp.Units.Add(owner);cp.Units.Add(Unit(2,subRank,level));
        var skill=catalog.CreateSkill(10760,0,10024,rank,level,subRank,catalog.CalculateSkillProps(10760,rank,level,subRank)
            .Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
        skill.LimitedTimes=1;owner.Skills.Add(skill);owner.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,5));
        var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var expected=SourceGrowthReference.Reference.Evaluate(24030,rank,level,subRank);
        long fixedBlock=expected.TryGetValue(110,out long fixedValue)?fixedValue:0;
        var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
        int casts=0,ends=0,steps=0;
        do
        {
            var step=executor.Step(cp.Sequence,cp.Counter+1).Event;
            resumed.Step(restored.Sequence,restored.Counter+1);
            casts+=step.SkillCasts.Count(c=>c.SkillEntityId==10760&&c.SkillItemId==10024);
            ends+=step.StatusViewEvents.Count(e=>e.Status.ClassId==24030&&e.Kind==FightBattleStatusViewEventKind.End);
            bool active=owner.SkillState.Round<4;
            var status=owner.Statuses.SingleOrDefault(s=>s.ClassId==24030);
            Check((status!=null)==active,"cultivation changed personal-round lifetime");
            Check(Prop(owner,62)==170+(active?expected[62]:0)&&Prop(owner,110)==7+(active?fixedBlock:0)
                &&Prop(owner,formula.PropertyId("BlockValue"))==7+(active?fixedBlock:0),"cultivation projection or removal differs");
            if(active)
                Check(status.SourceSkillRank==rank&&status.SourceSkillLevel==level&&status.SourceSubRank==subRank
                    &&status.ApplyingSkillItemId==10024&&status.RootSkillId==10760&&status.ShieldKind==0
                    &&Equal(status.ResolvedProperties.ToDictionary(p=>p.Id,p=>p.Value),expected),"cultivation source/status properties lost");
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(FightBattleCheckpointSerializer.Save(restored)==saved,"cultivation continuation diverged");
            if(owner.SkillState.Round>=5)break;
            restored=FightBattleCheckpointSerializer.Load(saved);
            resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            Check(FightBattleCheckpointSerializer.Save(restored)==saved,"cultivation restoration changed the complete checkpoint");
            Check(++steps<20,"cultivation lifecycle did not end");
        }while(true);
        Check(casts==1&&ends==1,"original cast/expiry must each occur once");
    }
}
