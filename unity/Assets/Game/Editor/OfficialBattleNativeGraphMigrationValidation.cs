using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static class OfficialBattleNativeGraphMigrationValidation
    {
        [Serializable] private sealed class Data
        {
            public Row[] skills = null, hits = null, moves = null, damages = null;
        }
        [Serializable] private sealed class Row
        {
            public int skill_entity_id = 0;
            public string entity_resource = null;
            public OfficialBattleSkillGraphBinding[] playables = null;
        }
        [Serializable] private sealed class StatusData { public StatusRow[] graphs = null; }
        [Serializable] private sealed class StatusRow { public int id = 0; public string resource = null; }

        public static void ValidateConqueror()
        {
            ValidateFamily("Combat/conqueror-current-skill-graphs", "ConquerorSkillGraphs");
            OfficialSparseShaderVariants.ValidateConquerorSkills();
        }

        public static void ValidateConquerorPassives()
        {
            ValidateFamily("Combat/conqueror-passive-current-skill-graphs", "ConquerorPassiveSkillGraphs");
            var asset = Resources.Load<TextAsset>("Combat/wind-orb-current-graphs");
            var data = asset == null ? null : JsonUtility.FromJson<StatusData>(asset.text);
            if (data?.graphs == null || data.graphs.Length != 4)
                throw new InvalidOperationException("Current wind-orb status bindings are missing.");
            var prefabs = new HashSet<GameObject>();
            foreach (var row in data.graphs)
            {
                var entity = Resources.Load<GameObject>(row.resource);
                if (!entity || !entity.GetComponent<MyEntity>() || !entity.GetComponent<MyFightStatus>()
                    || !AssetDatabase.GetAssetPath(entity).StartsWith("Assets/Official/Combat/WindOrbGraphs/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Current wind-orb status resolved to another entity or family.");
                if (row.id == 24385 && (!entity.GetComponent<MyFightStatusRoundBeforeEnd>() || !entity.GetComponent<MyFightStatusShareStack>()))
                    throw new InvalidOperationException("Current wind-orb lifecycle/share-stack markers are missing.");
                prefabs.Add(entity);
                entity.GetComponent<MyFightStatus>().TryGetPlayables(ref prefabs);
            }
            CheckMissing(prefabs);
            Debug.Log("Current native wind-orb statuses passed: original lifecycle, shared stacks and playable bindings, " + prefabs.Count + " prefabs.");
            OfficialSparseShaderVariants.ValidateWindOrbs();
        }

        private static void ValidateFamily(string resource, string family)
        {
            // Loading the effective catalog verifies every declared source hash.
            var catalog = OfficialBattleSkillGraphCatalog.TryLoadDefault();
            var asset = Resources.Load<TextAsset>(resource);
            var data = asset == null ? null : JsonUtility.FromJson<Data>(asset.text);
            if (data?.skills == null || data.hits == null || data.moves == null || data.damages == null)
                throw new InvalidOperationException("Current native conqueror bindings are missing.");
            var prefabs = new HashSet<GameObject>();
            Check(data.skills, typeof(MyFightSkill), prefabs, family);
            Check(data.hits, typeof(MyFightHitFixed), prefabs, family);
            Check(data.moves, typeof(MyFightActionSkillMove), prefabs, family);
            Check(data.damages, typeof(MyFightDamage), prefabs, family);
            var views = OfficialBattleSkillEntityViewCatalog.Load();
            foreach (var skill in data.skills)
            {
                var entity = Resources.Load<GameObject>(skill.entity_resource).GetComponent<MyFightSkill>();
                var fields = views.Find(skill.skill_entity_id);
                if (fields.preferredPosToPlayable != entity.PreferredPosToPlayable
                    || fields.showSkillReplay != entity.ShowSkillReplay)
                    throw new InvalidOperationException("Current native skill view fields differ: " + skill.skill_entity_id);
                foreach (var binding in skill.playables)
                    if (catalog.Find(skill.skill_entity_id, binding.field) != Resources.Load<GameObject>(binding.resource))
                        throw new InvalidOperationException("A migrated skill still uses its historical graph.");
            }
            CheckMissing(prefabs);
            Debug.Log("Current native " + family + " graphs passed: skill, hit, movement, damage and explicit playable bindings, "
                + "view fields and " + prefabs.Count + " prefabs without missing scripts.");
        }

        private static void CheckMissing(HashSet<GameObject> prefabs)
        {
            foreach (var prefab in prefabs)
                foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                        throw new InvalidOperationException("Current native graph has a missing script: " + prefab.name + "/" + node.name);
        }

        private static void Check(Row[] rows, Type type, HashSet<GameObject> prefabs, string family)
        {
            foreach (var row in rows)
            {
                var entity = Resources.Load<GameObject>(row.entity_resource);
                if (entity == null || entity.GetComponent<MyEntity>() == null || entity.GetComponent(type) == null)
                    throw new InvalidOperationException("Current native entity component is missing: " + row.entity_resource);
                if (!AssetDatabase.GetAssetPath(entity).StartsWith("Assets/Official/Combat/" + family + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Current native entity resolved to another resource family.");
                prefabs.Add(entity);
                var component = entity.GetComponent(type);
                foreach (var binding in row.playables)
                {
                    var playable = Resources.Load<GameObject>(binding.resource);
                    var field = type.GetField(binding.field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (playable == null || field == null || field.GetValue(component) as GameObject != playable)
                        throw new InvalidOperationException("Current native playable pointer differs: " + row.entity_resource + "/" + binding.field);
                    prefabs.Add(playable);
                }
            }
        }
    }
}
