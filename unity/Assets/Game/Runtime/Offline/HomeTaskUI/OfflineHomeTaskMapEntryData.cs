using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomeTaskUI;

namespace Zjcs.Offline.HomeTaskUI
{
    // Selected original fields and list order; queries never expose mutable storage.
    public sealed class OfflineHomeTaskMapEntryData : IHomeTaskMapEntryData
    {
        readonly Dictionary<int,int> defaults=new Dictionary<int,int>();
        readonly Dictionary<(int,int),int[]> adjacent=new Dictionary<(int,int),int[]>();
        readonly Dictionary<(int,int),HomeTaskMapSingleEntry[]> entries=new Dictionary<(int,int),HomeTaskMapSingleEntry[]>();
        readonly Dictionary<(int,int),int> periodicityIds=new Dictionary<(int,int),int>();
        readonly Dictionary<int,int> periodicityMax=new Dictionary<int,int>();
        readonly HomeTaskMapPeriodicityRules rules;
        public IReadOnlyList<int> MapClassIds {get;}
        OfflineHomeTaskMapEntryData(JObject data)
        {
            var maps=data["maps"].Values<int>().ToArray();
            if(!maps.SequenceEqual(new[]{10,11,12}))throw new FormatException("Selected mainline entry maps differ.");
            MapClassIds=Array.AsReadOnly(maps);
            foreach(var row in data["worlds"])defaults.Add((int)row["ClassId"],(int)row["DefEntryId"]);
            if(!maps.SequenceEqual(defaults.Keys.OrderBy(id=>id)))throw new FormatException("Selected map entry defaults differ.");
            foreach(var row in data["regions"])adjacent.Add(((int)row["ClassId"],(int)row["Region"]),row["AdjacentRegions"].Type==JTokenType.Null?null:row["AdjacentRegions"].Values<int>().ToArray());
            foreach(var map in ((JObject)data["entries"]).Properties())
                foreach(var region in ((JObject)map.Value).Properties()){
                    int mapId=int.Parse(map.Name),regionId=int.Parse(region.Name);
                    var rows=region.Value.Select(row=>new HomeTaskMapSingleEntry{LoEntId=(int)row[0],EntryType=(HomeTaskMapEntryType)(int)row[1],Region=(int)row[2]}).ToArray();
                    if(!adjacent.ContainsKey((mapId,regionId))||rows.Any(row=>row.Region!=regionId||!Enum.IsDefined(typeof(HomeTaskMapEntryType),row.EntryType)))throw new FormatException("Original map entry identity differs.");
                    entries.Add((mapId,regionId),rows);
                }
            foreach(var map in ((JObject)data["periodicityIds"]).Properties())
                foreach(var entry in ((JObject)map.Value).Properties())periodicityIds.Add((int.Parse(map.Name),int.Parse(entry.Name)),(int)entry.Value);
            foreach(var row in data["periodicity"])periodicityMax.Add((int)row["PeriodicityGroupId"],(int)row["PeriodicityMaxCount"]);
            rules=new HomeTaskMapPeriodicityRules(this);
        }
        public static OfflineHomeTaskMapEntryData LoadDefault()
        {
            var data=Resources.Load<TextAsset>("Home/home-task-map-entry");var hash=Resources.Load<TextAsset>("Home/home-task-map-entry-hash");
            if(!data||!hash)throw new InvalidOperationException("Selected official map entry configuration is absent.");
            return Load(data.text,hash.text);
        }
        public static OfflineHomeTaskMapEntryData Load(string text,string hashText)
        {
            var hash=JObject.Parse(hashText);
            using(var algorithm=SHA256.Create()){
                string digest=BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();
                if((string)hash["manifest"]!="884_156692"||digest!=(string)hash["output_sha256"])throw new FormatException("Selected official map entry hash differs.");
            }
            var data=JObject.Parse(text);
            if((string)data["manifest"]!="884_156692")throw new FormatException("Selected official map entry version differs.");
            return new OfflineHomeTaskMapEntryData(data);
        }
        public HomeTaskMapPart GetMapPartInfo(int mapClassId)=>defaults.TryGetValue(mapClassId,out int id)?new HomeTaskMapPart{DefEntryId=id}:null;
        public HomeTaskMapEntryRegion Get(int mapClassId,int region)=>adjacent.TryGetValue((mapClassId,region),out var rows)?new HomeTaskMapEntryRegion{AdjacentRegions=rows==null?null:(int[])rows.Clone()}:null;
        public bool TryGetMapRegionEntryDatas(int mapClassId,int region,out List<HomeTaskMapSingleEntry> entryDatas)
        {
            entryDatas=null;
            if(!entries.TryGetValue((mapClassId,region),out var rows))return false;
            entryDatas=rows.Select(row=>new HomeTaskMapSingleEntry{LoEntId=row.LoEntId,Region=row.Region,EntryType=row.EntryType}).ToList();return true;
        }
        public int GetPeriodicityInteractionId(int mapClassId,int loEntId)=>periodicityIds.TryGetValue((mapClassId,loEntId),out int id)?id:-1;
        public HomeTaskMapPeriodicity GetPeriodicity(int groupId)=>periodicityMax.TryGetValue(groupId,out int count)?new HomeTaskMapPeriodicity{PeriodicityMaxCount=count}:null;
        public int CheckCanInteraction(int groupId,int count,bool isNegation=false)=>rules.CheckCanInteraction(groupId,count,isNegation);
    }
}
