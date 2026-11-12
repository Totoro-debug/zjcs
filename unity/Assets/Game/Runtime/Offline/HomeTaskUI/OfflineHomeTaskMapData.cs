using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using TaskUI = Zjcs.Official.HomeTaskUI;
namespace Zjcs.Offline.HomeTaskUI
{
    public sealed class OfflineHomeTaskMapData : TaskUI.IHomeTaskCombatColors
    {
        readonly Dictionary<(int,int), Region> regions;
        readonly Dictionary<int,int> ranks;
        readonly int suppressUnlockRank;
        public TaskUI.CombatRatingConfig Value {get;}
        OfflineHomeTaskMapData(JObject data)
        {
            int index=(int)data["recommendCombatRatingIndex"];
            Value=new Colors((JObject)data["combatColors"]);
            regions=((JArray)data["regions"]).Cast<JObject>().ToDictionary(row=>((int)row["ClassId"],(int)row["Region"]),row=>new Region(row,index));
            ranks=((JObject)data["subRankToRank"]).Properties().ToDictionary(p=>int.Parse(p.Name),p=>(int)p.Value);
            suppressUnlockRank=(int)data["suppressUnlockRank"];
        }
        public static OfflineHomeTaskMapData LoadDefault()
        {
            var data=Resources.Load<TextAsset>("Home/home-task-map-data");
            var proof=Resources.Load<TextAsset>("Home/home-task-map-data-hash");
            if(!data||!proof)throw new InvalidOperationException("Original task map data is unbound.");
            return Load(data.text,proof.text);
        }
        public static OfflineHomeTaskMapData Load(string json,string proofJson)
        {
            var data=JObject.Parse(json);var proof=JObject.Parse(proofJson);
            string hash;
            using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","").ToLowerInvariant();
            if((string)data["manifest"]!="884_156692"||(string)proof["manifest"]!="884_156692"||(string)proof["output_sha256"]!=hash)
                throw new FormatException("Original task map payload differs from its selected source.");
            return new OfflineHomeTaskMapData(data);
        }
        public TaskUI.MapRegionInfo Get(int map,int region)=>regions[(map,region)];
        public int CalcRankSuppression(TaskUI.SubRank player,TaskUI.SubRank region)
            =>TaskUI.HomeTaskMapFormula.CalcRankSuppression((int)player,(int)region,suppressUnlockRank,ToRank);
        int ToRank(int value)
        {
            if(ranks.TryGetValue(value,out int rank))return rank;
            Debug.LogError("ToRank Unsupported SubRank:"+value);return 0;
        }
        sealed class Colors:TaskUI.CombatRatingConfig
        {
            readonly Dictionary<int,int> themes;
            readonly Dictionary<int,Color>[] colors;
            public Colors(JObject row)
            {
                themes=((JArray)row["mTypeToThemes"]["mSerializeBuffer"]).ToDictionary(x=>(int)x["Key"],x=>(int)x["Value"]);
                colors=((JArray)row["mColorTemplates"]).Select(x=>((JArray)x["LevelColors"]["mSerializeBuffer"]).ToDictionary(v=>(int)v["Key"],v=>new Color((float)v["Value"]["r"],(float)v["Value"]["g"],(float)v["Value"]["b"],(float)v["Value"]["a"]))).ToArray();
            }
            public override bool TryGetLevelColor(TaskUI.CombatRateingColorType type,int level,out Color color)
            {
                color=default;
                if(!themes.TryGetValue((int)type,out int theme)){Debug.LogError("CombatRatingConfig: 未找到 Type="+type+" 主题的档位颜色");return false;}
                if(theme<0||theme>=colors.Length){Debug.LogError("CombatRatingConfig: 未找到 index="+theme+" 主题的档位颜色");return false;}
                if(!colors[theme].TryGetValue(level,out color)){Debug.LogError("CombatRatingConfig: 未找到 Level="+level+" 的战力档位颜色");return false;}
                return true;
            }
        }
        sealed class Region:TaskUI.MapRegionInfo
        {
            readonly JObject row;readonly int index;readonly IReadOnlyList<long> values;
            public Region(JObject row,int index){this.row=row;this.index=index;values=row["CombatRatingLevelValues"].Type==JTokenType.Null?null:Array.AsReadOnly(row["CombatRatingLevelValues"].Values<long>().ToArray());}
            public override int LimitLevel=>(int)row["LimitLevel"];
            public override long CombatRating=>values!=null?values[index]:0L;
            public override IReadOnlyList<long> CombatRatingLevelValues=>values;
            public override TaskUI.SubRank SubRank=>(TaskUI.SubRank)(int)row["SubRank"];
            // Confirmed offline policy removes server-rank/calendar gates;
            // original fields remain in the selected payload for provenance.
            public override bool ExistLimit()=>LimitLevel>0;
            public override TaskUI.MapRegionLimitType IsLimitedByType(int level,object rank)
                =>level<LimitLevel?TaskUI.MapRegionLimitType.Level:TaskUI.MapRegionLimitType.None;
            public override bool IsLimited(int level,object rank)=>IsLimitedByType(level,rank)!=TaskUI.MapRegionLimitType.None;
        }
    }
}
