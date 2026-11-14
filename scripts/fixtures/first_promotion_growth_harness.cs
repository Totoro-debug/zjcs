using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Zjcs.Combat.Official;

public static class FirstPromotionGrowthHarness
{
    static void Equal(IReadOnlyDictionary<int,long> actual,IReadOnlyDictionary<int,long> expected,string message)
    {
        if(actual.Count!=expected.Count||expected.Any(p=>!actual.TryGetValue(p.Key,out long v)||v!=p.Value))
            throw new InvalidOperationException(message+" expected="+string.Join(",",expected.OrderBy(p=>p.Key))
                +" actual="+string.Join(",",actual.OrderBy(p=>p.Key)));
    }

    static int[] Levels(JsonElement root,int table,bool first)
    {
        if(table==0)return new[]{1,20};
        var rows=root.GetProperty("props").GetProperty(table.ToString()).GetProperty("rows");
        int[] levels=rows.EnumerateObject().Select(p=>int.Parse(p.Name)).ToArray();
        int max=levels.Max();
        return first?levels.Concat(new[]{max+1}).Distinct().OrderBy(v=>v).ToArray()
            :new[]{levels.Min(),1,20,max,max+1}.Distinct().OrderBy(v=>v).ToArray();
    }

    static IEnumerable<(int subRank,int table)> Mappings(JsonElement root,int group)
    {
        var result=root.GetProperty("groups").EnumerateArray().Where(r=>r.GetProperty("GroupId").GetInt32()==group)
            .Select(r=>(r.GetProperty("SubRank").GetInt32(),r.GetProperty("LevelPropId").GetInt32())).ToArray();
        return result.Length>0?result:Enumerable.Range(1,40).Select(s=>(s,0));
    }

    public static string Run(string input,FightOfficialCombatCatalog catalog)
    {
        SourceGrowthReference.Reference.Load(input);
        var root=JsonDocument.Parse(input).RootElement;
        int[] ranks=root.GetProperty("ranks").EnumerateArray().Select(r=>r.GetInt32()).ToArray();
        if(ranks.Length!=34||ranks.First()!=1||ranks.Last()!=34)throw new Exception("Review official rank scope");
        int values=0,passives=0,missing=0,independent=0;
        var entities=new HashSet<int>();
        foreach(var row in root.GetProperty("entities").EnumerateArray())
        {
            int entity=row.GetProperty("EntityId").GetInt32(),group=row.GetProperty("GroupLevelPropId").GetInt32();
            entities.Add(entity);var seen=new HashSet<int>();
            foreach(var mapping in Mappings(root,group))
            {
                bool first=seen.Add(mapping.table);
                foreach(int rank in ranks)
                foreach(int level in Levels(root,mapping.table,first))
                {
                    var expected=SourceGrowthReference.Reference.Evaluate(entity,rank,level,mapping.subRank);
                    var actual=catalog.CalculateSkillProps(entity,rank,level,mapping.subRank);
                    Equal(actual,expected,"Entity "+entity+"/"+rank+"/"+level+"/"+mapping.subRank);values++;
                }
                var saved=catalog.CalculateSkillProps(entity,7,20,mapping.subRank).ToDictionary(p=>p.Key,p=>p.Value);
                var mutable=catalog.CalculateSkillProps(entity,7,20,mapping.subRank) as IDictionary<int,long>;
                if(mutable!=null&&mutable.Count>0)mutable[mutable.Keys.First()]=-999;
                Equal(catalog.CalculateSkillProps(entity,7,20,mapping.subRank),saved,"Entity result aliases source: "+entity);
                independent++;
            }
        }
        foreach(var id in root.GetProperty("configured_entity_ids").EnumerateArray().Select(r=>r.GetInt32()).Where(id=>!entities.Contains(id)))
        foreach(int rank in new[]{1,17,34})
        foreach(int subRank in new[]{1,8,20,40})
        {
            if(catalog.CalculateSkillProps(id,rank,20,subRank).Count!=0)throw new Exception("Unconfigured growth: "+id);
            missing++;
        }
        foreach(var row in root.GetProperty("skills").EnumerateArray())
        {
            int item=row.GetProperty("ClassId").GetInt32(),group=row.GetProperty("PassiveGroupLevelPropId").GetInt32();
            int rankId=row.GetProperty("PassiveRankPropId").GetInt32(),factorId=row.GetProperty("PassiveRankFactorId").GetInt32();
            var factors=row.GetProperty("PassivePropFactors").EnumerateObject().ToDictionary(p=>int.Parse(p.Name),p=>p.Value.GetInt64());
            var seen=new HashSet<int>();
            foreach(var mapping in Mappings(root,group))
            {
                bool first=seen.Add(mapping.table);
                // Empty contributions still exercise every profession/subrank,
                // with rank/level boundaries; populated factors use every row.
                foreach(int rank in factors.Count>0?ranks:new[]{1,17,34})
                foreach(int level in Levels(root,mapping.table,first&&factors.Count>0))
                {
                    var expected=SourceGrowthReference.Reference.EvaluatePassive(rankId,group,factorId,factors,rank,level,mapping.subRank);
                    Equal(catalog.CalculatePassiveSkillProps(rankId,group,factorId,factors,rank,level,mapping.subRank),expected,
                        "Passive "+item+"/"+rank+"/"+level+"/"+mapping.subRank);passives++;
                }
                var saved=catalog.CalculatePassiveSkillProps(rankId,group,factorId,factors,7,20,mapping.subRank).ToDictionary(p=>p.Key,p=>p.Value);
                var mutable=catalog.CalculatePassiveSkillProps(rankId,group,factorId,factors,7,20,mapping.subRank) as IDictionary<int,long>;
                if(mutable!=null&&mutable.Count>0)mutable[mutable.Keys.First()]=-999;
                Equal(catalog.CalculatePassiveSkillProps(rankId,group,factorId,factors,7,20,mapping.subRank),saved,"Passive result aliases source: "+item);
                independent++;
            }
        }
        return "First-promotion growth passed: "+values+" original entity comparisons; "+passives
            +" original passive comparisons; "+missing+" absent-row checks; "+independent
            +" independent-result checks. 104 items, 383 entities, 34 ranks, 40 subranks. Scheduling and presentation remain outside this check.";
    }
}
