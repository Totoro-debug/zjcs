using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Editor
{
    public static class OfficialSageRoundSecretNativeValidation
    {
        [Serializable] private sealed class Data { public Row[] graphs = null; }
        [Serializable] private sealed class Row { public int id = 0; public string resource = null; }

        public static void Validate()
        {
            ValidateFamily("Combat/sage-blessing-current-status-graphs", "SageBlessingGraphs", 25119, new[] { 24395 });
            ValidateFamily("Combat/sage-dark-star-current-status-graphs", "SageDarkStarGraphs", 25120, new[] { 24396, 24398, 24399 });
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Native HUD validation requires a graphics device.");
            ProjectTools.Validate();
            Debug.Log("Sage personal-round secrets: original status components, effects, sounds and native HUD validation passed.");
        }

        private static void ValidateFamily(string resource, string family, int listener, int[] gains)
        {
            var asset = Resources.Load<TextAsset>(resource);
            var data = asset ? JsonUtility.FromJson<Data>(asset.text) : null;
            var remaining = new HashSet<int>(gains) { listener };
            if (data?.graphs == null || data.graphs.Length != remaining.Count)
                throw new InvalidOperationException("The original round-secret status catalog is missing: " + family);
            string directory = "Assets/Official/Combat/" + family + "/";
            foreach (var row in data.graphs)
            {
                var entity = Resources.Load<GameObject>(row.resource);
                if (!remaining.Remove(row.id) || !entity || !entity.GetComponent<MyEntity>()
                    || !entity.GetComponent<MyFightStatus>() || !entity.GetComponent<MyAction>()
                    || !AssetDatabase.GetAssetPath(entity).StartsWith(directory, StringComparison.Ordinal)
                    || row.id == listener && !entity.GetComponent<MyFightStatusRoundStart>()
                    || row.id != listener && !entity.GetComponent<MyFightStatusProp>())
                    throw new InvalidOperationException("The original round-secret status component or identity is missing: " + row.id);
                var effect = new SerializedObject(entity.GetComponent<MyAction>()).FindProperty("myEffect").objectReferenceValue;
                if (row.id == listener ? effect != null : effect == null)
                    throw new InvalidOperationException("The original round-secret effect binding changed: " + row.id);
            }
            if (remaining.Count != 0) throw new InvalidOperationException("The round-secret status catalog is incomplete.");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { directory.TrimEnd('/') }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                        throw new InvalidOperationException("The original round-secret prefab has a missing script: " + prefab.name);
            }
            Debug.Log("Original round-secret status and effect bindings passed: " + family);
        }
    }
}
