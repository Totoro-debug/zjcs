using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    // Resource verification only; actual battle input and rendering have separate Play Mode checks.
    public static class OfficialBattleSkillGraphAssetValidation
    {
        public static void Validate()
        {
            ProjectTools.Validate();
            var json = Resources.Load<TextAsset>("Combat/skill-native-graphs");
            Require(json != null, "The generated skill graph catalog is missing.");
            var data = JsonUtility.FromJson<OfficialBattleSkillGraphCatalogData>(json.text);
            var catalog = OfficialBattleSkillGraphCatalog.TryLoadDefault();
            Require(data != null && data.manifest == "884_156692" && data.skills != null,
                "The skill graph catalog did not deserialize with the selected official version.");
            Require(data.skills.Length == 1884 && catalog != null && catalog.Count == data.skills.Length,
                "The active-version catalog must contain all 1884 audited official skill entities.");
            var fields = new HashSet<string> { "myPlayablePrefab", "UpPlayablePrefab",
                "DownPlayablePrefab", "LeftPlayablePrefab", "RightPlayablePrefab" };
            var resources = new HashSet<string>();
            int bindings = 0;
            foreach (var skill in data.skills)
            {
                Require(skill != null && skill.playables != null, "A skill lost its graph binding array.");
                var usedFields = new HashSet<string>();
                foreach (var binding in skill.playables)
                {
                    Require(binding != null && fields.Contains(binding.field) && usedFields.Add(binding.field),
                        "A skill has an unknown or duplicate graph field: " + skill.skill_entity_id);
                    Require(!string.IsNullOrEmpty(binding.resource) && binding.resource.StartsWith("Combat/SkillGraphs/",
                        StringComparison.Ordinal), "A skill graph escaped its explicit resource root.");
                    resources.Add(binding.resource);
                    bindings++;
                }
            }
            Require(bindings == 2286 && resources.Count == 2216,
                "The catalog must preserve all 2286 official fields and 2216 distinct graph roots.");

            var shaders = new HashSet<string>();
            int loaded = 0;
            foreach (string resource in resources)
            {
                GameObject prefab = Resources.Load<GameObject>(resource);
                Require(prefab != null, "A skill graph cannot load: " + resource);
                string path = AssetDatabase.GetAssetPath(prefab);
                Require(path.StartsWith("Assets/Official/Combat/SkillGraphRuntime/", StringComparison.Ordinal),
                    "A skill graph resolved to a competing resource directory: " + path);
                foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0,
                        "A graph component script is missing: " + path + " / " + node.name);
                foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                    if (dependency.EndsWith(".shader", StringComparison.Ordinal)) shaders.Add(dependency);
                // Do not retain the full texture closure in memory while checking every prefab.
                if (++loaded % 32 == 0) EditorUtility.UnloadUnusedAssetsImmediate();
            }
            foreach (string path in shaders)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Require(shader != null && !ShaderUtil.ShaderHasError(shader), "A graph shader failed: " + path);
            }
            EditorUtility.UnloadUnusedAssetsImmediate();
            Debug.Log("Skill graph assets passed: 1884 entities, 2286 directed fields, 2216 distinct prefabs, "
                + "no missing scripts, " + shaders.Count + " dependency shaders. This does not verify battle rendering.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Official skill graph assets: " + message);
        }
    }
}
