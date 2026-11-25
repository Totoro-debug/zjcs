using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.HomeAirship;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.WorldUI;

public static class AirshipBannerTitleProbe
{
    public static void Run()
    {
        try
        {
            var root = new GameObject("OriginalBannerFixture");
            root.SetActive(false);
            var text = new GameObject("Title", typeof(RectTransform), typeof(TextMeshPro), typeof(WorldMultiLanguageText));
            text.transform.SetParent(root.transform, false);
            var language = text.GetComponent<WorldMultiLanguageText>();
            language.Localizer = key => key;
            var banner = root.AddComponent<AirshipActivityBannerItem>();
            typeof(AirshipActivityBannerItem).GetField("mTitleTxt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(banner, language);
            banner.Init(new AirshipActivityEntry { EntryType = 18, ActivityId = 100 });
            if (language.myKey != "activity_entry_LinkageActivityPreview_100_mainTitle" || language.Text != language.myKey)
                throw new Exception("Original banner title key differs");
            banner.Init(new AirshipActivityEntry { EntryType = 7, ActivityId = 0 });
            if (language.myKey != "activity_entry_TopUpRebate_0_mainTitle")
                throw new Exception("Original rebate title key differs");
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("AIRSHIP_BANNER_TITLE_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
