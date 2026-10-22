using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfficialCharacterSpriteBinding
    {
        public string sourcePath;
        public Sprite sprite;
    }

    [CreateAssetMenu(menuName = "Zjcs/Official Character Asset Catalog")]
    public sealed class OfficialCharacterAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Character/OfficialCharacterAssetCatalog";

        [SerializeField] private TextAsset characterOverview;
        [SerializeField] private TextAsset characterProperties;
        [SerializeField] private TextAsset progressionRules;
        [SerializeField] private TextAsset timedAppearanceItems;
        [SerializeField] private TextAsset wardrobeItems;
        [SerializeField] private TextAsset wardrobeRules;
        [SerializeField] private TextAsset wardrobeUi;
        [SerializeField] private TextAsset characterPanels;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private TextAsset skillRules;
        [SerializeField] private TextAsset skillRecommendations;
        [SerializeField] private OfficialCharacterSpriteBinding[] sprites = System.Array.Empty<OfficialCharacterSpriteBinding>();

        public TextAsset CharacterOverview => characterOverview;
        public TextAsset CharacterProperties => characterProperties;
        public TextAsset ProgressionRules => progressionRules;
        public TextAsset TimedAppearanceItems => timedAppearanceItems;
        public TextAsset WardrobeItems => wardrobeItems;
        public TextAsset WardrobeRules => wardrobeRules;
        public TextAsset WardrobeUi => wardrobeUi;
        public TextAsset CharacterPanels => characterPanels;
        public TextAsset SourceManifest => sourceManifest;
        public TextAsset SkillRules => skillRules;
        public TextAsset SkillRecommendations => skillRecommendations;

        public Sprite FindSprite(string sourceUri)
        {
            string path = NormalizeSpritePath(sourceUri);
            foreach (OfficialCharacterSpriteBinding binding in sprites ?? System.Array.Empty<OfficialCharacterSpriteBinding>())
                if (binding != null && binding.sourcePath == path) return binding.sprite;
            return null;
        }

        public static string NormalizeSpritePath(string sourceUri)
        {
            const string prefix = "res://assets/official/";
            if (string.IsNullOrEmpty(sourceUri) || !sourceUri.StartsWith(prefix, StringComparison.Ordinal))
                return string.Empty;
            return sourceUri.Substring(prefix.Length).Replace('\\', '/');
        }

        public static OfficialCharacterAssetCatalog LoadDefault()
        {
            OfficialCharacterAssetCatalog catalog = Resources.Load<OfficialCharacterAssetCatalog>(DefaultResourcePath);
            if (catalog == null) throw new InvalidOperationException("Official character assets are not installed.");
            catalog.ValidateBindings();
            return catalog;
        }

        public void ValidateBindings()
        {
            if (characterOverview == null || characterProperties == null || progressionRules == null
                || timedAppearanceItems == null || wardrobeItems == null || characterPanels == null
                || wardrobeRules == null || wardrobeUi == null || sourceManifest == null || skillRules == null
                || skillRecommendations == null)
                throw new InvalidOperationException("Official character TextAssets are not bound.");
            if (sprites == null || sprites.Length == 0 || System.Array.Exists(sprites, value => value == null || value.sprite == null))
                throw new InvalidOperationException("Official character sprite bindings are incomplete.");
        }

        public Zjcs.Offline.Skills.OfflineSkillRules LoadSkillRules()
        {
            ValidateBindings();
            Zjcs.Offline.Skills.OfflineSkillRules rules = JsonUtility.FromJson<Zjcs.Offline.Skills.OfflineSkillRules>(
                skillRules.text);
            if (rules == null || rules.Skills == null || rules.Skills.Length == 0)
                throw new InvalidOperationException("Official skill progression rules are invalid.");
            return rules;
        }

        public OfficialSkillRecommendationSet LoadSkillRecommendations()
        {
            ValidateBindings();
            OfficialSkillRecommendationSet set = JsonUtility.FromJson<OfficialSkillRecommendationSet>(
                skillRecommendations.text);
            if (set == null || set.recommendations == null || set.recommendations.Length == 0)
                throw new InvalidOperationException("Official skill recommendations are invalid.");
            return set;
        }

        public OfficialCharacterCatalog Load()
        {
            ValidateBindings();
            return OfficialCharacterCatalog.Parse(progressionRules.text, timedAppearanceItems.text,
                wardrobeItems.text, wardrobeRules.text, characterOverview.text);
        }
    }

    /// <summary>Local character state used to evaluate official condition ids.</summary>
    public sealed class OfficialConditionContext
    {
        public int Level;
        public HashSet<int> UnlockedMapClassIds = new HashSet<int>();
        public HashSet<int> RegionCodes = new HashSet<int>();
    }

    public sealed class OfficialCharacterCatalog
    {
        private readonly Dictionary<string, List<Dictionary<string, object>>> tables;
        private readonly Dictionary<int, Dictionary<string, object>> items;
        private readonly Dictionary<int, WardrobeSkinDefinition> skins;
        private readonly Dictionary<int, WardrobeItemDefinition> appearances;
        private readonly HashSet<string> allowed;
        private readonly Dictionary<int, long> levelExperience;
        private readonly List<CharacterLevelLimitRecord> levelLimits;
        private readonly List<CharacterBlessLevelRecord> blessLevels;
        private readonly Dictionary<int, Dictionary<string, object>> timedAppearanceItems;
        private readonly Dictionary<string, int> propertyIds;
        private readonly Dictionary<int, Dictionary<int, Dictionary<int, long>>> levelProps;
        private readonly Dictionary<string, List<Dictionary<string, object>>> wardrobeTables;
        private readonly Dictionary<int, Dictionary<string, object>> wardrobeDefinitions;
        private readonly Dictionary<int, int> rankBySubRank;
        private readonly Dictionary<string, object> defaultAppearance;
        private readonly HashSet<int> appearanceItemTypes;
        private Dictionary<int, Dictionary<string, object>> conditionCache;
        private readonly OfficialPropCatalog props;
        private readonly Dictionary<int, string> itemIcons;
        private readonly Dictionary<int, string> professionNames;
        private readonly Dictionary<int, string> professionIcons;

        private OfficialCharacterCatalog(Dictionary<string, List<Dictionary<string, object>>> tables,
            Dictionary<int, Dictionary<string, object>> items, Dictionary<int, WardrobeSkinDefinition> skins,
            Dictionary<int, WardrobeItemDefinition> appearances, HashSet<string> allowed,
            Dictionary<int, long> levelExperience, List<CharacterLevelLimitRecord> levelLimits,
            List<CharacterBlessLevelRecord> blessLevels,
            Dictionary<int, Dictionary<string, object>> timedAppearanceItems,
            Dictionary<string, int> propertyIds,
            Dictionary<int, Dictionary<int, Dictionary<int, long>>> levelProps,
            Dictionary<string, List<Dictionary<string, object>>> wardrobeTables,
            Dictionary<int, Dictionary<string, object>> wardrobeDefinitions,
            Dictionary<int, int> rankBySubRank,
            Dictionary<string, object> defaultAppearance,
            HashSet<int> appearanceItemTypes,
            OfficialPropCatalog props,
            Dictionary<int, string> itemIcons,
            Dictionary<int, string> professionNames,
            Dictionary<int, string> professionIcons)
        {
            this.tables = tables;
            this.items = items;
            this.skins = skins;
            this.appearances = appearances;
            this.allowed = allowed;
            this.levelExperience = levelExperience;
            this.levelLimits = levelLimits;
            this.blessLevels = blessLevels;
            this.timedAppearanceItems = timedAppearanceItems;
            this.propertyIds = propertyIds;
            this.levelProps = levelProps;
            this.wardrobeTables = wardrobeTables;
            this.wardrobeDefinitions = wardrobeDefinitions;
            this.rankBySubRank = rankBySubRank;
            this.defaultAppearance = defaultAppearance;
            this.appearanceItemTypes = appearanceItemTypes;
            this.props = props;
            this.itemIcons = itemIcons;
            this.professionNames = professionNames;
            this.professionIcons = professionIcons;
        }

        public string ProfessionName(int profession)
        {
            return professionNames.TryGetValue(profession, out string name) ? name : string.Empty;
        }

        public string ProfessionIconUri(int profession)
        {
            return professionIcons.TryGetValue(profession, out string icon) ? icon : string.Empty;
        }

        public OfficialPropCatalog Props => props;

        /// <summary>Original <c>ItemInfo.RawIcon</c> sprite of an item when it is part of the pinned character catalog.</summary>
        public string ItemIconUri(int itemId)
        {
            return itemIcons.TryGetValue(itemId, out string icon) ? icon : string.Empty;
        }

        public IReadOnlyList<CharacterLevelLimitRecord> LevelLimits => levelLimits;
        public IReadOnlyList<CharacterBlessLevelRecord> BlessLevels => blessLevels;
        public IReadOnlyDictionary<int, WardrobeItemDefinition> Appearances => appearances;
        public IReadOnlyDictionary<int, WardrobeSkinDefinition> Skins => skins;
        public ISet<string> AllowedSkins => allowed;
        public IReadOnlyDictionary<int, Dictionary<string, object>> Items => items;
        public IReadOnlyDictionary<string, int> PropertyIds => propertyIds;

        public IReadOnlyDictionary<int, long> GetLevelProps(int propClassId, int level)
        {
            if (!levelProps.TryGetValue(propClassId, out Dictionary<int, Dictionary<int, long>> rows))
                return new Dictionary<int, long>();
            if (rows.TryGetValue(level, out Dictionary<int, long> props)) return props;
            int maximum = rows.Keys.DefaultIfEmpty(0).Max();
            return level > maximum && rows.TryGetValue(maximum, out props)
                ? props : new Dictionary<int, long>();
        }

        public IReadOnlyDictionary<int, long> CalcCharacterBaseBattleProperties(int level, int subRank)
        {
            Dictionary<string, object> settings = Rows("game_settings").FirstOrDefault();
            if (settings == null) throw new FormatException("Official character battle property settings are missing.");
            int levelClass = Integer(settings, "PlayerLevelBattlePropId");
            int rankClass = Integer(settings, "PlayerSubRankBattlePropId");
            int roleGroup = Integer(settings, "PlayerFightRolePropGroupId");
            var result = new Dictionary<int, long>();
            Merge(result, GetLevelProps(levelClass, level));
            Merge(result, GetLevelProps(rankClass, subRank));
            Dictionary<string, object> role = Rows("role_prop_group").FirstOrDefault(row =>
                Integer(row, "ID") == roleGroup && Integer(row, "SubRank") == subRank);
            if (role != null) Merge(result, GetLevelProps(Integer(role, "LevelPropID"), level));
            return result;
        }

        public int RankOfSubRank(int subRank)
        {
            return rankBySubRank.TryGetValue(subRank, out int rank) ? rank : 0;
        }

        /// <summary>
        /// Evaluates one official condition id (rules.json <c>conditions</c>) against the local
        /// character state. Condition types that need data this save does not own stay false
        /// instead of being opened for free.
        /// </summary>
        public bool EvaluateCondition(int conditionId, OfficialConditionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return EvaluateConditionNode(FindCondition(conditionId), context, new HashSet<int>());
        }

        private Dictionary<string, object> FindCondition(int conditionId)
        {
            if (conditionCache == null)
            {
                conditionCache = new Dictionary<int, Dictionary<string, object>>();
                foreach (Dictionary<string, object> row in Rows("conditions"))
                {
                    int classId = Integer(row, "ClassId");
                    if (classId > 0 && !conditionCache.ContainsKey(classId)) conditionCache.Add(classId, row);
                }
            }
            return conditionCache.TryGetValue(conditionId, out Dictionary<string, object> condition)
                ? condition : null;
        }

        private bool EvaluateConditionNode(Dictionary<string, object> row,
            OfficialConditionContext context, HashSet<int> visiting)
        {
            if (row == null) return false;
            if (!row.TryGetValue("Condition", out object raw) || !(raw is Dictionary<string, object> condition))
                return false;
            string type = Text(condition, "_type");
            switch (type)
            {
                case "NoneCondition":
                    return true;
                case "LevelCondition":
                    return Compare(context.Level, Integer(condition, "Level"), Text(condition, "Op"));
                case "CompositeAndCondition":
                    foreach (int id in Integers(condition, "Ids"))
                        if (!EvaluateConditionId(id, context, visiting)) return false;
                    return true;
                case "CompositeOrCondition":
                    foreach (int id in Integers(condition, "Ids"))
                        if (EvaluateConditionId(id, context, visiting)) return true;
                    return false;
                case "MapUnlockCondition":
                    return context.UnlockedMapClassIds.Contains(Integer(condition, "MapClassId"));
                case "RegionCodeCondition":
                    bool contains = false;
                    foreach (int code in Integers(condition, "RegionCodes"))
                        if (context.RegionCodes.Contains(code)) { contains = true; break; }
                    return Boolean(condition, "Not") ? !contains : contains;
                default:
                    return false;
            }
        }

        private bool EvaluateConditionId(int conditionId, OfficialConditionContext context,
            HashSet<int> visiting)
        {
            if (conditionId <= 0 || !visiting.Add(conditionId)) return false;
            try
            {
                return EvaluateConditionNode(FindCondition(conditionId), context, visiting);
            }
            finally
            {
                visiting.Remove(conditionId);
            }
        }

        private static bool Compare(int value, int expected, string op)
        {
            switch (op)
            {
                case ">=": return value >= expected;
                case ">": return value > expected;
                case "<=": return value <= expected;
                case "<": return value < expected;
                case "==": return value == expected;
                case "!=": return value != expected;
                default: return false;
            }
        }

        /// <summary>
        /// Official initial active skill id of the starting weapon
        /// (<c>SkillOptionsWhenChooseSword</c> / <c>SkillOptionsWhenChooseWand</c>).
        /// </summary>
        public int InitialActiveSkill(bool wand)
        {
            Dictionary<string, object> settings = Rows("game_settings").FirstOrDefault();
            if (settings == null) return 0;
            List<int> ids = Integers(settings,
                wand ? "SkillOptionsWhenChooseWand" : "SkillOptionsWhenChooseSword").ToList();
            return ids.Count == 0 ? 0 : ids[0];
        }

        public IReadOnlyDictionary<int, int> DefaultSkins(int sex, int profession)
        {
            var result = new Dictionary<int, int>();
            string key = sex.ToString(CultureInfo.InvariantCulture) + ":" + profession.ToString(CultureInfo.InvariantCulture);
            if (!defaultAppearance.TryGetValue(key, out object value) || !(value is Dictionary<string, object> defaults))
                return result;
            foreach (KeyValuePair<string, object> entry in defaults)
            {
                int type = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                if (type == 7 || type == 8) continue;
                result[type] = Convert.ToInt32(entry.Value, CultureInfo.InvariantCulture);
            }
            return result;
        }

        public IReadOnlyDictionary<int, OfflineAppearanceDye> DefaultDyes()
        {
            var result = new Dictionary<int, OfflineAppearanceDye>();
            foreach (Dictionary<string, object> dye in Rows("character_skin_dye"))
            {
                List<int> fixedColors = Integers(dye, "FixedColorIdList").ToList();
                if (fixedColors.Count == 0) continue;
                Dictionary<string, object> color = Rows("character_skin_color").FirstOrDefault(row =>
                    Integer(row, "Id") == fixedColors[0]);
                if (color == null) continue;
                int type = Integer(dye, "DyeType");
                result[type] = new OfflineAppearanceDye
                {
                    Type = type, ColorId = fixedColors[0], Hue = Integer(color, "H"),
                    Saturation = Integer(color, "S"), Value = Integer(color, "V")
                };
            }
            return result;
        }

        public bool IsValidDye(int type, int colorId, int hue, int saturation, int value)
        {
            Dictionary<string, object> dye = Rows("character_skin_dye").FirstOrDefault(row => Integer(row, "DyeType") == type);
            if (dye == null || !(dye.TryGetValue("HSVRangeList", out object rangeData) && rangeData is List<object> range)
                || range.Count != 6) return false;
            bool fixedColor = Integers(dye, "FixedColorIdList").Contains(colorId);
            if (colorId > 0 && !fixedColor) return false;
            int hMin = Convert.ToInt32(range[0], CultureInfo.InvariantCulture);
            int hMax = Convert.ToInt32(range[1], CultureInfo.InvariantCulture);
            int sMin = Convert.ToInt32(range[2], CultureInfo.InvariantCulture);
            int sMax = Convert.ToInt32(range[3], CultureInfo.InvariantCulture);
            int vMin = Convert.ToInt32(range[4], CultureInfo.InvariantCulture);
            int vMax = Convert.ToInt32(range[5], CultureInfo.InvariantCulture);
            return hue >= hMin && hue <= hMax && saturation >= sMin && saturation <= sMax
                && value >= vMin && value <= vMax;
        }

        public IReadOnlyList<long> CumulativeLevelExperience
        {
            get
            {
                if (!levelExperience.TryGetValue(1, out long ignored))
                    throw new FormatException("Official level experience table has no Id 1 row.");
                return levelExperience.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
            }
        }

        public IReadOnlyList<Dictionary<string, object>> Rows(string table)
        {
            return tables.TryGetValue(table, out List<Dictionary<string, object>> result)
                ? result : System.Array.Empty<Dictionary<string, object>>();
        }

        public Dictionary<string, object> Find(string table, int id, string key = "ClassId")
        {
            return Rows(table).FirstOrDefault(row => Integer(row, key) == id);
        }

        public Dictionary<string, object> FindItem(int id)
        {
            return items.TryGetValue(id, out Dictionary<string, object> item) ? item : null;
        }

        public Dictionary<string, object> FindTimedAppearance(int id)
        {
            return timedAppearanceItems.TryGetValue(id, out Dictionary<string, object> item) ? item : null;
        }

        public int PropertyId(string name)
        {
            return propertyIds.TryGetValue(name, out int id) ? id : 0;
        }

        public IReadOnlyList<Dictionary<string, object>> WardrobeRows(string table)
        {
            return wardrobeTables.TryGetValue(table, out List<Dictionary<string, object>> result)
                ? result : System.Array.Empty<Dictionary<string, object>>();
        }

        public long FashionExperience(IEnumerable<OfflineItemStack> inventory, IEnumerable<int> limitedTitleCollection)
        {
            long experience = 0;
            var countedLimitedTitles = new HashSet<int>();
            foreach (OfflineItemStack stack in inventory ?? Enumerable.Empty<OfflineItemStack>())
            {
                if (stack == null || stack.Count <= 0 || !items.TryGetValue(stack.ItemId, out Dictionary<string, object> item)) continue;
                int itemType = Integer(item, "ItemType");
                if (appearanceItemTypes.Contains(itemType) && timedAppearanceItems.ContainsKey(stack.ItemId))
                {
                    if (itemType == 339 && limitedTitleCollection != null && limitedTitleCollection.Contains(stack.ItemId))
                        countedLimitedTitles.Add(stack.ItemId);
                    continue;
                }
                experience = checked(experience + FashionExperienceFor(stack.ItemId, itemType, Integer(item, "Quality")));
            }
            foreach (int itemId in countedLimitedTitles)
                if (items.TryGetValue(itemId, out Dictionary<string, object> item))
                    experience = checked(experience + FashionExperienceFor(itemId, Integer(item, "ItemType"), Integer(item, "Quality")));
            return experience;
        }

        public int AppearanceLevel(long experience)
        {
            IReadOnlyList<long> levels = AppearanceLevelExperienceTable();
            int level = 0;
            for (int index = 0; index < levels.Count; index++)
            {
                if (experience < levels[index]) break;
                level = index;
            }
            return level;
        }

        public int AppearanceLevelMax => Math.Max(0, AppearanceLevelExperienceTable().Count - 1);

        public bool IsMaxAppearanceLevel(int level)
        {
            return level >= AppearanceLevelMax;
        }

        public long AppearanceLevelExperience(int level)
        {
            IReadOnlyList<long> levels = AppearanceLevelExperienceTable();
            return levels[Math.Max(0, Math.Min(levels.Count - 1, level))];
        }

        public long AppearanceLevelNextExperience(int level)
        {
            return AppearanceLevelExperience(level + 1);
        }

        public IReadOnlyList<long> AppearanceLevelExperienceTable()
        {
            Dictionary<string, object> row = Rows("level_exp").FirstOrDefault(value => Integer(value, "Id") == 6);
            if (row == null || !(row.TryGetValue("Exp", out object value) && value is List<object> levels))
                throw new FormatException("Official appearance experience table is missing.");
            return levels.Select(level => Convert.ToInt64(level, CultureInfo.InvariantCulture)).ToArray();
        }

        public IReadOnlyDictionary<int, long> AppearancePropertiesBetween(int fromLevel, int toLevel)
        {
            var result = new Dictionary<int, long>();
            foreach (Dictionary<string, object> row in WardrobeRows("appearance_prop"))
            {
                int level = Integer(row, "Level");
                if (level <= fromLevel || level > toLevel) continue;
                if (!(row.TryGetValue("Attribuate", out object value)
                    && value is Dictionary<string, object> attributes)) continue;
                foreach (KeyValuePair<string, object> attribute in attributes)
                    AddProperty(result, attribute.Key, Convert.ToInt64(attribute.Value, CultureInfo.InvariantCulture));
            }
            return result;
        }

        public IReadOnlyDictionary<int, long> AppearanceProperties(int appearanceLevel,
            IEnumerable<OfflineItemStack> inventory, IReadOnlyDictionary<int, long> expiryTimes, long nowUnixSeconds)
        {
            var result = new Dictionary<int, long>();
            foreach (Dictionary<string, object> row in WardrobeRows("appearance_prop"))
            {
                if (Integer(row, "Level") > appearanceLevel || !(row.TryGetValue("Attribuate", out object value)
                    && value is Dictionary<string, object> attributes)) continue;
                foreach (KeyValuePair<string, object> attribute in attributes)
                    AddProperty(result, attribute.Key, Convert.ToInt64(attribute.Value, CultureInfo.InvariantCulture));
            }
            foreach (OfflineItemStack stack in inventory ?? Enumerable.Empty<OfflineItemStack>())
            {
                if (stack == null || stack.Count <= 0 || ItemType(stack.ItemId) != 339
                    || expiryTimes != null && expiryTimes.TryGetValue(stack.ItemId, out long expiresAt) && nowUnixSeconds > expiresAt
                    || !wardrobeDefinitions.TryGetValue(stack.ItemId, out Dictionary<string, object> definition)
                    || !(definition.TryGetValue("EffectProp", out object value)
                        && value is Dictionary<string, object> attributes)) continue;
                foreach (KeyValuePair<string, object> attribute in attributes)
                    AddProperty(result, attribute.Key, Convert.ToInt64(attribute.Value, CultureInfo.InvariantCulture));
            }
            return result;
        }

        /// <summary>Original <c>AppendAppearanceInfo.EffectProp</c> of an owned title.</summary>
        public IReadOnlyDictionary<int, long> TitleProperties(int itemId)
        {
            var result = new Dictionary<int, long>();
            if (!wardrobeDefinitions.TryGetValue(itemId, out Dictionary<string, object> definition)
                || !(definition.TryGetValue("EffectProp", out object value)
                    && value is Dictionary<string, object> attributes)) return result;
            foreach (KeyValuePair<string, object> attribute in attributes)
                AddProperty(result, attribute.Key, Convert.ToInt64(attribute.Value, CultureInfo.InvariantCulture));
            return result;
        }

        public int ItemType(int itemId)
        {
            return items.TryGetValue(itemId, out Dictionary<string, object> item) ? Integer(item, "ItemType") : 0;
        }

        public int PlayerExperienceItemId
        {
            get
            {
                Dictionary<string, object> row = Rows("game_settings").FirstOrDefault();
                return row == null ? 0 : Integer(row, "PlayerExpItemId");
            }
        }

        public static OfficialCharacterCatalog Parse(string progressionJson, string timedItemsJson,
            string wardrobeJson, string wardrobeRulesJson, string overviewJson)
        {
            Dictionary<string, object> progression = Object(OfficialEquipmentJson.Parse(progressionJson), "character progression");
            if (Text(progression, "manifest") != "854_156383")
                throw new FormatException("Character progression data are not pinned to 854_156383.");
            Dictionary<string, object> rules = Object(progression, "rules");
            Dictionary<string, object> fidelity = Object(progression, "tables");
            Dictionary<string, object> timed = Object(OfficialEquipmentJson.Parse(timedItemsJson), "timed appearances");
            if (Text(timed, "manifest") != "854_156383")
                throw new FormatException("Timed appearance data are not pinned to 854_156383.");
            Dictionary<string, object> wardrobe = Object(OfficialEquipmentJson.Parse(wardrobeJson), "wardrobe items");
            Dictionary<string, object> appearanceRules = Object(OfficialEquipmentJson.Parse(wardrobeRulesJson), "wardrobe rules");
            Dictionary<string, object> overview = Object(OfficialEquipmentJson.Parse(overviewJson), "character overview");
            var tables = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> entry in rules)
                if (entry.Value is List<object> rows)
                    tables[entry.Key] = rows.Select(value => Object(value, entry.Key + " row")).ToList();
                else if (entry.Key == "game_settings" && entry.Value is Dictionary<string, object> setting)
                    tables[entry.Key] = new List<Dictionary<string, object>> { setting };
            foreach (string table in new[] { "system_level_limit", "system_level_gap_limit", "level_prop_default_value" })
                if (fidelity.TryGetValue(table, out object rows) && rows is List<object> values)
                    tables[table] = values.Select(value => Object(value, table + " row")).ToList();
            if (progression.TryGetValue("role_prop_group", out object roleGroups) && roleGroups is List<object> roleRows)
                tables["role_prop_group"] = roleRows.Select(value => Object(value, "role_prop_group row")).ToList();
            var levelProps = new Dictionary<int, Dictionary<int, Dictionary<int, long>>>();
            if (progression.TryGetValue("level_prop", out object levelPropData)
                && levelPropData is Dictionary<string, object> classes)
            {
                foreach (KeyValuePair<string, object> propClass in classes)
                {
                    Dictionary<string, object> classData = Object(propClass.Value, "level_prop class");
                    Dictionary<string, object> sourceRows = Object(classData, "rows");
                    var decodedRows = new Dictionary<int, Dictionary<int, long>>();
                    foreach (KeyValuePair<string, object> sourceRow in sourceRows)
                    {
                        Dictionary<string, object> sourceProps = Object(sourceRow.Value, "level_prop row");
                        var decodedProps = new Dictionary<int, long>();
                        foreach (KeyValuePair<string, object> prop in sourceProps)
                            decodedProps[int.Parse(prop.Key, CultureInfo.InvariantCulture)] = Convert.ToInt64(prop.Value, CultureInfo.InvariantCulture);
                        decodedRows[int.Parse(sourceRow.Key, CultureInfo.InvariantCulture)] = decodedProps;
                    }
                    levelProps[int.Parse(propClass.Key, CultureInfo.InvariantCulture)] = decodedRows;
                }
            }
            var wardrobeTables = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.Ordinal);
            if (appearanceRules.TryGetValue("tables", out object wardrobeTableData)
                && wardrobeTableData is Dictionary<string, object> wardrobeRows)
                foreach (KeyValuePair<string, object> table in wardrobeRows)
                    if (table.Value is List<object> values)
                        wardrobeTables[table.Key] = values.Select(value => Object(value, table.Key + " row")).ToList();

            Dictionary<string, object> propertyRows = Object(overview, "properties");
            var propertyIds = new Dictionary<string, int>(StringComparer.Ordinal);
            if (progression.TryGetValue("property_ids", out object propIds) && propIds is Dictionary<string, object> ids)
            {
                foreach (KeyValuePair<string, object> property in ids)
                    propertyIds[property.Key] = Convert.ToInt32(property.Value, CultureInfo.InvariantCulture);
            }
            var items = new Dictionary<int, Dictionary<string, object>>();
            foreach (Dictionary<string, object> item in Rows("item", tables))
                items[Integer(item, "ClassId")] = item;
            var skins = new Dictionary<int, WardrobeSkinDefinition>();
            foreach (Dictionary<string, object> row in KeyedRows(appearanceRules, "skins"))
            {
                int id = Integer(row, "Id");
                skins[id] = new WardrobeSkinDefinition
                {
                    SkinId = id,
                    Type = Integer(row, "SkinType"),
                    BlockedSkinIds = Integers(row, "blocked_ids").ToArray(),
                    BindingSkinIds = Integers(row, "BindingIdList").ToArray()
                };
            }

            var appearances = new Dictionary<int, WardrobeItemDefinition>();
            var wardrobeDefinitions = new Dictionary<int, Dictionary<string, object>>();
            foreach (Dictionary<string, object> row in KeyedRows(appearanceRules, "items"))
                wardrobeDefinitions[Integer(row, "ClassId")] = row;
            foreach (Dictionary<string, object> row in KeyedRows(wardrobe, "appearances"))
            {
                int id = Integer(row, "ClassId");
                Dictionary<string, object> item = items.TryGetValue(id, out Dictionary<string, object> itemInfo)
                    ? itemInfo : null;
                if (item != null && Integer(item, "ItemType") == 339
                    && row.TryGetValue("SkinDict", out object titleSkinData) && titleSkinData == null)
                    continue;
                var definition = new WardrobeItemDefinition
                {
                    ItemId = id,
                    ItemType = item == null ? 0 : Integer(item, "ItemType")
                };
                foreach (KeyValuePair<string, object> skin in Object(row, "SkinDict"))
                    definition.SkinBySex[Convert.ToInt32(skin.Key, CultureInfo.InvariantCulture)]
                        = Convert.ToInt32(skin.Value, CultureInfo.InvariantCulture);
                appearances[id] = definition;
            }
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> entry in Object(wardrobe, "allowed"))
            {
                string[] identity = entry.Key.Split(':');
                if (identity.Length != 2 || !(entry.Value is List<object> appearanceIds)) continue;
                foreach (object id in appearanceIds) allowed.Add(identity[0] + ":" + identity[1] + ":"
                    + Convert.ToInt32(id, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            }

            var levelExperience = new Dictionary<int, long>();
            foreach (Dictionary<string, object> row in Rows("level_exp", tables))
                if (Integer(row, "Id") == 1 && row.TryGetValue("Exp", out object sequence) && sequence is List<object> values)
                    for (int i = 0; i < values.Count; i++) levelExperience[i] = Convert.ToInt64(values[i], CultureInfo.InvariantCulture);
            var limits = Rows("system_level_limit", tables).Select(row => new CharacterLevelLimitRecord
            {
                SubRank = Integer(row, "SubRank"), PlayerLevel = Integer(row, "PlayerLevel"),
                PlayerBlessLevel = Integer(row, "PlayerBlessLevel")
            }).ToList();
            var bless = Rows("level_bless", tables).Select(row => new CharacterBlessLevelRecord
            {
                Rank = Integer(row, "Rank"), BlessLevel = Integer(row, "BlessLevel"), Exp = Long(row, "Exp")
            }).ToList();
            var timedAppearanceItems = new Dictionary<int, Dictionary<string, object>>();
            foreach (Dictionary<string, object> row in KeyedRows(timed, "rows"))
                timedAppearanceItems[Integer(row, "ClassId")] = row;
            var rankBySubRank = new Dictionary<int, int>();
            foreach (KeyValuePair<string, object> rank in Object(overview, "ranks"))
                rankBySubRank[int.Parse(rank.Key, CultureInfo.InvariantCulture)]
                    = Integer(Object(rank.Value, "rank entry"), "rank");
            var professionNames = new Dictionary<int, string>();
            if (progression.TryGetValue("profession_names", out object nameRows)
                && nameRows is Dictionary<string, object> nameMap)
                foreach (KeyValuePair<string, object> entry in nameMap)
                    professionNames[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                        = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
            var professionIcons = new Dictionary<int, string>();
            if (progression.TryGetValue("profession_icons", out object iconRows2)
                && iconRows2 is Dictionary<string, object> iconMap2)
                foreach (KeyValuePair<string, object> entry in iconMap2)
                    professionIcons[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                        = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
            var itemIcons = new Dictionary<int, string>();
            if (progression.TryGetValue("item_icons", out object iconRows)
                && iconRows is Dictionary<string, object> iconMap)
                foreach (KeyValuePair<string, object> entry in iconMap)
                    itemIcons[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                        = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
            var appearanceItemTypes = new HashSet<int>();
            Dictionary<string, object> settings = Rows("game_settings", tables).FirstOrDefault();
            if (settings != null) appearanceItemTypes.UnionWith(Integers(settings, "AppearanceTypes"));
            if (levelExperience.Count == 0 || limits.Count == 0 || bless.Count == 0
                || propertyRows.Count == 0 || skins.Count == 0 || appearances.Count == 0 || allowed.Count == 0
                || timedAppearanceItems.Count == 0 || rankBySubRank.Count == 0)
                throw new FormatException("Official character catalogs are incomplete.");
            return new OfficialCharacterCatalog(tables, items, skins, appearances, allowed,
                levelExperience, limits, bless, timedAppearanceItems, propertyIds, levelProps, wardrobeTables,
                wardrobeDefinitions, rankBySubRank, Object(appearanceRules, "defaults"), appearanceItemTypes,
                OfficialPropCatalog.Parse(progressionJson, overviewJson), itemIcons, professionNames,
                professionIcons);
        }

        private static List<Dictionary<string, object>> Rows(string table,
            Dictionary<string, List<Dictionary<string, object>>> tables)
        {
            return tables.TryGetValue(table, out List<Dictionary<string, object>> rows)
                ? rows : new List<Dictionary<string, object>>();
        }

        private static void Merge(Dictionary<int, long> target, IReadOnlyDictionary<int, long> source)
        {
            foreach (KeyValuePair<int, long> property in source)
                target[property.Key] = target.TryGetValue(property.Key, out long current)
                    ? checked(current + property.Value) : property.Value;
        }

        private static IEnumerable<Dictionary<string, object>> KeyedRows(Dictionary<string, object> root, string key)
        {
            object source = root.TryGetValue(key, out object value) ? value : null;
            if (source is Dictionary<string, object> keyed)
            {
                foreach (object row in keyed.Values) yield return Object(row, key + " row");
                yield break;
            }
            if (source is List<object> rows)
                foreach (object row in rows)
                {
                    Dictionary<string, object> entry = Object(row, key + " entry");
                    yield return entry.TryGetValue("Value", out object nested)
                        ? Object(nested, key + " value") : entry;
                }
        }

        private static Dictionary<string, object> Object(Dictionary<string, object> root, string key)
        {
            return Object(root.TryGetValue(key, out object value) ? value : null, key);
        }

        private static Dictionary<string, object> Object(object value, string context)
        {
            if (value is Dictionary<string, object> result) return result;
            throw new FormatException(context + " must be an object.");
        }

        private static List<object> Array(Dictionary<string, object> root, string key)
        {
            if (root.TryGetValue(key, out object value) && value is List<object> result) return result;
            throw new FormatException(key + " must be an array.");
        }

        private static IEnumerable<int> Integers(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value) && value is List<object> entries
                ? entries.Select(entry => Convert.ToInt32(entry, CultureInfo.InvariantCulture)) : Enumerable.Empty<int>();
        }

        private static string Text(Dictionary<string, object> value, string key)
        {
            return Convert.ToString(value.TryGetValue(key, out object result) ? result : null, CultureInfo.InvariantCulture);
        }

        private static int Integer(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object result) ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : 0;
        }

        private static long Long(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object result) ? Convert.ToInt64(result, CultureInfo.InvariantCulture) : 0L;
        }

        private long FashionExperienceFor(int itemId, int itemType, int quality)
        {
            if (itemType == 425)
            {
                Dictionary<string, object> skillSkin = Rows("skill_skin").FirstOrDefault(row => Integer(row, "ClassId") == itemId);
                if (skillSkin == null || Integer(skillSkin, "SkinType") == 0) return 0;
            }
            else if (itemType == 428 || itemType == 429 || itemType == 430)
            {
                string table = itemType == 428 ? "furniture_skin" : itemType == 429 ? "home_theme_skin" : "home_environment_skin";
                Dictionary<string, object> skin = WardrobeRows(table).FirstOrDefault(row => Integer(row, "SkinItemId") == itemId);
                if (skin == null || Boolean(skin, "IsDefault")) return 0;
            }
            foreach (Dictionary<string, object> group in WardrobeRows("fashion_exp_group"))
            {
                if (!Integers(group, "FashionItemTypeList").Contains(itemType)
                    || !(group.TryGetValue("FashionQualityList", out object qualities) && qualities is List<object> q)
                    || !(group.TryGetValue("FashionExpList", out object values) && values is List<object> exp)) continue;
                for (int i = 0; i < q.Count && i < exp.Count; i++)
                    if (Convert.ToInt32(q[i], CultureInfo.InvariantCulture) == quality)
                        return Convert.ToInt64(exp[i], CultureInfo.InvariantCulture);
            }
            return 0;
        }

        private static bool Boolean(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) && value is bool result && result;
        }

        private void AddProperty(Dictionary<int, long> target, string propertyName, long value)
        {
            if (!int.TryParse(propertyName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                id = PropertyId(propertyName);
            if (id > 0) target[id] = checked(target.TryGetValue(id, out long before) ? before + value : value);
        }
    }
}
