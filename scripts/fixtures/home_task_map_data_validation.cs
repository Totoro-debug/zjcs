using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using Zjcs.Offline.HomeTaskUI;
using TaskUI=Zjcs.Official.HomeTaskUI;
public static class HomeTaskMapDataValidation
{
    public static void Run()
    {
        try {
            var model=OfflineHomeTaskMapData.LoadDefault();
            var data=JObject.Parse(Resources.Load<TextAsset>("Home/home-task-map-data").text);
            int count=0;
            foreach(var row in (JArray)data["regions"]){
                var region=model.Get((int)row["ClassId"],(int)row["Region"]);int limit=(int)row["LimitLevel"];
                Require(region.LimitLevel==limit&&region.ExistLimit()==(limit>0),"actual region level limit");
                Require(region.IsLimited(limit-1,null)&&!region.IsLimited(limit,null),"original exact level boundary");
                Require(region.IsLimitedByType(limit-1,null)==TaskUI.MapRegionLimitType.Level&&region.IsLimitedByType(limit,null)==TaskUI.MapRegionLimitType.None,"level reason without server/calendar gates");
                var values=row["CombatRatingLevelValues"];
                Require(region.CombatRating==(values.Type==JTokenType.Null?0:(long)values[(int)data["recommendCombatRatingIndex"]]),"original recommended index/null branch");
                count++;
            }
            Require(count==45,"all supported world-task targets");
            Require(TaskUI.HomeTaskMapFormula.GetCombatRatingLevel(0,null)==0&&TaskUI.HomeTaskMapFormula.GetCombatRatingLevel(0,Array.Empty<long>())==0,"original absent thresholds");
            var bounds=new long[]{10,20,30};
            foreach(var pair in new[]{(9L,3),(10L,2),(19L,2),(20L,1),(30L,0),(long.MaxValue,0)})
                Require(TaskUI.HomeTaskMapFormula.GetCombatRatingLevel(pair.Item1,bounds)==pair.Item2,"original threshold equality");
            var ranks=((JObject)data["subRankToRank"]).Properties().ToDictionary(p=>int.Parse(p.Name),p=>(int)p.Value);
            int suppress=(int)data["suppressUnlockRank"];
            foreach(var a in ranks)foreach(var b in ranks)
                Require(model.CalcRankSuppression((TaskUI.SubRank)a.Key,(TaskUI.SubRank)b.Key)==(a.Key<suppress?0:a.Value-b.Value),"all original rank-switch pairs");
            int colors=0;
            foreach(var theme in data["combatColors"]["mTypeToThemes"]["mSerializeBuffer"])
                foreach(var entry in data["combatColors"]["mColorTemplates"][(int)theme["Value"]]["LevelColors"]["mSerializeBuffer"]){
                    Require(model.Value.TryGetLevelColor((TaskUI.CombatRateingColorType)(int)theme["Key"],(int)entry["Key"],out var color),"original theme/level resolves");
                    var expected=entry["Value"];
                    Require(color==new Color((float)expected["r"],(float)expected["g"],(float)expected["b"],(float)expected["a"]),"original RGBA preserved");colors++;
                }
            Require(colors>0,"original selected combat colors are populated");
            bool rejected=false;
            try{OfflineHomeTaskMapData.Load("{}",Resources.Load<TextAsset>("Home/home-task-map-data-hash").text);}catch(FormatException){rejected=true;}
            Require(rejected,"changed selected payload rejected");
            Debug.Log("HOME_TASK_MAP_DATA_PASSED: 45 actual selected task regions, personal level boundaries, original recommendation/null/index branch, combat threshold equality and all 7396 original rank-switch pairs and every selected theme/level RGBA. No production HUD/map navigation claim.");
            EditorApplication.Exit(0);
        }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
