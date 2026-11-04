using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialBattleSkillGraphBinding
    {
        public string field;
        public string resource;
    }

    [Serializable]
    public sealed class OfficialBattleSkillGraphRecord
    {
        public int skill_entity_id;
        public OfficialBattleSkillGraphBinding[] playables;
    }

    [Serializable]
    public sealed class OfficialBattleSkillGraphCatalogData
    {
        public string manifest;
        public OfficialBattleSkillGraphRecord[] skills;
    }

    /// <summary>Loads the verified active-version FightSkill prefab bindings.</summary>
    public sealed class OfficialBattleSkillGraphCatalog
    {
        private const string ResourcePath = "Combat/skill-native-graphs";
        private readonly Dictionary<int, OfficialBattleSkillGraphRecord> records =
            new Dictionary<int, OfficialBattleSkillGraphRecord>();

        private OfficialBattleSkillGraphCatalog(OfficialBattleSkillGraphCatalogData data)
        {
            if (data == null || data.manifest != "884_156692" || data.skills == null)
                throw new InvalidOperationException("Official battle skill graph catalog has the wrong source.");
            foreach (OfficialBattleSkillGraphRecord record in data.skills)
            {
                if (record == null || record.skill_entity_id <= 0 || records.ContainsKey(record.skill_entity_id))
                    throw new InvalidOperationException("Official battle skill graph catalog contains a duplicate skill.");
                records.Add(record.skill_entity_id, record);
            }
        }

        public int Count => records.Count;

        internal bool ContainsSkill(int skillEntityId) => records.ContainsKey(skillEntityId);

        public static OfficialBattleSkillGraphCatalog TryLoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return null;
            OfficialBattleSkillGraphCatalogData data =
                JsonUtility.FromJson<OfficialBattleSkillGraphCatalogData>(asset.text);
            return new OfficialBattleSkillGraphCatalog(data);
        }

        public GameObject Find(int skillEntityId, string direction)
        {
            if (!records.TryGetValue(skillEntityId, out OfficialBattleSkillGraphRecord record)
                || record.playables == null) return null;
            OfficialBattleSkillGraphBinding fallback = null;
            foreach (OfficialBattleSkillGraphBinding binding in record.playables)
            {
                if (binding == null || string.IsNullOrEmpty(binding.resource)) continue;
                if (binding.field == direction)
                    return LoadDeclaredPrefab(skillEntityId, binding);
                if (binding.field == "myPlayablePrefab") fallback = binding;
            }
            return fallback == null ? null : LoadDeclaredPrefab(skillEntityId, fallback);
        }

        private static GameObject LoadDeclaredPrefab(int skillEntityId, OfficialBattleSkillGraphBinding binding)
        {
            GameObject prefab = Resources.Load<GameObject>(binding.resource);
            if (prefab == null)
                throw new InvalidOperationException("Official battle skill graph asset is missing: skill="
                    + skillEntityId + ", field=" + binding.field + ", resource=" + binding.resource);
            return prefab;
        }
    }
}
