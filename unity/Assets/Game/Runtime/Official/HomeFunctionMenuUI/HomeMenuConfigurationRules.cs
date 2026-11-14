using System;
using System.Collections.Generic;
using System.Linq;
namespace Zjcs.Official.HomeFunctionMenuUI
{
// Original parser methods; binary input is materialized by the local asset owner.
public sealed class HomeMenuConfigurationRules
{

public enum RegionCodeEnum
{
    CN,
    TW,
    HK,
    MO,
    JP,
    KR,
    HKMOT,
    US,
    VN,
    All
}

public enum ActivityCategoryType
{
    Common,
    Pay
}

public sealed class WikiInfo
{
public string Type;
public RegionCodeEnum Region;
public int LimitConditionId;
public string URL;
}

public sealed class ActivityRecommendInfo
{
public ActivityEntryType ActivityEntryType;
public int Id;
public int Order;
public bool ShowInMap;
}

public sealed class ActivityEntryInfo
{
public ActivityEntryType ActivityType;
public int Id;
public int Order;
public ActivityCategoryType ActivityCategoryType;
public string EntryPrefabPath;
public bool IsShow;
}

private Dictionary<string,WikiInfo> mWikiInfoDict;
private readonly Dictionary<ActivityEntryType,Dictionary<int,ActivityRecommendInfo>> recommendations;
private readonly Dictionary<ActivityEntryType,Dictionary<int,ActivityEntryInfo>> entries;
public HomeMenuConfigurationRules(WikiInfo[] wiki,ActivityRecommendInfo[] recommendations,ActivityEntryInfo[] entries)
{
    ClientPostParse(wiki);
    this.recommendations=IndexRecommendations(recommendations.ToDictionary(row=>(row.ActivityEntryType,row.Id),row=>row));
    this.entries=IndexEntries(entries.ToDictionary(row=>(row.ActivityType,row.Id),row=>row));
}
public ActivityRecommendInfo GetRecommend(ActivityEntryType type,int id)
    => recommendations.TryGetValue(type,out var group)&&group.TryGetValue(id,out var row)?row:null;
public IReadOnlyDictionary<int,ActivityEntryInfo> GetEntries(ActivityEntryType type)
    => entries.TryGetValue(type,out var group)?group:null;


    private void ClientPostParse(WikiInfo[] configs)
    {
        mWikiInfoDict = new Dictionary<string, WikiInfo>();
        RegionCodeEnum regionCodeEnum = RegionCodeEnum.CN;
        foreach (WikiInfo wikiInfo in configs)
        {
            if (wikiInfo.Region != regionCodeEnum)
            {
                continue;
            }
            if (mWikiInfoDict.ContainsKey(wikiInfo.Type))
            {
            }
            else
            {
                mWikiInfoDict[wikiInfo.Type] = wikiInfo;
            }
        }
    }

    public string GetTitleKey(string type)
    {
        return "wiki_title_" + type;
    }

    public string GetURL(string type)
    {
        return GetWikiInfo(type)?.URL;
    }

    public WikiInfo GetWikiInfo(string type)
    {
        if (mWikiInfoDict.TryGetValue(type, out var value))
        {
            return value;
        }
        return null;
    }

    private static Dictionary<ActivityEntryType, Dictionary<int, ActivityRecommendInfo>> IndexRecommendations(IReadOnlyDictionary<(ActivityEntryType, int), ActivityRecommendInfo> dataMapUnion)
    {

        Dictionary<ActivityEntryType, Dictionary<int, ActivityRecommendInfo>> dictionary = new Dictionary<ActivityEntryType, Dictionary<int, ActivityRecommendInfo>>();
        foreach (KeyValuePair<(ActivityEntryType, int), ActivityRecommendInfo> item in dataMapUnion)
        {
            (ActivityEntryType, int) key = item.Key;
            if (!dictionary.TryGetValue(key.Item1, out var value))
            {
                value = new Dictionary<int, ActivityRecommendInfo>();
                dictionary.Add(key.Item1, value);
            }
            value.Add(key.Item2, item.Value);
        }
        return dictionary;
    }

    private static Dictionary<ActivityEntryType, Dictionary<int, ActivityEntryInfo>> IndexEntries(IReadOnlyDictionary<(ActivityEntryType, int), ActivityEntryInfo> dataMapUnion)
    {

        Dictionary<ActivityEntryType, Dictionary<int, ActivityEntryInfo>> dictionary = new Dictionary<ActivityEntryType, Dictionary<int, ActivityEntryInfo>>();
        foreach (KeyValuePair<(ActivityEntryType, int), ActivityEntryInfo> item in dataMapUnion)
        {
            (ActivityEntryType, int) key = item.Key;
            if (!dictionary.TryGetValue(key.Item1, out var value))
            {
                value = new Dictionary<int, ActivityEntryInfo>();
                dictionary.Add(key.Item1, value);
            }
            value.Add(key.Item2, item.Value);
        }
        return dictionary;
    }

}
}
