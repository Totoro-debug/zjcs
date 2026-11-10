using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;

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
            Require(data.skills.Length == 1906 && catalog != null && catalog.Count == data.skills.Length,
                "The active-version catalog must contain 1884 skill entities and 22 ordinary movement skills.");
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
            Require(bindings == 2296 && resources.Count == 2226,
                "The catalog must preserve all 2296 official fields and 2226 distinct graph roots.");
            ValidateOrdinaryMovementEntities(data);

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
            Debug.Log("Skill graph assets passed: 1906 entities, 2296 directed fields, 2226 distinct prefabs, "
                + "no missing scripts, " + shaders.Count + " dependency shaders. This does not verify battle rendering.");
        }

        private static void ValidateOrdinaryMovementEntities(OfficialBattleSkillGraphCatalogData data)
        {
            var expected = new HashSet<int> { 15300, 15310, 15320, 15330, 15340,
                19800, 19810, 19820, 19830, 19840, 19850, 19870, 19890, 19900,
                19910, 19920, 19930, 19950, 19960, 19970, 19980, 19990 };
            int withGraph = 0;
            foreach (var record in data.skills)
            {
                if (!expected.Remove(record.skill_entity_id)) continue;
                Require(!string.IsNullOrEmpty(record.entity_resource)
                    && record.entity_resource.Contains("/FightSkill/Move/"),
                    "An ordinary movement skill lost its full original entity binding.");
                var entity = Resources.Load<GameObject>(record.entity_resource);
                Require(entity != null && entity.GetComponent<MyEntity>() != null
                    && entity.GetComponent<MyFightSkill>() != null,
                    "An ordinary movement skill lost its original entity or skill view: " + record.skill_entity_id);
                foreach (Transform node in entity.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0,
                        "An ordinary movement entity has a missing script: " + record.entity_resource);
                var view = entity.GetComponent<MyFightSkill>();
                if (record.playables.Length == 0)
                    Require(view.myPlayablePrefab == null && view.UpPlayablePrefab == null
                        && view.DownPlayablePrefab == null && view.LeftPlayablePrefab == null
                        && view.RightPlayablePrefab == null,
                        "An originally empty movement graph was replaced.");
                else
                {
                    Require(record.playables.Length == 1 && record.playables[0].field == "myPlayablePrefab"
                        && view.myPlayablePrefab == Resources.Load<GameObject>(record.playables[0].resource),
                        "An ordinary movement graph differs from the original explicit entity pointer.");
                    withGraph++;
                }
            }
            Require(expected.Count == 0 && withGraph == 10,
                "The 22 original ordinary movement entities and their 10 graphs must remain intact.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Official skill graph assets: " + message);
        }
    }
}
