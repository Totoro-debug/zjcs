using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Editor
{
    public static class OfficialStoneSkinGraphValidation
    {
        [Serializable] private sealed class Catalog { public Row[] graphs; }
        [Serializable] private sealed class Row { public int id; public string resource; }

        public static void Run()
        {
            try { Validate(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void BuildWindows()
        {
            Validate();
            Skills.OfficialSkillAssetTools.BuildWindowsValidation();
        }

        public static void Validate()
        {
            var input = Resources.Load<TextAsset>("Combat/stone-skin-current-graphs");
            var catalog = input ? JsonUtility.FromJson<Catalog>(input.text) : null;
            if (catalog?.graphs == null || catalog.graphs.Length != 2)
                throw new InvalidOperationException("Stone Skin native status catalog is incomplete.");
            var prefabs = new HashSet<GameObject>();
            var ids = new HashSet<int>();
            foreach (var row in catalog.graphs)
            {
                if (row == null || !ids.Add(row.id) || (row.id != 25033 && row.id != 24229))
                    throw new InvalidOperationException("Stone Skin native status identity differs.");
                var entity = Resources.Load<GameObject>(row.resource);
                if (!entity || !entity.GetComponent<MyEntity>() || !entity.GetComponent<MyFightStatus>()
                    || row.id == 25033 && !entity.GetComponent<MyFightStatusDamageSkill>()
                    || row.id == 24229 && !entity.GetComponent<MyFightStatusProp>())
                    throw new InvalidOperationException("Stone Skin status binding differs: " + row.id);
                var status = entity.GetComponent<MyFightStatus>();
                if (row.id == 24229)
                {
                    MonoBehaviour sound = null;
                    if (status.myPlayablePrefab)
                        foreach (var component in status.myPlayablePrefab.GetComponentsInChildren<MonoBehaviour>(true))
                            if (component && component.GetType().Name == "MyPlayableSound") sound = component;
                    var reference = sound ? new SerializedObject(sound).FindProperty("SoundEvent.WwiseObjectReference") : null;
                    if (!sound || reference == null || !reference.objectReferenceValue)
                        throw new InvalidOperationException("Stone Skin playable or sound is missing.");
                }
                prefabs.Add(entity);
                status.TryGetPlayables(ref prefabs);
            }
            foreach (var prefab in prefabs)
                foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                        throw new InvalidOperationException("Stone Skin graph has a missing script: " + node.name);
            Debug.Log("Stone Skin original listener, status, playable and sound bindings passed.");
        }
    }
}
