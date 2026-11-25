using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Spine.Unity;

public static class HomeFruitTreeNativeValidation
{
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static void Run()
    {
        try
        {
            const string root = "Assets/Official/Home/FruitTree/Native/";
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(root + "FruitTree.prefab");
            var fruit = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Fruit.prefab");
            Require(tree && fruit, "Both original prefabs must import.");
            foreach (var prefab in new[] { tree, fruit })
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                        "Missing native script: " + prefab.name + "/" + transform.name);
            var scripts = tree.GetComponentsInChildren<MonoBehaviour>(true);
            Require(scripts.Count(value => value.GetType().Name == "HomeFruitTree") == 1, "Original parent controller.");
            Require(scripts.Count(value => value.GetType().Name == "HomeFruitTreePet") == 1, "Original pet subtree.");
            Require(scripts.Count(value => value.GetType().Name == "HomeFruitTreeProduceFruit") == 1, "Original production subtree.");
            var output = scripts.Single(value => value.GetType().Name == "HomeFruitTreeHangUpHandlerFruitItem");
            var serialized = new SerializedObject(output);
            var effect = serialized.FindProperty("mEffectItem").objectReferenceValue as Component;
            Require(effect && effect.gameObject == fruit, "The output handler must point to the original external Fruit component.");
            Require(serialized.FindProperty("mEffectItemSlotConfigs").arraySize == 10, "All ten original fruit slots.");
            var renderers = tree.GetComponentsInChildren<SkeletonAnimation>(true);
            Require(renderers.Length > 0, "Original Spine renderer must be retained.");
            foreach (var renderer in renderers)
            {
                Require(renderer.skeletonDataAsset, "Original skeleton data reference.");
                var data = renderer.skeletonDataAsset.GetSkeletonData(false);
                Require(data != null && data.Bones.Count > 0 && data.Animations.Count > 0, "Native skeleton and animation data must decode.");
            }
            Debug.Log("HOME_FRUIT_TREE_NATIVE_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
