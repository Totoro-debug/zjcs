using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    /// <summary>
    /// Original <c>CombatRatingPropSort</c> values used when the combat rating splits properties.
    /// </summary>
    public enum OfficialCombatRatingSort
    {
        None = 0,
        Base = 1,
        BasePercent = 2,
        NonBase = 3,
        NonBasePercent = 4,
        Advanced = 5,
        BaseRate = 6
    }

    /// <summary>
    /// Original <c>PropCatalog</c> values of a property row.
    /// </summary>
    public enum OfficialPropCatalogType
    {
        Base = 0,
        Addition = 1,
        Hide = 2,
        SkillCommon = 3,
        SkillAttack = 4,
        SkillCure = 5,
        Status = 6,
        World = 7,
        Home = 8,
        Main = 9,
        Element = 10,
        Origin = 11,
        Factor = 12
    }

    /// <summary>Original <c>prop_cfg</c> row together with its localized display data.</summary>
    public sealed class OfficialPropDefinition
    {
        public int PropType;
        public int SkillPanelShowOrder;
        public bool IsBattleProp;
        public int Catalog;
        public bool SkillHide;
        public bool HideWhenNoValue;
        public bool Float;
        public bool Percent;
        public int RelativeProp;
        public int PercentShowBase;
        public string Unit;
        public int[] ScoreBase;
        public OfficialCombatRatingSort CombatRatingSort;
        public double Score;
        public double PetScore;
        public int ScoreLevelGroupId;
        public string Name;
        public string EntryName;
        public string Description;
        public string IconUri;
    }

    /// <summary>
    /// Read-only view of the pinned <c>prop_cfg</c>, property display names, category names
    /// and combat-rating ordering. Mirrors <c>PropCfgInfoParser</c>.
    /// </summary>
    public sealed class OfficialPropCatalog
    {
        private readonly Dictionary<int, OfficialPropDefinition> definitions;
        private readonly Dictionary<int, IReadOnlyList<OfficialPropDefinition>> byCatalog;
        private readonly Dictionary<OfficialCombatRatingSort, IReadOnlyList<int>> byCombatRatingSort;
        private readonly Dictionary<int, int> relativeOwner;
        private readonly Dictionary<int, string> categories;
        private readonly Dictionary<string, int> propertyIds;
        private readonly Dictionary<int, Dictionary<int, int>> rolePropGroups;
        private readonly int calcCombatPropGroupId;

        private OfficialPropCatalog(Dictionary<int, OfficialPropDefinition> definitions,
            Dictionary<int, IReadOnlyList<OfficialPropDefinition>> byCatalog,
            Dictionary<OfficialCombatRatingSort, IReadOnlyList<int>> byCombatRatingSort,
            Dictionary<int, int> relativeOwner, Dictionary<int, string> categories,
            Dictionary<string, int> propertyIds, Dictionary<int, Dictionary<int, int>> rolePropGroups,
            int calcCombatPropGroupId)
        {
            this.definitions = definitions;
            this.byCatalog = byCatalog;
            this.byCombatRatingSort = byCombatRatingSort;
            this.relativeOwner = relativeOwner;
            this.categories = categories;
            this.propertyIds = propertyIds;
            this.rolePropGroups = rolePropGroups;
            this.calcCombatPropGroupId = calcCombatPropGroupId;
        }

        public int Count => definitions.Count;
        public IReadOnlyDictionary<string, int> PropertyIds => propertyIds;
        public IReadOnlyDictionary<int, string> Categories => categories;
        public int CalcCombatPropGroupId => calcCombatPropGroupId;

        /// <summary>Original <c>RolePropGroupInfoParser</c> lookup used by the rating reference data.</summary>
        public int RolePropGroupLevelPropId(int groupId, int subRank)
        {
            return rolePropGroups.TryGetValue(groupId, out Dictionary<int, int> rows)
                && rows.TryGetValue(subRank, out int levelPropId) ? levelPropId : 0;
        }

        public bool TryGet(int propType, out OfficialPropDefinition definition)
        {
            return definitions.TryGetValue(propType, out definition);
        }

        public OfficialPropDefinition Get(int propType)
        {
            return definitions.TryGetValue(propType, out OfficialPropDefinition definition) ? definition : null;
        }

        public bool IsBattleProp(int propType)
        {
            return definitions.TryGetValue(propType, out OfficialPropDefinition definition) && definition.IsBattleProp;
        }

        public IReadOnlyList<OfficialPropDefinition> ByCatalog(int catalog)
        {
            return byCatalog.TryGetValue(catalog, out IReadOnlyList<OfficialPropDefinition> rows)
                ? rows : Array.Empty<OfficialPropDefinition>();
        }

        public IReadOnlyList<int> ByCombatRatingSort(OfficialCombatRatingSort sort)
        {
            return byCombatRatingSort.TryGetValue(sort, out IReadOnlyList<int> rows)
                ? rows : Array.Empty<int>();
        }

        public bool TryGetMainPropByRelativeProp(int relativeProp, out int mainProp)
        {
            return relativeOwner.TryGetValue(relativeProp, out mainProp);
        }

        public string CategoryName(int category)
        {
            return categories.TryGetValue(category, out string name) ? name : string.Empty;
        }

        public int Id(string propertyName)
        {
            return propertyIds.TryGetValue(propertyName, out int id) ? id : 0;
        }

        public string Unit(int propType)
        {
            return definitions.TryGetValue(propType, out OfficialPropDefinition definition)
                ? definition.Unit ?? string.Empty : string.Empty;
        }

        public static OfficialPropCatalog Parse(string progressionJson, string overviewJson)
        {
            Dictionary<string, object> progression = Object(OfficialEquipmentJson.Parse(progressionJson),
                "character progression");
            if (Text(progression, "manifest") != "854_156383")
                throw new FormatException("Property configuration is not pinned to 854_156383.");
            Dictionary<string, object> rules = Object(progression, "rules");
            if (!(rules.TryGetValue("prop_cfg", out object propRows) && propRows is List<object> rows))
                throw new FormatException("Official prop_cfg table is missing.");
            Dictionary<string, object> overview = Object(OfficialEquipmentJson.Parse(overviewJson),
                "character overview");
            Dictionary<string, object> displays = Object(overview, "properties");
            Dictionary<string, object> units = progression.TryGetValue("prop_units", out object unitData)
                && unitData is Dictionary<string, object> unitMap ? unitMap : new Dictionary<string, object>();
            Dictionary<string, object> categoryData = progression.TryGetValue("prop_categories",
                out object categoryRows) && categoryRows is Dictionary<string, object> categoryMap
                ? categoryMap : new Dictionary<string, object>();
            var propertyIds = new Dictionary<string, int>(StringComparer.Ordinal);
            if (progression.TryGetValue("property_ids", out object ids) && ids is Dictionary<string, object> idMap)
                foreach (KeyValuePair<string, object> entry in idMap)
                    propertyIds[entry.Key] = Convert.ToInt32(entry.Value, CultureInfo.InvariantCulture);

            var definitions = new Dictionary<int, OfficialPropDefinition>();
            foreach (object row in rows)
            {
                Dictionary<string, object> source = Object(row, "prop_cfg row");
                int propType = Integer(source, "PropType");
                if (propType == 0 || definitions.ContainsKey(propType))
                    throw new FormatException("Official prop_cfg row is invalid: " + propType);
                string unit = Text(source, "Unit");
                var definition = new OfficialPropDefinition
                {
                    PropType = propType,
                    SkillPanelShowOrder = Integer(source, "SkillPanelShowOrder"),
                    IsBattleProp = Boolean(source, "IsBattleProp"),
                    Catalog = Integer(source, "Catalog"),
                    SkillHide = Boolean(source, "SkillHide"),
                    HideWhenNoValue = Boolean(source, "HideWhenNoValue"),
                    Float = Boolean(source, "Float"),
                    Percent = Boolean(source, "Percent"),
                    RelativeProp = Integer(source, "RelativeProp"),
                    PercentShowBase = Integer(source, "PercentShowBase"),
                    Unit = !string.IsNullOrEmpty(unit) && units.TryGetValue(unit, out object localized)
                        ? Convert.ToString(localized, CultureInfo.InvariantCulture) : unit,
                    ScoreBase = Integers(source, "ScoreBase"),
                    CombatRatingSort = (OfficialCombatRatingSort)Integer(source, "CombatRatingSort"),
                    Score = Number(source, "Score"),
                    PetScore = Number(source, "PetScore"),
                    ScoreLevelGroupId = Integer(source, "ScoreLevelGroupId")
                };
                if (displays.TryGetValue(propType.ToString(CultureInfo.InvariantCulture),
                        out object display) && display is Dictionary<string, object> displayRow)
                {
                    definition.Name = Text(displayRow, "name");
                    definition.EntryName = Text(displayRow, "entry_name");
                    definition.Description = Text(displayRow, "description");
                    if (displayRow.TryGetValue("icon", out object icon) && icon is Dictionary<string, object> iconRow)
                        definition.IconUri = Text(iconRow, "file");
                }
                if (string.IsNullOrEmpty(definition.Name))
                    definition.Name = ReverseName(propertyIds, propType);
                definitions[propType] = definition;
            }

            var byCatalog = new Dictionary<int, IReadOnlyList<OfficialPropDefinition>>();
            foreach (IGrouping<int, OfficialPropDefinition> group in definitions.Values.GroupBy(value => value.Catalog))
                byCatalog[group.Key] = group.OrderBy(value => value.PropType).ToArray();
            var bySort = new Dictionary<OfficialCombatRatingSort, IReadOnlyList<int>>();
            foreach (IGrouping<OfficialCombatRatingSort, OfficialPropDefinition> group in definitions.Values
                         .Where(value => value.CombatRatingSort != OfficialCombatRatingSort.None)
                         .GroupBy(value => value.CombatRatingSort))
                bySort[group.Key] = group.Select(value => value.PropType).OrderBy(value => value).ToArray();
            var relativeOwner = new Dictionary<int, int>();
            foreach (OfficialPropDefinition definition in definitions.Values)
                if (definition.RelativeProp != 0 && !relativeOwner.ContainsKey(definition.RelativeProp))
                    relativeOwner.Add(definition.RelativeProp, definition.PropType);
            var categories = new Dictionary<int, string>();
            foreach (KeyValuePair<string, object> entry in categoryData)
                categories[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                    = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
            var roleGroups = new Dictionary<int, Dictionary<int, int>>();
            if (progression.TryGetValue("role_prop_group", out object groupRows)
                && groupRows is List<object> groupList)
                foreach (object value in groupList)
                {
                    Dictionary<string, object> row = Object(value, "role_prop_group row");
                    int id = Integer(row, "ID");
                    if (!roleGroups.TryGetValue(id, out Dictionary<int, int> byRank))
                        roleGroups[id] = byRank = new Dictionary<int, int>();
                    byRank[Integer(row, "SubRank")] = Integer(row, "LevelPropID");
                }
            int calcGroupId = 0;
            if (rules.TryGetValue("game_settings", out object settingData)
                && settingData is Dictionary<string, object> settings)
                calcGroupId = Integer(settings, "CalcCombatPropGroupId");
            if (definitions.Count == 0 || byCatalog.Count == 0 || categories.Count == 0
                || roleGroups.Count == 0 || calcGroupId == 0)
                throw new FormatException("Official property configuration is incomplete.");
            return new OfficialPropCatalog(definitions, byCatalog, bySort, relativeOwner, categories,
                propertyIds, roleGroups, calcGroupId);
        }

        private static string ReverseName(Dictionary<string, int> propertyIds, int propType)
        {
            foreach (KeyValuePair<string, int> entry in propertyIds)
                if (entry.Value == propType) return entry.Key;
            return propType.ToString(CultureInfo.InvariantCulture);
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

        private static string Text(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static bool Boolean(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value) && value is bool result && result;
        }

        private static double Number(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value)
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 0d;
        }

        private static int[] Integers(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value) && value is List<object> values
                ? values.Select(entry => Convert.ToInt32(entry, CultureInfo.InvariantCulture)).ToArray()
                : Array.Empty<int>();
        }
    }
}
