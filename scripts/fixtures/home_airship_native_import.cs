using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Spine.Unity;
using Zjcs.Official.HomeFurniture;

public static class AirshipImportProbe
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            string[] names = { "Airship", "AirshipSnow", "Airship_YellowDuck", "Airship_YellowDuckSnow" };
            foreach (string name in names)
            {
                string path = "Assets/Official/Home/Airship/Native/" + name + ".prefab";
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(root, "Missing prefab " + name);
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0,
                        name + " missing component at " + node.name);
                AirshipFurniture[] controllers = root.GetComponentsInChildren<AirshipFurniture>(true);
                Require(controllers.Length == 1, name + " controller count");
                var fields = new SerializedObject(controllers[0]);
                Require(fields.FindProperty("mAirship").objectReferenceValue is SkeletonAnimation, name + " skeleton binding");
                Require(!string.IsNullOrEmpty(fields.FindProperty("mAirshipActiveAnim").stringValue), name + " animation binding");
                int expectedSlots = name.Contains("YellowDuck") ? 0 : 2;
                Require(fields.FindProperty("mItemBgList").arraySize == expectedSlots, name + " background bindings");
                Require(fields.FindProperty("mItemIconList").arraySize == expectedSlots, name + " display bindings");
                Require(fields.FindProperty("mLightSpriteRenderers").arraySize == (expectedSlots == 0 ? 0 : 4), name + " light bindings");
                var states = fields.FindProperty("mStateSwitcher").FindPropertyRelative("mStateConfigs");
                Require(states.arraySize > 0 && states.GetArrayElementAtIndex(0).managedReferenceValue != null,
                    name + " managed state config");
                foreach (SkeletonAnimation skeleton in root.GetComponentsInChildren<SkeletonAnimation>(true))
                    Require(skeleton.skeletonDataAsset, name + " skeleton data");
                Debug.Log("AIRSHIP_IMPORT_PROBE " + name + " passed");
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
