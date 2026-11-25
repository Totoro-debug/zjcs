using System;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.HomeAirship;
using Zjcs.Offline.Home;

public static class AirshipConfigProbe
{
    public static void Run()
    {
        try
        {
            var config = OfflineHomeAirshipConfiguration.LoadDefault();
            var condition = config.TradeCondition(83, 0, 0);
            if (condition == null || condition._type != "HasItemCondition" || condition.ClassId != 4020 ||
                condition.Count != 1 || condition.Op != ">=") throw new Exception("Original trade condition differs");
            var rules = OfficialHomeRules.LoadDefault();
            var empty = new HomeConditionContext { ItemCount = _ => 0 };
            var owned = new HomeConditionContext { ItemCount = id => id == 4020 ? 1 : 0 };
            if (rules.IsMet(condition, empty) || !rules.IsMet(condition, owned))
                throw new Exception("Original trade item condition did not evaluate correctly");
            if (config.TradeCondition(83, 1, 0) != null || config.TradeCondition(82, 0, 0) != null)
                throw new Exception("Unknown function condition matched");
            var activity = config.MatchActivity(18, 100);
            if (activity == null || activity.Order != 1 || activity.EntryType != 18 || activity.ActivityId != 100 ||
                config.BannerPrefabPath(18, 100) != "Home/Dynamic/Banner/SeasonPreviewItem1")
                throw new Exception("Original activity match differs");
            if (config.MatchActivity(18, -1) != null || config.BannerPrefabPath(18, -1) != null)
                throw new Exception("Unknown activity unexpectedly matched");
            Debug.Log("AIRSHIP_CONFIG_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
