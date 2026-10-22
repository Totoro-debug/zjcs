using System;
using System.Collections.Generic;
using System.Globalization;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Official skill-item row from the pinned rules payload. The skill item class id is the
    /// value stored in a character skill plan; <see cref="EntityId"/> is the fight EC entity that
    /// the combat catalog assembles into a battle skill.
    /// </summary>
    public sealed class FightOfficialSkillItem
    {
        public int ItemId;
        public int EntityId;
        public int AiPriorityId;
        public int PassiveRankPropId;
        public int PassiveGroupLevelPropId;
        public int PassiveRankFactorId;
        public int[] PassiveStatusIds = Array.Empty<int>();
        public Dictionary<int, long> PassivePropFactors = new Dictionary<int, long>();
    }

    /// <summary>
    /// Reads the official skill item table and the initial weapon skill settings from
    /// <c>rules.json</c>, verifying the payload digest against its source manifest.
    /// </summary>
    public sealed class FightOfficialSkillItemCatalog
    {
        public const int SwordInitialWeaponType = 200;
        public const int WandInitialWeaponType = 201;

        private readonly Dictionary<int, FightOfficialSkillItem> items =
            new Dictionary<int, FightOfficialSkillItem>();
        private readonly Dictionary<int, int> initialSkillByWeaponType =
            new Dictionary<int, int>();

        private FightOfficialSkillItemCatalog() { }

        public int Count => items.Count;

        public static FightOfficialSkillItemCatalog Load(string rulesJson, string rulesSha256,
            string rulesManifest)
        {
            FightOfficialCombatCatalog.VerifyPayloadForDerived("rules.json", rulesJson, rulesSha256,
                rulesManifest);
            Dictionary<string, object> rules = FightOfficialCombatCatalog.ParseObject(rulesJson,
                "rules.json");
            var catalog = new FightOfficialSkillItemCatalog();
            foreach (object row in FightOfficialCombatCatalog.ParseArray(
                FightOfficialCombatCatalog.RequiredField(rules, "skill"), "rules.skill"))
            {
                Dictionary<string, object> skill = FightOfficialCombatCatalog.ParseObject(row,
                    "rules.skill row");
                int itemId = FightOfficialCombatCatalog.ReadInt(skill, "ClassId");
                int entityId = FightOfficialCombatCatalog.ReadInt(skill, "EcEntityId");
                // Passive-only skill items have no fight EC skill entity; they contribute
                // passive properties/statuses instead and stay resolvable by item id.
                if (itemId <= 0)
                    throw new FormatException("Official skill item row is missing its class id.");
                catalog.items[itemId] = new FightOfficialSkillItem
                {
                    ItemId = itemId,
                    EntityId = entityId,
                    AiPriorityId = FightOfficialCombatCatalog.ReadInt(skill, "AiPriorityId", -1),
                    PassiveRankPropId = FightOfficialCombatCatalog.ReadInt(skill, "PassiveRankPropId"),
                    PassiveGroupLevelPropId = FightOfficialCombatCatalog.ReadInt(skill,
                        "PassiveGroupLevelPropId"),
                    PassiveRankFactorId = FightOfficialCombatCatalog.ReadInt(skill, "PassiveRankFactorId"),
                    PassiveStatusIds = FightOfficialCombatCatalog.ReadIntArray(skill, "PassiveStatusIdList"),
                    PassivePropFactors = FightOfficialCombatCatalog.ReadLongMap(skill,
                        "PassivePropFactors")
                };
            }
            if (catalog.items.Count == 0)
                throw new FormatException("Official skill item table is empty.");

            Dictionary<string, object> settings = null;
            if (rules.TryGetValue("game_settings", out object rawSettings))
                settings = FightOfficialCombatCatalog.ParseObject(rawSettings, "game_settings");
            if (settings != null)
            {
                int sword = FirstId(settings, "SkillOptionsWhenChooseSword");
                int wand = FirstId(settings, "SkillOptionsWhenChooseWand");
                if (sword > 0) catalog.initialSkillByWeaponType[SwordInitialWeaponType] = sword;
                if (wand > 0) catalog.initialSkillByWeaponType[WandInitialWeaponType] = wand;
            }
            return catalog;
        }

        public bool TryGet(int itemId, out FightOfficialSkillItem item)
        {
            return items.TryGetValue(itemId, out item);
        }

        public FightOfficialSkillItem Required(int itemId)
        {
            if (!items.TryGetValue(itemId, out FightOfficialSkillItem item))
                throw new KeyNotFoundException("Official skill item is absent: " + itemId);
            return item;
        }

        /// <summary>
        /// Official initial active skill for the chosen starting weapon
        /// (<c>SkillOptionsWhenChooseSword</c> / <c>SkillOptionsWhenChooseWand</c>).
        /// </summary>
        public int InitialActiveSkill(int weaponType)
        {
            int weapon = weaponType == WandInitialWeaponType ? WandInitialWeaponType : SwordInitialWeaponType;
            if (initialSkillByWeaponType.TryGetValue(weapon, out int itemId)) return itemId;
            if (initialSkillByWeaponType.TryGetValue(SwordInitialWeaponType, out itemId)) return itemId;
            return 0;
        }

        private static int FirstId(Dictionary<string, object> settings, string key)
        {
            int[] values = FightOfficialCombatCatalog.ReadIntArray(settings, key);
            return values.Length == 0 ? 0 : values[0];
        }
    }
}
