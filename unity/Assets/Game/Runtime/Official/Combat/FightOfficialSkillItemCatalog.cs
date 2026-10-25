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
        public int AiPriorityId = -1;
        public int LastAiPriorityId = -1;
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
        private readonly Dictionary<int, int> itemTypes = new Dictionary<int, int>();
        private readonly Dictionary<int, int> initialSkillByWeaponType =
            new Dictionary<int, int>();
        private readonly Dictionary<int, int[]> aiPriorities = new Dictionary<int, int[]>();
        private readonly Dictionary<int, ProfessionRule> professions =
            new Dictionary<int, ProfessionRule>();

        private sealed class ProfessionRule
        {
            public int PreProfession;
            public int[] Skills;
            public int[] FightUsedSkills;
        }

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
                FightOfficialCombatCatalog.RequiredField(rules, "item"), "rules.item"))
            {
                Dictionary<string, object> item = FightOfficialCombatCatalog.ParseObject(row,
                    "rules.item row");
                int itemId = FightOfficialCombatCatalog.ReadInt(item, "ClassId");
                if (itemId > 0)
                    catalog.itemTypes[itemId] = FightOfficialCombatCatalog.ReadInt(item, "ItemType");
            }
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
                    LastAiPriorityId = FightOfficialCombatCatalog.ReadInt(skill, "LastAiPriorityId", -1),
                    PassiveRankPropId = FightOfficialCombatCatalog.ReadInt(skill, "PassiveRankPropId"),
                    PassiveGroupLevelPropId = FightOfficialCombatCatalog.ReadInt(skill,
                        "PassiveGroupLevelPropId"),
                    PassiveRankFactorId = FightOfficialCombatCatalog.ReadInt(skill, "PassiveRankFactorId"),
                    PassiveStatusIds = FightOfficialCombatCatalog.ReadIntArray(skill, "PassiveStatusIdList"),
                    PassivePropFactors = FightOfficialCombatCatalog.ReadLongMap(skill,
                        "PassivePropFactors")
                };
            }
            foreach (object row in FightOfficialCombatCatalog.ParseArray(
                FightOfficialCombatCatalog.RequiredField(rules, "ai_priority"), "rules.ai_priority"))
            {
                Dictionary<string, object> priority = FightOfficialCombatCatalog.ParseObject(row,
                    "rules.ai_priority row");
                int priorityId = FightOfficialCombatCatalog.ReadInt(priority, "Id");
                int[] types = FightOfficialCombatCatalog.ReadIntArray(priority, "AiPriorityTypes");
                if (priorityId <= 0 || types.Length == 0)
                    throw new FormatException("Official AI priority row is invalid: " + priorityId);
                catalog.aiPriorities[priorityId] = types;
            }
            foreach (object row in FightOfficialCombatCatalog.ParseArray(
                FightOfficialCombatCatalog.RequiredField(rules, "profession_base"),
                "rules.profession_base"))
            {
                Dictionary<string, object> profession = FightOfficialCombatCatalog.ParseObject(row,
                    "rules.profession_base row");
                int professionId = FightOfficialCombatCatalog.ReadInt(profession, "Profession");
                if (professionId <= 0) continue;
                if (catalog.professions.ContainsKey(professionId))
                    throw new FormatException("Official profession row is duplicated: " + professionId);
                catalog.professions.Add(professionId, new ProfessionRule
                {
                    PreProfession = FightOfficialCombatCatalog.ReadInt(profession, "PrePro"),
                    Skills = FightOfficialCombatCatalog.ReadIntArray(profession, "Skills"),
                    FightUsedSkills = FightOfficialCombatCatalog.ReadIntArray(profession,
                        "FightUsedSkills")
                });
            }
            if (catalog.items.Count == 0)
                throw new FormatException("Official skill item table is empty.");
            if (catalog.professions.Count == 0)
                throw new FormatException("Official profession base table is empty.");

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

        public int[] PriorityTypes(int priorityId)
        {
            return aiPriorities.TryGetValue(priorityId, out int[] types)
                ? (int[])types.Clone() : Array.Empty<int>();
        }

        public FightSkillProfessionAvailability ProfessionAvailabilityFor(int itemId,
            int professionId, int currentWeaponType, int initialWeaponType)
        {
            if (itemId <= 0) return FightSkillProfessionAvailability.Unknown;
            if (!items.ContainsKey(itemId)) return FightSkillProfessionAvailability.Mismatch;

            int safeProfession = professionId;
            if (safeProfession <= 0)
            {
                int weaponType = currentWeaponType > 0 ? currentWeaponType : initialWeaponType;
                if (weaponType == SwordInitialWeaponType) safeProfession = 1;
                else if (weaponType == WandInitialWeaponType) safeProfession = 10;
            }
            if (safeProfession <= 0 || !professions.ContainsKey(safeProfession))
                return FightSkillProfessionAvailability.Unknown;

            var visited = new HashSet<int>();
            while (safeProfession > 0 && visited.Add(safeProfession)
                && professions.TryGetValue(safeProfession, out ProfessionRule rule))
            {
                if (Contains(rule.Skills, itemId) || Contains(rule.FightUsedSkills, itemId))
                    return FightSkillProfessionAvailability.Available;
                safeProfession = rule.PreProfession;
            }
            return FightSkillProfessionAvailability.Mismatch;
        }

        public int ItemTypeFor(int itemId)
        {
            return itemTypes.TryGetValue(itemId, out int itemType) ? itemType : 0;
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

        private static bool Contains(int[] values, int value)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Length; i++)
                if (values[i] == value) return true;
            return false;
        }
    }
}
