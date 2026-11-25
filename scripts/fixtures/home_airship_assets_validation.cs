using System;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.HomeAirship;

public static class AirshipAssetsProbe
{
    public static void Run()
    {
        try
        {
            var catalog = OfflineHomeAirshipAssets.LoadDefault();
            var first = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Home/Airship/Native/Image/Home/Dynamic/Airship/SmallPoster_1.asset");
            var second = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Home/Airship/Native/Image/Home/Dynamic/Airship/SmallPoster_2.asset");
            if (!first || !second || catalog.UnrepairedBackground != first || catalog.RepairedBackground != second)
                throw new Exception("The original airship sprite bindings differ");
            Debug.Log("AIRSHIP_ASSET_CATALOG_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
