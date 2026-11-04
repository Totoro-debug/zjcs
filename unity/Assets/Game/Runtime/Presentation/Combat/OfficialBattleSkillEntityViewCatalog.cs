using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialBattleSkillEntityViewRecord
    {
        public int skillEntityId;
        public bool preferredPosToPlayable;
        public bool showSkillReplay;
    }

    [Serializable]
    public sealed class OfficialBattleSkillEntityViewData
    {
        public string manifest;
        public OfficialBattleSkillEntityViewRecord[] skills;
    }

    public sealed class OfficialBattleSkillEntityViewCatalog
    {
        private readonly Dictionary<int, OfficialBattleSkillEntityViewRecord> records =
            new Dictionary<int, OfficialBattleSkillEntityViewRecord>();

        public static OfficialBattleSkillEntityViewCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("Combat/skill-entity-views");
            if (asset == null) throw new InvalidOperationException("Original MyFightSkill view configuration is missing.");
            var data = JsonUtility.FromJson<OfficialBattleSkillEntityViewData>(asset.text);
            if (data == null || data.manifest != "884_156692" || data.skills == null)
                throw new InvalidOperationException("Original MyFightSkill view configuration has the wrong source.");
            var result = new OfficialBattleSkillEntityViewCatalog();
            foreach (var record in data.skills)
            {
                if (record == null || record.skillEntityId <= 0 || result.records.ContainsKey(record.skillEntityId))
                    throw new InvalidOperationException("Original MyFightSkill view configuration has duplicate entities.");
                result.records.Add(record.skillEntityId, record);
            }
            return result;
        }

        public OfficialBattleSkillEntityViewRecord Find(int skillEntityId) =>
            records.TryGetValue(skillEntityId, out var value) ? value :
            throw new InvalidOperationException("Original MyFightSkill view is not bound: " + skillEntityId);
    }
}
