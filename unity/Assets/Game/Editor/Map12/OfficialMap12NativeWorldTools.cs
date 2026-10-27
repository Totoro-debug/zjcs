using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TMPro;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Presentation.World.NativeMap12;

namespace Zjcs.Editor.Map12
{
    public static class OfficialMap12NativeWorldTools
    {
        public static void Restore()
        {
            OfficialMap12EnvironmentSceneTools.RestoreAndValidate();
            OfficialMap12LabelTools.RestoreAndValidate();
            string path = "Assets/Game/Resources/Map12/Native/world-assets.asset";
            var assets = AssetDatabase.LoadAssetAtPath<OfficialMap12NativeWorldAssets>(path);
            if (!assets)
            {
                assets = ScriptableObject.CreateInstance<OfficialMap12NativeWorldAssets>();
                AssetDatabase.CreateAsset(assets, path);
            }
            assets.Terrain = Resources.Load<NativeMap13AssetCatalog>("Map12/Native/native-terrain-catalog");
            assets.Scene = Resources.Load<NativeMap13AssetCatalog>("Map12/Native/native-scene-catalog");
            assets.Environment = Resources.Load<NativeMap13AssetCatalog>("Map12/Native/native-environment-scene-catalog");
            assets.Labels = Resources.Load<NativeMap13AssetCatalog>("Map12/Native/native-region-labels-catalog");
            assets.Pipeline = OfficialMap12EnvironmentSceneTools.SharedAsset<NativeMap13AssetCatalog>(true)
                .Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
            assets.EnvironmentData = OfficialMap12EnvironmentSceneTools.SharedAsset<MapEnvData>(false);
            assets.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset");
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssetIfDirty(assets);
            OfficialMap12NativeWorldAssets.Load();
            if (PlayerSettings.colorSpace != ColorSpace.Linear) throw new InvalidDataException("APK PlayerSettings color space was not restored.");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            Debug.Log("Map12 complete native world resource binding passed; runtime uses only packaged local references.");
        }

        public static void RestoreAndRunChests()
        {
            try
            {
                Restore();
                Type.GetType("Zjcs.Editor.OfficialMap12PlayModeAcceptance, Zjcs.Editor", true)
                    .GetMethod("RunRuntimeChests", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void RestoreAndRunNativeWorld()
        {
            try
            {
                Restore();
                Type.GetType("Zjcs.Editor.OfficialMap12PlayModeAcceptance, Zjcs.Editor", true)
                    .GetMethod("RunRuntimeNativeWorld", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
