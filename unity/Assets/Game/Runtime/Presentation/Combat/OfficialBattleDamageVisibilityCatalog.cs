using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class OfficialBattleDamageVisibilityData
    {
        public string manifest;
        public OfficialBattleDamageVisibility[] damage;
        public OfficialBattleRoleDamageFilter[] roles;
    }
    [Serializable] public sealed class OfficialBattleDamageVisibility
    {
        public int damage_entity_id;
        public bool hide, show_zero;
    }
    [Serializable] public sealed class OfficialBattleRoleDamageFilter
    {
        public string kind, entity;
        public bool has_filter;
        public CustomDamageUITypeFilter filter;
    }

    /// <summary>Original prefab display fields; synthetic/legacy hits use MyFightDamage defaults.</summary>
    public sealed class OfficialBattleDamageVisibilityCatalog
    {
        private static OfficialBattleDamageVisibilityCatalog current;
        private readonly Dictionary<int, OfficialBattleDamageVisibility> damage = new Dictionary<int, OfficialBattleDamageVisibility>();
        private readonly Dictionary<int, CustomDamageUITypeFilter> monsterFilters = new Dictionary<int, CustomDamageUITypeFilter>();

        public static OfficialBattleDamageVisibilityCatalog Load()
        {
            if (current != null) return current;
            TextAsset asset = Resources.Load<TextAsset>("Combat/damage-visibility");
            if (asset == null) throw new InvalidOperationException("Original damage visibility bindings are missing.");
            var data = JsonUtility.FromJson<OfficialBattleDamageVisibilityData>(asset.text);
            if (data == null || data.manifest != "854_156383" || data.damage == null || data.damage.Length != 1367
                || data.roles == null || data.roles.Length != 551)
                throw new FormatException("Original damage visibility coverage changed.");
            var result = new OfficialBattleDamageVisibilityCatalog();
            foreach (var row in data.damage)
            {
                if (row == null || row.damage_entity_id <= 0 || result.damage.ContainsKey(row.damage_entity_id))
                    throw new FormatException("Invalid original damage visibility identity.");
                result.damage.Add(row.damage_entity_id, row);
            }
            foreach (var row in data.roles)
            {
                if (row == null) throw new FormatException("Missing original role filter record.");
                if (!row.has_filter) continue;
                if (row.filter == null) throw new FormatException("Original role filter fields are missing.");
                if (row.kind != "monster" || !int.TryParse(row.entity, out int id) || id <= 0
                    || result.monsterFilters.ContainsKey(id))
                    throw new FormatException("Original role filter binding requires investigation.");
                result.monsterFilters.Add(id, row.filter);
            }
            return current = result;
        }

        public bool CanShow(FightBattleDamageEvent hit, FightBattleUnitState target)
        {
            if (hit == null) return false;
            // The original OnSkillCostHp calls ShowDamage directly, bypassing MyFightDamage filters.
            if (hit.IsSkillCost) return true;
            if (!damage.TryGetValue(hit.DamageEntityId, out var flags) && hit.DamageEntityId > 0)
                throw new InvalidOperationException("Original damage visibility identity is not bound: " + hit.DamageEntityId);
            CustomDamageUITypeFilter filter = null;
            if (target != null && target.MonsterId > 0)
                monsterFilters.TryGetValue(target.MonsterTemplateId > 0 ? target.MonsterTemplateId : target.MonsterId, out filter);
            return CharacterDamageUIManager.CanShowDamage(hit, flags != null && flags.hide,
                flags == null || flags.show_zero, filter);
        }
    }
}
