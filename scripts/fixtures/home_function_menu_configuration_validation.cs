using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Offline.HomeFunctionMenuUI;
using Zjcs.Offline.HomeUI;
using Rules = Zjcs.Official.HomeFunctionMenuUI.HomeMenuConfigurationRules;

public sealed partial class HomeFunctionMenuConfigurationRunner
{
    void CheckConfiguration()
    {
        var catalog=UnityEngine.Resources.Load<OfficialCurrentHomeRuleAssetCatalog>("Home/OfflineHomeFunctionMenuConfiguration");
        Require(catalog,"Selected menu configuration resource binding");
        var configuration=new OfflineHomeFunctionMenuConfiguration(catalog);
        Require(catalog.Rows("wiki").Count==4 && catalog.Rows("activity_recommend").Count==176
            && catalog.Rows("activity_entry").Count==468,"Selected complete table row counts");
        foreach(var row in catalog.Rows("wiki"))
        {
            string type=(string)row["Type"];
            Require(Convert.ToInt32(row["Region"])==7 && configuration.GetWikiInfo(type)==null
                && configuration.GetWikiUrl(type)==null,"Original selected CN parser excludes US Wiki records");
            Require(configuration.GetWikiTitleKey(type)=="wiki_title_"+type,"Original Wiki title key");
        }
        Require(configuration.DisplayServerTimeRegionList.SequenceEqual(new[]{7})
            && !configuration.DisplayServerTimeRegionList.Contains(0),"Original selected CN client has no server time row");
        foreach(var row in catalog.Rows("activity_recommend"))
        {
            var type=(ActivityEntryType)Convert.ToInt32(row["ActivityEntryType"]); int id=Convert.ToInt32(row["Id"]);
            var value=configuration.GetRecommend(type,id);
            Require(value!=null && value.ActivityEntryType==type && value.Id==id
                && value.Order==Convert.ToInt32(row["Order"]) && value.ShowInMap==(bool)row["ShowInMap"],"Original recommendation keys and fields");
        }
        foreach(var row in catalog.Rows("activity_entry"))
        {
            var type=(ActivityEntryType)Convert.ToInt32(row["ActivityType"]); int id=Convert.ToInt32(row["Id"]);
            Require(configuration.GetEntries(type)[id].EntryPrefabPath==(string)row["EntryPrefabPath"],"Original activity entry key and prefab path");
        }
        Require(configuration.GetRecommend((ActivityEntryType)int.MaxValue,0)==null
            && configuration.GetEntries((ActivityEntryType)int.MaxValue)==null
            && configuration.GetWikiInfo("missing")==null,"Original missing key branches");
        var first=new Rules.WikiInfo{Type="same",Region=Rules.RegionCodeEnum.CN,URL="local-first",LimitConditionId=1};
        var rules=new Rules(new[]{new Rules.WikiInfo{Type="same",Region=Rules.RegionCodeEnum.US,URL="excluded"},
            first,new Rules.WikiInfo{Type="same",Region=Rules.RegionCodeEnum.CN,URL="duplicate",LimitConditionId=2}},
            Array.Empty<Rules.ActivityRecommendInfo>(),Array.Empty<Rules.ActivityEntryInfo>());
        Require(ReferenceEquals(rules.GetWikiInfo("same"),first) && rules.GetURL("same")=="local-first",
            "Original CN filter retains first duplicate and object identity");
        Require(rules.GetURL("absent")==null && rules.GetEntries((ActivityEntryType)1)==null
            && rules.GetRecommend((ActivityEntryType)1,0)==null,"Original empty parsed dictionaries");
        UnityEngine.Debug.Log("HOME_FUNCTION_MENU_CONFIGURATION_PASSED: selected Wiki/CN and time region, all 176 recommendations/468 entry paths, original duplicate/missing branches. No activity records created or external URLs opened.");
    }
}
