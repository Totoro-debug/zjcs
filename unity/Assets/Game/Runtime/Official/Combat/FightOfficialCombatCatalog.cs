using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Zjcs.Combat.Official
{
    public sealed class FightOfficialCombatSourceManifest
    {
        public string Manifest;
        public string OutputSha256;
    }

    public sealed class FightOfficialCombatCatalog
    {
        public const string ExpectedManifest = "854_156383";

        private readonly Dictionary<int, Dictionary<string, object>> entities =
            new Dictionary<int, Dictionary<string, object>>();
        private Dictionary<string, object> skillPropertyNames;
        private Dictionary<string, object> battleTables;
        private Dictionary<string, object> battleProperties;
        private readonly Dictionary<int, List<int>> actionBlockersByProposedType =
            new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<MonsterHpPliesLevel>> monsterHpPlies =
            new Dictionary<int, List<MonsterHpPliesLevel>>();

        private sealed class MonsterHpPliesLevel
        {
            public int LowLevel;
            public int Plies;
        }

        private FightOfficialCombatCatalog() { }

        public static FightOfficialCombatSourceManifest ReadSourceManifest(string json)
        {
            Dictionary<string, object> manifest = Object(OfficialJson.Parse(json), "source manifest");
            return new FightOfficialCombatSourceManifest
            {
                Manifest = Convert.ToString(Required(manifest, "manifest"), CultureInfo.InvariantCulture),
                OutputSha256 = Convert.ToString(Required(manifest, "output_sha256"), CultureInfo.InvariantCulture)
            };
        }

        public static FightOfficialCombatCatalog Load(string skillRulesJson, string skillRulesSha256,
            string skillManifest, string battleRulesJson, string battleRulesSha256, string battleManifest)
        {
            return Load(skillRulesJson, skillRulesSha256, skillManifest, battleRulesJson,
                battleRulesSha256, battleManifest, null, null, null);
        }

        public static FightOfficialCombatCatalog Load(string skillRulesJson, string skillRulesSha256,
            string skillManifest, string battleRulesJson, string battleRulesSha256, string battleManifest,
            string monsterHpPliesJson, string monsterHpPliesSha256, string monsterHpPliesManifest)
        {
            VerifyPayload("skill-rules.json", skillRulesJson, skillRulesSha256, skillManifest);
            VerifyPayload("battle-rules.json", battleRulesJson, battleRulesSha256, battleManifest);
            bool hasHpPliesPayload = !string.IsNullOrEmpty(monsterHpPliesJson);
            if (hasHpPliesPayload)
                VerifyPayload("monster-hp-plies.json", monsterHpPliesJson,
                    monsterHpPliesSha256, monsterHpPliesManifest);
            else if (!string.IsNullOrEmpty(monsterHpPliesSha256)
                || !string.IsNullOrEmpty(monsterHpPliesManifest))
                throw new ArgumentException("Monster HP plies payload metadata is incomplete.");
            var catalog = new FightOfficialCombatCatalog();
            Dictionary<string, object> skillCatalog = Object(OfficialJson.Parse(skillRulesJson), "skill-rules.json");
            Dictionary<string, object> battleCatalog = Object(OfficialJson.Parse(battleRulesJson), "battle-rules.json");
            catalog.AddEntities(skillCatalog);
            catalog.AddEntities(battleCatalog);
            catalog.skillPropertyNames = Object(Required(skillCatalog, "prop_names"), "prop_names");
            catalog.battleTables = Object(Required(battleCatalog, "tables"), "battle tables");
            catalog.battleProperties = Object(Required(battleCatalog, "props"), "battle props");
            catalog.ParseStateMutex();
            catalog.ParseSkillPropertyTables(skillCatalog);
            if (hasHpPliesPayload) catalog.ParseMonsterHpPlies(monsterHpPliesJson);
            return catalog;
        }

        /// <summary>
        /// Computes the official skill property snapshot for an EC skill entity:
        /// level-prop row (from the entity's prop group level for the battle sub-rank) scaled by the
        /// rank-prop row, then by the entity's fixed prop factors. This mirrors
        /// <c>BattleFormulaHandler.CalcSkillProps</c>; PVP-only scaling is deliberately absent.
        /// </summary>
        public IReadOnlyDictionary<int, long> CalculateSkillProps(int entityId, int rank, int level,
            int subRank)
        {
            if (!skillEntityProps.TryGetValue(entityId, out FightOfficialEntityProp prop))
                return EmptyProps;
            IReadOnlyDictionary<int, long> levelProps = EmptyProps;
            if (prop.GroupLevelPropId > 0 && skillGroupLevels.TryGetValue(prop.GroupLevelPropId,
                    out Dictionary<int, int> bySubRank)
                && bySubRank.TryGetValue(subRank, out int levelPropId) && levelPropId > 0)
                levelProps = PropRows(levelPropId, level);
            IReadOnlyDictionary<int, long> result = prop.RankPropId > 0
                ? ScaleProps(levelProps, PropRows(prop.RankPropId, rank), false)
                : new Dictionary<int, long>(levelProps);
            return ScaleProps(result, prop.PropFactors, true);
        }

        /// <summary>
        /// Official skill cooldown (<c>BattleFormula.SkillCD</c>) over the merged source and skill
        /// properties. Negative values are clamped to zero like every other cooldown consumer.
        /// </summary>
        public long CalculateSkillCooldown(int entityId, int rank, int level, int subRank,
            IReadOnlyDictionary<int, long> sourceProps)
        {
            if (cooldownPropertyId <= 0) return 0;
            long cooldown = sourceProps != null && sourceProps.TryGetValue(cooldownPropertyId, out long source)
                ? source : 0;
            IReadOnlyDictionary<int, long> skillProps = CalculateSkillProps(entityId, rank, level, subRank);
            if (skillProps.TryGetValue(cooldownPropertyId, out long value)) cooldown += value;
            return cooldown < 0 ? 0 : cooldown;
        }

        public int CooldownPropertyId => cooldownPropertyId;
        public bool ContainsEntity(int entityId) => entities.ContainsKey(entityId);

        public IReadOnlyDictionary<int, long> CalculateMonsterProps(int monsterId, int groupNo,
            int level, int subRank)
        {
            Dictionary<string, object> monsterGroup = FindMonsterGroup(monsterId, groupNo);
            if (monsterGroup == null)
                throw new InvalidOperationException("Official monster group is missing: "
                    + monsterId + "/" + groupNo);
            Dictionary<string, object> propConfig = Object(Required(monsterGroup, "PropCfg"),
                "monster PropCfg");
            int propGroupId = Int(propConfig, "PropGroupId");
            Dictionary<string, object> roleGroup = FindRolePropGroup(propGroupId, subRank);
            if (roleGroup == null)
                throw new InvalidOperationException("Official monster role prop group is missing: "
                    + propGroupId + "/" + subRank);

            var properties = ReadMonsterLevelProps(Int(roleGroup, "LevelPropID"), level);
            int extraLevelPropId = Int(roleGroup, "ExtraLevelPropID");
            if (extraLevelPropId > 0)
                AddProperties(properties, ReadMonsterLevelProps(extraLevelPropId, level));

            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
                Increment(properties, "Base" + name, "Base" + name + "Rate");
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int percentId = PropertyId("Base" + name + "Percent");
                long rate = Value(properties, percentId);
                if (percentId > 0 && rate != 0)
                {
                    Increment(properties, name, "Base" + name + "Percent",
                        Value(properties, PropertyId("Base" + name)));
                    properties.Remove(percentId);
                }
            }
            foreach (string name in new[] { "ElementMaster", "ElementResistance", "KongFuMaster",
                "KongFuResistance", "EffectRate", "EffectDodge" })
                Increment(properties, name, name + "Percent");
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int baseId = PropertyId("Base" + name);
                long baseValue = Value(properties, baseId);
                if (baseValue == 0) continue;
                properties.Remove(baseId);
                AddProperty(properties, PropertyId(name), baseValue);
            }
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed", "EffectDodge" })
            {
                int scaleId = PropertyId(name + "Scale");
                long scale = Value(properties, scaleId);
                if (scaleId <= 0 || scale == 0) continue;
                long baseValue = Value(properties, PropertyId("Base" + name));
                long current = Value(properties, PropertyId(name));
                properties[PropertyId(name)] = Math.Max(1L,
                    current + (long)((current + baseValue) * (scale / 10000.0)));
                properties.Remove(scaleId);
            }

            Dictionary<string, object> factors = Object(Required(propConfig, "PropFactors"),
                "monster PropFactors");
            foreach (int propertyId in new List<int>(properties.Keys))
            {
                if (!factors.TryGetValue(propertyId.ToString(CultureInfo.InvariantCulture),
                    out object rawFactor))
                {
                    properties.Remove(propertyId);
                    continue;
                }
                properties[propertyId] = (long)(properties[propertyId]
                    * (Convert.ToDouble(rawFactor, CultureInfo.InvariantCulture) / 10000.0));
            }
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int id = PropertyId(name);
                if (id > 0 && properties.TryGetValue(id, out long propertyValue))
                    properties[id] = Math.Max(1L, propertyValue);
            }
            foreach (KeyValuePair<int, long> extra in ReadLongMap(monsterGroup, "ExtraProps"))
                AddProperty(properties, extra.Key, extra.Value);
            return properties;
        }

        public IReadOnlyDictionary<int, long> CalculateNpcProps(int propGroupId,
            IReadOnlyDictionary<int, long> propFactors, IReadOnlyDictionary<int, long> extraProps,
            int level, int subRank)
        {
            if (propGroupId <= 0 || level <= 0 || propFactors == null)
                throw new ArgumentOutOfRangeException(nameof(propGroupId), "Official NPC property inputs are invalid.");
            Dictionary<string, object> roleGroup = FindRolePropGroup(propGroupId, subRank);
            if (roleGroup == null)
                throw new InvalidOperationException("Official NPC role prop group is missing: "
                    + propGroupId + "/" + subRank);

            var properties = ReadMonsterLevelProps(Int(roleGroup, "LevelPropID"), level);
            int extraLevelPropId = Int(roleGroup, "ExtraLevelPropID");
            if (extraLevelPropId > 0)
                AddProperties(properties, ReadMonsterLevelProps(extraLevelPropId, level));

            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
                Increment(properties, "Base" + name, "Base" + name + "Rate");
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int percentId = PropertyId("Base" + name + "Percent");
                long rate = Value(properties, percentId);
                if (percentId > 0 && rate != 0)
                {
                    Increment(properties, name, "Base" + name + "Percent",
                        Value(properties, PropertyId("Base" + name)));
                    properties.Remove(percentId);
                }
            }
            foreach (string name in new[] { "ElementMaster", "ElementResistance", "KongFuMaster",
                "KongFuResistance", "EffectRate", "EffectDodge" })
                Increment(properties, name, name + "Percent");
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int baseId = PropertyId("Base" + name);
                long baseValue = Value(properties, baseId);
                if (baseValue == 0) continue;
                properties.Remove(baseId);
                AddProperty(properties, PropertyId(name), baseValue);
            }
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed", "EffectDodge" })
            {
                int scaleId = PropertyId(name + "Scale");
                long scale = Value(properties, scaleId);
                if (scaleId <= 0 || scale == 0) continue;
                long baseValue = Value(properties, PropertyId("Base" + name));
                long current = Value(properties, PropertyId(name));
                properties[PropertyId(name)] = Math.Max(1L,
                    current + (long)((current + baseValue) * (scale / 10000.0)));
                properties.Remove(scaleId);
            }

            foreach (int propertyId in new List<int>(properties.Keys))
            {
                if (!propFactors.TryGetValue(propertyId, out long factor))
                {
                    properties.Remove(propertyId);
                    continue;
                }
                properties[propertyId] = (long)(properties[propertyId] * (factor / 10000.0));
            }
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int id = PropertyId(name);
                if (id > 0 && properties.TryGetValue(id, out long propertyValue))
                    properties[id] = Math.Max(1L, propertyValue);
            }
            AddProperties(properties, extraProps);
            return properties;
        }

        public int MonsterType(int monsterId)
        {
            if (!battleTables.TryGetValue("monster", out object rawRows)
                || !(rawRows is List<object> rows)) return 0;
            foreach (object item in rows)
            {
                Dictionary<string, object> row = Object(item, "monster row");
                if (Int(row, "Id") == monsterId) return Int(row, "MonsterType");
            }
            return 0;
        }

        public int MonsterHpPliesFor(int monsterId, int groupNo, int level)
        {
            Dictionary<string, object> group = FindMonsterGroup(monsterId, groupNo);
            return group == null ? 1 : MonsterHpPliesForId(Int(group, "HpPliesId"), level);
        }

        public int MonsterHpPliesForId(int hpPliesId, int level)
        {
            if (hpPliesId <= 0 || !monsterHpPlies.TryGetValue(hpPliesId,
                    out List<MonsterHpPliesLevel> levels)) return 1;
            MonsterHpPliesLevel selected = null;
            foreach (MonsterHpPliesLevel row in levels)
                if (row.LowLevel <= level && (selected == null || row.LowLevel > selected.LowLevel))
                    selected = row;
            return selected == null ? 1 : selected.Plies;
        }

        public int MonsterTemplateId(int monsterId)
        {
            Dictionary<string, object> row = FindMonster(monsterId);
            return row == null ? 0 : Int(row, "TempEntityId");
        }

        public int PropertyType(string name) => PropertyId(name);

        public int CalculateMoveDist(IReadOnlyDictionary<int, long> properties)
        {
            double distance = Value(properties, PropertyId("MoveDist"));
            double percent = Value(properties, PropertyId("MoveDistPercent")) / 10000.0;
            return (int)((1.0 + percent) * distance);
        }

        public int MonsterCooldownChangeAtStart(int monsterId)
        {
            Dictionary<string, object> row = FindMonster(monsterId);
            return row == null ? 0 : Int(row, "CDChangeAtStart");
        }

        public int MonsterCharacterType(int monsterId)
        {
            if (!battleTables.TryGetValue("monster", out object rawRows)
                || !(rawRows is List<object> rows)) return 0;
            foreach (object item in rows)
            {
                Dictionary<string, object> row = Object(item, "monster row");
                if (Int(row, "Id") == monsterId) return Int(row, "CharacterType");
            }
            return 0;
        }

        public int MonsterAiType(int monsterId)
        {
            Dictionary<string, object> row = FindMonster(monsterId);
            return row == null ? -1 : Int(row, "AiType", -1);
        }

        public bool HasBackToLifeComponent(int statusClassId)
        {
            return Component(RequiredEntity(statusClassId),
                "FightStatusBackToLifeComponent") != null;
        }

        public int StatusTypeFor(int statusClassId)
        {
            if (!entities.TryGetValue(statusClassId, out Dictionary<string, object> components)) return 0;
            Dictionary<string, object> status = Component(components, "FightStatusComponent");
            return status == null ? 0 : Int(status, "StatusType");
        }

        public FightBattleResilienceInfo MonsterResilienceInfo(int monsterId, int groupNo)
        {
            Dictionary<string, object> group = FindMonsterGroup(monsterId, groupNo);
            if (group == null || !group.TryGetValue("ResilienceInfo", out object rawInfo)
                || rawInfo == null) return null;
            Dictionary<string, object> info = Object(rawInfo, "monster resilience info");
            return new FightBattleResilienceInfo
            {
                DisappearStatusIds = new List<int>(IntArray(info, "DisappearStatusIds")),
                DisappearSkillId = Int(info, "DisappearSkillId"),
                SkillMaxHpPercent = Int(info, "SkillMaxHpPercent"),
                SkillAtkPercent = Int(info, "SkillAtkPercent"),
                ResetRound = Int(info, "ResetRound")
            };
        }

        public bool StatusStopsSkillCooldown(int statusClassId)
        {
            return statusClassId > 0
                && entities.TryGetValue(statusClassId,
                    out Dictionary<string, object> components)
                && Component(components, "FightStatusSkillStopCdComponent") != null;
        }

        public List<int> MonsterActiveSkillItems(int monsterId, int groupNo)
        {
            Dictionary<string, object> group = FindMonsterGroup(monsterId, groupNo);
            return group == null ? new List<int>()
                : new List<int>(IntArray(group, "ActiveSkills"));
        }

        public List<int> MonsterPassiveSkillItems(int monsterId, int groupNo)
        {
            Dictionary<string, object> group = FindMonsterGroup(monsterId, groupNo);
            return group == null ? new List<int>()
                : new List<int>(IntArray(group, "PassiveSkills"));
        }

        public List<int> MonsterDeactivePassiveSkillItems(int monsterId, int groupNo)
        {
            Dictionary<string, object> group = FindMonsterGroup(monsterId, groupNo);
            return group == null ? new List<int>()
                : new List<int>(IntArray(group, "DeactivePassiveSkills"));
        }

        public List<int> MonsterInitialStatusIds(int monsterId)
        {
            var result = new List<int>();
            Dictionary<string, object> row = FindMonster(monsterId);
            int templateId = row == null ? 0 : Int(row, "TempEntityId");
            if (templateId <= 0 || !entities.TryGetValue(templateId,
                    out Dictionary<string, object> components)) return result;
            Dictionary<string, object> status = Component(components, "FightStatusAgentComponent");
            if (status != null) result.AddRange(IntArray(status, "InitStatusEntityClassIds"));
            return result;
        }

        public List<FightGridPoint> MonsterBodyOffsets(int monsterId)
        {
            var result = new List<FightGridPoint>();
            if (!battleTables.TryGetValue("monster", out object rawRows)
                || !(rawRows is List<object> rows)) return result;
            int templateId = 0;
            foreach (object item in rows)
            {
                Dictionary<string, object> row = Object(item, "monster row");
                if (Int(row, "Id") == monsterId) { templateId = Int(row, "TempEntityId"); break; }
            }
            if (templateId <= 0 || !entities.TryGetValue(templateId,
                    out Dictionary<string, object> components)) return result;
            Dictionary<string, object> grid = Component(components, "GridTransformComponent");
            if (grid == null || !grid.TryGetValue("BodyRangeInfos", out object rawBody)
                || rawBody == null) return result;
            foreach (object raw in Array(rawBody, "GridTransformComponent.BodyRangeInfos"))
            {
                Dictionary<string, object> body = Object(raw, "body range info");
                Dictionary<string, object> position = Component(body, "Pos");
                if (position != null)
                    result.Add(new FightGridPoint(Int(position, "X"), Int(position, "Y")));
            }
            return result;
        }

        private Dictionary<string, object> FindMonster(int monsterId)
        {
            if (!battleTables.TryGetValue("monster", out object rawRows)
                || !(rawRows is List<object> rows)) return null;
            foreach (object item in rows)
            {
                Dictionary<string, object> row = Object(item, "monster row");
                if (Int(row, "Id") == monsterId) return row;
            }
            return null;
        }

        /// <summary>
        /// Official battle speed table, populated together with the damage formula from
        /// <c>rules.json → game_settings.Battle</c>.
        /// </summary>
        public FightBattleSpeedSettings SpeedSettings { get; private set; }

        private static FightBattleSpeedSettings ReadSpeedSettings(Dictionary<string, object> battle)
        {
            var settings = new FightBattleSpeedSettings();
            foreach (object value in Array(Required(battle, "Speeds"), "Battle.Speeds"))
                settings.Speeds.Add(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            foreach (object value in Array(Required(battle, "SpeedUIs"), "Battle.SpeedUIs"))
                settings.Labels.Add(Convert.ToString(value, CultureInfo.InvariantCulture));
            foreach (object item in Array(Required(battle, "PlayModeSpeeds"), "Battle.PlayModeSpeeds"))
            {
                Dictionary<string, object> mode = Object(item, "Battle.PlayModeSpeeds entry");
                var parsed = new FightBattleSpeedMode { Type = Int(mode, "Type") };
                foreach (object entryItem in Array(Required(mode, "Speeds"), "Battle.PlayModeSpeeds speeds"))
                {
                    Dictionary<string, object> entry = Object(entryItem, "Battle.PlayModeSpeeds speed");
                    var speed = new FightBattleSpeedEntry { Index = Int(entry, "SpeedIndex") };
                    speed.Conditions.AddRange(IntArray(entry, "Conditions"));
                    parsed.Speeds.Add(speed);
                }
                settings.Modes.Add(parsed);
            }
            settings.RecordPlaybackDefaultIndex = Int(battle, "RecordPlaybackSpeedDefaultIndex");
            if (!settings.IsValid)
                throw new FormatException("Official battle speed configuration is incomplete.");
            return settings;
        }

        /// <summary>
        /// Passive skill properties (<c>BattleFormula.CalcSkillPassiveProps</c>) from the skill item
        /// row's passive rank/level/factor configuration. The result is merged into the owner's
        /// battle properties before MaxHp, speed and move distance are derived.
        /// </summary>
        public IReadOnlyDictionary<int, long> CalculatePassiveSkillProps(int rankPropId,
            int groupLevelPropId, int rankFactorId, IReadOnlyDictionary<int, long> propFactors,
            int rank, int level, int subRank)
        {
            int levelPropId = 0;
            if (groupLevelPropId > 0 && skillGroupLevels.TryGetValue(groupLevelPropId,
                    out Dictionary<int, int> bySubRank))
                bySubRank.TryGetValue(subRank, out levelPropId);
            IReadOnlyDictionary<int, long> levelProps = levelPropId > 0
                ? PropRows(levelPropId, level) : EmptyProps;
            IReadOnlyDictionary<int, long> result = rankPropId > 0
                ? ScaleProps(levelProps, PropRows(rankPropId, rank), false)
                : new Dictionary<int, long>(levelProps);
            var scaled = ScaleProps(result, propFactors ?? EmptyProps, true);
            if (rankFactorId <= 0) return scaled;
            IReadOnlyDictionary<int, long> rankFactor = PropRows(rankFactorId, rank);
            foreach (int propertyId in new List<int>(scaled.Keys))
                if (rankFactor.TryGetValue(propertyId, out long factor))
                    scaled[propertyId] = (long)(scaled[propertyId] * (factor / 10000.0));
            return scaled;
        }

        public FightDamageFormula CreateDamageFormula(string rulesJson, string rulesSha256, string rulesManifest)
        {
            VerifyPayload("rules.json", rulesJson, rulesSha256, rulesManifest);
            Dictionary<string, object> rules = Object(OfficialJson.Parse(rulesJson), "rules.json");
            var schema = new FightDamagePropertySchema();
            foreach (KeyValuePair<string, object> property in skillPropertyNames)
                schema.Names.Add(new FightPropertyName
                {
                    Id = IntKey(property.Key, "property ID"),
                    Name = Convert.ToString(property.Value, CultureInfo.InvariantCulture)
                });
            foreach (object item in Array(Required(rules, "prop_cfg"), "prop_cfg"))
            {
                Dictionary<string, object> property = Object(item, "property config");
                if (Bool(property, "Float")) schema.FloatPropertyIds.Add(Int(property, "PropType"));
            }

            Dictionary<string, object> gameSettings = Object(Required(rules, "game_settings"), "game_settings");
            Dictionary<string, object> battleSettings = Object(Required(gameSettings, "Battle"), "Battle settings");
            SpeedSettings = ReadSpeedSettings(battleSettings);
            var config = new FightDamageFormulaConfig
            {
                FixedDamageLimitPercent = Int(battleSettings, "FixedDmgLimitPercent"),
                MinCritPowerPercent = Number(battleSettings, "MinCritPowerPercent", 0.0),
                MinBlockValuePercent = Number(battleSettings, "MinBlockValuePercent", 0.0),
                MaxLevelOffsetDamageScale = Int(battleSettings, "MaxLevelOffsetDamageScale"),
                CureAddSoftCapThreshold = Number(battleSettings, "CureAddSoftCapThreshold", 0.0) / 10000.0,
                CureAddOverflowScale = Number(battleSettings, "CureAddOverflowScale", 0.0) / 10000.0
            };
            AddTable(config.LevelOffsetDamage, battleTables, "fight_level_offset_damage", "LevelOffset", "DamageScale");
            AddTable(config.RankOffsetDamage, battleTables, "fight_rank_offset_damage", "Rank", "DamageScale");
            AddTable(config.DemonRankOffsetDamage, battleTables, "demon_battlefield_fight_rank_offset_damage", "Rank", "DamageScale");
            AddTable(config.MonsterSpecialProps, battleTables, "monster_special_prop_type", "MonsterId", "DmgAdd");
            return new FightDamageFormula(schema, config);
        }

        public FightBattleSkillDefinition CreateSkill(int entityId, int slot, int itemId,
            int rank, int level, int subRank, IList<FightPropertyValue> properties)
        {
            Dictionary<string, object> components = RequiredEntity(entityId);
            Dictionary<string, object> skill = Component(components, "FightSkillComponent");
            if (skill == null) throw new InvalidOperationException("Official entity is not a skill: " + entityId);
            Dictionary<string, object> ai = Component(skill, "Ai");
            var range = new List<FightGridPoint>();
            foreach (object value in Array(Required(skill, "Range"), "FightSkillComponent.Range"))
            {
                Dictionary<string, object> point = Object(value, "FightSkillComponent.Range point");
                range.Add(new FightGridPoint(Int(point, "X"), Int(point, "Y")));
            }

            var result = new FightBattleSkillDefinition
            {
                EntityId = entityId,
                RootEntityId = entityId,
                Slot = slot,
                ItemId = itemId,
                Rank = rank,
                Level = level,
                SubRank = subRank,
                SkillType = Int(skill, "SkillType"),
                ActionType = Int(skill, "ActionType"),
                HitTargetActionType = Int(skill, "HitTargetActionType"),
                BlockingSourceActionTypes = BlockingStatusActionTypes(Int(skill, "ActionType")),
                BlockingTargetActionTypes = BlockingStatusActionTypes(
                    Int(skill, "HitTargetActionType")),
                SkillTargetType = Int(skill, "SkillTargetType"),
                ElementType = Int(skill, "ElementType"),
                NeedConditionStatusMeetToRelease = Bool(skill,
                    "NeedConditionStatusMeetToRelease"),
                LimitedTimes = Int(skill, "LimitedTimes", -1),
                ResetCooldownAtStart = Bool(skill, "ResetCDAtStart"),
                CostType = Int(skill, "CostType"),
                MustHaveValidCost = Bool(skill, "MustHaveValidCost"),
                TargetType = Int(skill, "TargetType", 1),
                Range = range,
                CanTriggerChild = Bool(skill, "CanTriggerChild", true),
                CanTriggerStatusEntityIds = new List<int>(IntArray(skill, "CanTriggerStatusEntityIds")),
                ResetReleasedTimeOnNextWave = Bool(skill, "ResetReleasedTimeOnNextWave"),
                HasCustomDamage = false,
                RangeVerified = true,
                PriorityProperty = ai == null ? 0 : Int(ai, "AiPriorityPropType"),
                PriorityFindMax = ai == null || Bool(ai, "AiPriorityFindMax", true),
                PriorityCharacterType = ai == null ? -1 : Int(ai, "AIPriroityCharacterType", -1),
                ReleaseWhileRidicule = ai != null && Bool(ai, "ReleaseWhileRidicule"),
                ReleaseWhileConfusion = ai != null && Bool(ai, "ReleaseWhileConfusion"),
                IgnoreStatusIds = ai == null ? new List<int>()
                    : new List<int>(IntArray(ai, "IgnoreStatusArray")),
                HpLowerCharacterTypeFilter = ai == null ? new List<int>()
                    : new List<int>(IntArray(ai, "AIPriorityHpLowerCharacterTypeFilter")),
                PriorityCharacterTypeGroups = ai == null ? new List<List<int>>()
                    : IntGroups(ai, "AIPriorityCharacterTypeGroups"),
                Properties = CopyProperties(properties),
                ActionDuration = Float(skill, "ActionDuration", -1f),
                AllEndedActionDuration = Float(skill, "AllEndedActionDuration", -1f),
                AllRelevantEndedActionDuration = Float(skill, "AllRelevantEndedActionDuration", -1f),
                Duration = Float(skill, "Duration", -1f),
                AllEndedDuration = Float(skill, "AllEndedDuration", -1f),
                AllRelevantEndedDuration = Float(skill, "AllRelevantEndedDuration", -1f),
                HitGraphEntities = new List<FightBattleHitEntity>()
            };

            AddSourceMoves(skill, result.SourceMoves);
            Dictionary<string, object> extSkill = Component(components, "FightSkillExtSkillComponent");
            if (extSkill != null) AddExtraSkills(extSkill, result.ExtraSkills);
            Dictionary<string, object> replay = Component(components, "FightSkillReplayComponent");
            if (replay != null) AddSkillReplay(replay, result);
            var pending = new Queue<int>();
            var visited = new HashSet<int>();
            var references = new Dictionary<int, FightBattleHitReference>();
            AddHitChildren(skill, pending, result.HitGraphRoots, result.HitGraphRootEntityIds, references);
            while (pending.Count > 0)
            {
                int hitId = pending.Dequeue();
                if (hitId <= 0 || !visited.Add(hitId)) continue;
                Dictionary<string, object> hitComponents = RequiredEntity(hitId);
                FightBattleHitEntity hit = CreateHitEntity(hitId, hitComponents, pending, references);
                result.HitGraphEntities.Add(hit);
            }
            if (result.NeedConditionStatusMeetToRelease)
            {
                int summonLimit = 0;
                foreach (FightBattleHitEntity hit in result.HitGraphEntities)
                {
                    if (!hit.IsSummon) continue;
                    foreach (FightBattleSummonPool pool in hit.SummonPools)
                    {
                        if (pool == null || pool.MonsterId <= 0) continue;
                        summonLimit = checked(summonLimit + Math.Max(0, pool.MaxSummonCount));
                    }
                }
                if (summonLimit > 0) result.SummonCountLimit = summonLimit;
            }
            if (result.HitGraphEntities.Count == 0)
                throw new InvalidOperationException("Official skill has no resolved hit graph: " + entityId);
            return result;
        }

        public FightSkillSlotState CreateSkillSlot(FightBattleSkillDefinition skill, long cooldown,
            int lastRound = 0, int releasedTimes = 0)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (cooldown < 0 || releasedTimes < 0) throw new ArgumentOutOfRangeException(nameof(cooldown));
            return new FightSkillSlotState
            {
                Slot = skill.Slot,
                Cooldown = cooldown,
                LastRound = lastRound,
                ReleasedTimes = releasedTimes,
                LimitedTimes = skill.LimitedTimes,
                ResetCooldownAtStart = skill.ResetCooldownAtStart
            };
        }

        public void AssembleUnit(FightBattleUnitState unit,
            IList<FightOfficialSkillBinding> skills, IList<FightOfficialStatusBinding> statuses)
        {
            if (unit == null || unit.Id <= 0) throw new ArgumentException("Official combat unit identity is invalid.", nameof(unit));
            if (unit.SkillState == null) throw new ArgumentException("Official combat unit skill state is missing.", nameof(unit));
            if (unit.Skills == null) unit.Skills = new List<FightBattleSkillDefinition>();
            if (unit.SkillState.Slots == null) unit.SkillState.Slots = new List<FightSkillSlotState>();
            if (unit.Statuses == null) unit.Statuses = new List<FightBattleStatusState>();

            var slots = new HashSet<int>();
            foreach (FightSkillSlotState slot in unit.SkillState.Slots)
                if (slot != null) slots.Add(slot.Slot);
            var assembledSkills = new List<FightBattleSkillDefinition>();
            var assembledSlots = new List<FightSkillSlotState>();
            if (skills != null)
            {
                foreach (FightOfficialSkillBinding binding in skills)
                {
                    if (binding == null) throw new ArgumentException("Official skill binding is null.", nameof(skills));
                    if (!slots.Add(binding.Slot))
                        throw new InvalidOperationException("Official combat unit has duplicate skill slot " + binding.Slot);
                    FightBattleSkillDefinition skill = CreateSkill(binding.EntityId, binding.Slot,
                        binding.ItemId, binding.Rank, binding.Level, binding.SubRank, binding.Properties);
                    skill.SkillCost = binding.SkillCost;
                    skill.ProfessionAvailability = binding.ProfessionAvailability;
                    assembledSkills.Add(skill);
                    assembledSlots.Add(CreateSkillSlot(skill, binding.Cooldown,
                        binding.LastRound, binding.ReleasedTimes));
                }
            }

            var statusIds = new HashSet<int>();
            foreach (FightBattleStatusState previous in unit.Statuses)
                if (previous != null) statusIds.Add(previous.InstanceId);
            var assembledStatuses = new List<FightBattleStatusState>();
            if (statuses != null)
            {
                foreach (FightOfficialStatusBinding binding in statuses)
                {
                    if (binding == null || binding.InstanceId <= 0)
                        throw new ArgumentException("Official status binding identity is invalid.", nameof(statuses));
                    if (!statusIds.Add(binding.InstanceId))
                        throw new InvalidOperationException("Official combat unit has duplicate status instance " + binding.InstanceId);
                    assembledStatuses.Add(CreateStatus(binding.ClassId, unit.Id, binding.CreatorId,
                        binding.InstanceId, binding.RootSkillId, binding.ApplyingSkillItemId,
                        binding.SkillRank, binding.SkillLevel, binding.SourceSubRank,
                        binding.SourceProperties, binding.CustomShieldValue));
                }
            }
            unit.Skills.AddRange(assembledSkills);
            unit.SkillState.Slots.AddRange(assembledSlots);
            unit.Statuses.AddRange(assembledStatuses);
        }

        public List<FightOfficialStatusBinding> CreatePassiveStatusBindings(
            FightBattleUnitState unit, FightOfficialSkillItemCatalog skillItems,
            IList<FightOfficialPassiveSkillBinding> passiveSkills)
        {
            if (unit == null || unit.Id <= 0)
                throw new ArgumentException("Official passive status owner is invalid.", nameof(unit));
            if (skillItems == null) throw new ArgumentNullException(nameof(skillItems));

            int nextInstanceId = 1;
            foreach (FightBattleStatusState status in unit.Statuses ?? new List<FightBattleStatusState>())
                if (status != null && status.InstanceId >= nextInstanceId)
                {
                    if (status.InstanceId == Int32.MaxValue)
                        throw new InvalidOperationException("Official status instance id space is exhausted.");
                    nextInstanceId = status.InstanceId + 1;
                }

            var result = new List<FightOfficialStatusBinding>();
            if (passiveSkills == null) return result;
            foreach (FightOfficialPassiveSkillBinding binding in passiveSkills)
            {
                if (binding == null || binding.ItemId <= 0 || binding.Rank <= 0 || binding.Level <= 0
                    || binding.SubRank < 0)
                    throw new ArgumentException("Official passive skill binding is invalid.",
                        nameof(passiveSkills));
                FightOfficialSkillItem item = skillItems.Required(binding.ItemId);
                foreach (int statusId in item.PassiveStatusIds ?? System.Array.Empty<int>())
                {
                    if (statusId <= 0) continue;
                    if (nextInstanceId == Int32.MaxValue)
                        throw new InvalidOperationException("Official status instance id space is exhausted.");
                    result.Add(new FightOfficialStatusBinding
                    {
                        ClassId = statusId,
                        InstanceId = nextInstanceId++,
                        CreatorId = unit.Id,
                        RootSkillId = item.EntityId,
                        ApplyingSkillItemId = item.ItemId,
                        SkillRank = binding.Rank,
                        SkillLevel = binding.Level,
                        SourceSubRank = binding.SubRank,
                        SourceProperties = unit.Properties
                    });
                }
            }
            return result;
        }

        public FightBattleStatusState CreateStatus(int classId, int ownerId, int creatorId,
            int instanceId = 0, int rootSkillId = 0, int applyingSkillItemId = 0,
            int sourceSkillRank = 1, int sourceSkillLevel = 1, int sourceSubRank = 0,
            IList<FightPropertyValue> sourceProperties = null, long customShieldValue = 0)
        {
            Dictionary<string, object> components = RequiredEntity(classId);
            Dictionary<string, object> status = Component(components, "FightStatusComponent");
            Dictionary<string, object> action = Component(components, "ActionComponent");
            Dictionary<string, object> statusProps = Component(components, "FightStatusPropComponent")
                ?? Component(components, "FightStatusPropBySkillComponent")
                ?? Component(components, "FightStatusPropByStatusComponent");
            Dictionary<string, object> falloff = Component(components, "FightStatusDamageFalloffComponent");
            Dictionary<string, object> hpDecrease =
                Component(components, "FightStatusHpDecreaseUnitComponent");
            Dictionary<string, object> roundStart =
                Component(components, "FightStatusRoundStartComponent");
            Dictionary<string, object> roundEnd =
                Component(components, "FightStatusRoundEndComponent");
            Dictionary<string, object> skillStart =
                Component(components, "FightStatusSkillStartComponent");
            Dictionary<string, object> skillEnd =
                Component(components, "FightStatusSkillEndComponent");
            Dictionary<string, object> hpAccDecrease =
                Component(components, "FightStatusHpAccDecreaseComponent");
            Dictionary<string, object> skillCondition =
                Component(components, "FightStatusSkillConditionComponent");
            Dictionary<string, object> damageCustomProps =
                Component(components, "FightStatusBattleFormulaDamageCustomPropsComponent");
            Dictionary<string, object> shieldPersistent =
                Component(components, "FightStatusShieldPersistentComponent");
            Dictionary<string, object> shieldDisposable =
                Component(components, "FightStatusShieldDisposableComponent");
            Dictionary<string, object> shieldInvincible =
                Component(components, "FightStatusShieldInvincibleComponent");
            Dictionary<string, object> shieldCounted = Component(components, "FightStatusShieldComponent");
            Dictionary<string, object> damageReducePer =
                Component(components, "FightStatusDamageReducePerComponent");
            Dictionary<string, object> shield = shieldPersistent ?? shieldDisposable
                ?? shieldInvincible ?? shieldCounted;
            if (status == null) throw new InvalidOperationException("Official entity is not a status: " + classId);

            int durationRound = Int(status, "DurationRound");
            var result = new FightBattleStatusState
            {
                InstanceId = instanceId,
                ClassId = classId,
                StatusType = Int(status, "StatusType"),
                HasBackToLifeComponent = HasBackToLifeComponent(classId),
                ActionType = action == null ? 0 : Int(action, "ActionType"),
                DurationRound = durationRound,
                CurDurationRound = durationRound,
                StartRound = -1,
                RoundTarget = Int(status, "RoundTarget"),
                RoundUpdateTiming = Int(status, "RoundUpdateTiming"),
                RemoveAtRoundTargetDestroy = Bool(status, "RemoveAtRoundTargetDestroy"),
                ActionBehOfSameStatus = Int(status, "ActionBehOfSameStatus"),
                CanDispersed = Bool(status, "CanDispersed"),
                IsAddDurationRound = Bool(status, "IsAddDurationRound"),
                MaxAddDurationRound = Int(status, "MaxAddDurationRound", 100),
                IsResetDurationWhenStack = Bool(status, "IsResetDurationWhenStack"),
                ScalePropWhenChangeStacked = Bool(status, "ScalePropWhenChangeStacked"),
                ApplyIgnoreResilience = Bool(status, "ApplyIgnoreResilience"),
                ForbiddenOnApplicatorDie = Bool(status, "ForbiddenOnApplicatorDie"),
                FollowRoundUpdateTimingOnGlobalRound = Bool(status,
                    "FollowRoundUpdateTimingOnGlobalRound"),
                IsFixedProp = Bool(status, "IsFixedProp"),
                HpTriggerPer = hpDecrease == null ? -1f : Float(hpDecrease, "UnitHpPer", -1f),
                HpTriggerMaxCount = hpDecrease == null ? -1 : Int(hpDecrease, "MaxCount", -1),
                HpTriggerAtStart = hpDecrease != null && Bool(hpDecrease, "TryAtStart"),
                HasRoundStartTrigger = roundStart != null,
                RoundStartRulesSupported = roundStart != null
                    && SupportsRoundStart(roundStart),
                RoundStartListenTarget = roundStart == null ? 0
                    : Int(roundStart, "ListenTargetType"),
                RoundStartDelayRound = roundStart == null ? 0
                    : Int(roundStart, "DelayRound"),
                RoundStartRate = roundStart == null ? 1f
                    : Float(roundStart, "Rate", 1f),
                RoundStartMaxCount = roundStart == null ? -1
                    : Int(roundStart, "MaxCount", -1),
                RoundStartMeetCount = roundStart == null ? 1
                    : Int(roundStart, "MeetCount", 1),
                RoundStartTryAtStart = roundStart != null
                    && Bool(roundStart, "TryAtStart"),
                HasRoundEndTrigger = roundEnd != null,
                RoundEndRulesSupported = roundEnd != null && SupportsRoundEnd(roundEnd),
                RoundEndListenTarget = roundEnd == null ? 0
                    : Int(roundEnd, "ListenTargetType"),
                RoundEndMaxCount = roundEnd == null ? -1 : Int(roundEnd, "MaxCount", -1),
                RoundEndAutoRemove = roundEnd != null && Bool(roundEnd, "AutoRemove"),
                HasSkillStartTrigger = skillStart != null,
                SkillStartRulesSupported = skillStart != null
                    && SupportsSkillStart(skillStart),
                SkillStartSource = skillStart == null ? 0
                    : Int(skillStart, "SkillStartSource"),
                SkillStartMaxCount = skillStart == null ? -1
                    : Int(skillStart, "MaxCount", -1),
                HasSkillEndTrigger = skillEnd != null,
                SkillEndRulesSupported = skillEnd != null && SupportsSkillEnd(skillEnd),
                SkillEndMetCount = skillEnd == null ? 1 : Int(skillEnd, "MetCount", 1),
                HasHpAccDecrease = hpAccDecrease != null,
                HpAccUnitHp = hpAccDecrease == null ? -1L : Long(hpAccDecrease, "UnitHp", -1L),
                HpAccUnitHpPercentage = hpAccDecrease == null ? -1f
                    : Float(hpAccDecrease, "UnitHpPercentage", -1f),
                HpAccResetAtRoundEnd = hpAccDecrease != null
                    && Bool(hpAccDecrease, "ResetAtRoundEnd"),
                HpAccMaxCount = hpAccDecrease == null ? -1 : Int(hpAccDecrease, "MaxCount", -1),
                HpAccUpdateHpPerOnCure = hpAccDecrease == null
                    || Bool(hpAccDecrease, "UpdateHpPerOnCure", true),
                HasDamageCustomProps = damageCustomProps != null,
                DamageCustomPropsRate = damageCustomProps == null ? 1f
                    : Float(damageCustomProps, "Rate", 1f),
                DamageCustomPropsDmgGreaterMaxHpPer = damageCustomProps == null ? -1f
                    : Float(damageCustomProps, "DmgGreaterMaxHpPer", -1f),
                DamageCustomPropsMaxCount = damageCustomProps == null ? -1
                    : Int(damageCustomProps, "Count", -1),
                DamageCustomPropsAutoRemove = damageCustomProps != null
                    && Bool(damageCustomProps, "AutoRemove", true),
                DamageCustomPropsCheckSkillTrigger = damageCustomProps != null
                    && Bool(damageCustomProps, "IsCheckSkillTrigger", true),
                DamageCustomPropsPropertyId = damageCustomProps == null ? 0
                    : Int(damageCustomProps, "DmgReducePer"),
                DamageCustomPropsPhase = damageCustomProps == null ? 0
                    : Int(damageCustomProps, "Phase"),
                DamageReducePer = ParseDamageReducePer(damageReducePer),
                ShieldKind = shieldPersistent != null ? FightBattleShieldKind.Persistent
                    : shieldDisposable != null ? FightBattleShieldKind.Disposable
                    : shieldInvincible != null ? FightBattleShieldKind.Invincible
                    : shieldCounted != null ? FightBattleShieldKind.Counted
                    : FightBattleShieldKind.None,
                ShieldRate = shield == null ? 1f : Float(shield, "Rate", 1f),
                ShieldDamageGreaterMaxHpPer = shield == null ? -1f
                    : Float(shield, "DmgGreaterMaxHpPer", -1f),
                ShieldDestroyedSkillId = shield == null ? 0 : Int(shield, "DestroyedSkillId"),
                ShieldInitialCount = shieldDisposable != null ? Int(shieldDisposable, "Count", -1)
                    : shieldInvincible != null ? Int(shieldInvincible, "Count", -1)
                    : shieldCounted != null ? Int(shieldCounted, "Count", -1) : -1,
                ShieldRemainCount = shieldDisposable != null ? Int(shieldDisposable, "Count", -1)
                    : shieldInvincible != null ? Int(shieldInvincible, "Count", -1)
                    : shieldCounted != null ? Int(shieldCounted, "Count", -1) : -1,
                ShieldKeepRemainValue = shieldCounted != null && Bool(shieldCounted, "KeepRemainValue"),
                ShieldIgnoreValue = shieldCounted != null && Bool(shieldCounted, "IgnoreShieldValue"),
                ShieldUsesCustomValue = shieldPersistent != null
                    ? Bool(shieldPersistent, "CustomShield")
                    : shieldDisposable != null ? Bool(shieldDisposable, "CustomShield")
                    : shieldCounted != null && Bool(shieldCounted, "CustomShield"),
                StackCount = Int(status, "InitStackCount", 1),
                AddStackedCount = Int(status, "AddStackedCount", 1),
                AddStackedRandomMinCount = Int(status, "AddStackedRandomMinCount", 1),
                AddStackedRandomMaxCount = Int(status, "AddStackedRandomMaxCount", 1),
                MaxStackedCount = Int(status, "MaxStackedCount"),
                IsOpenStack = Bool(status, "IsOpenStack"),
                OwnerId = ownerId,
                CreatorId = creatorId,
                RootSkillId = rootSkillId,
                ApplyingSkillItemId = applyingSkillItemId,
                SourceSkillRank = Math.Max(1, sourceSkillRank),
                SourceSkillLevel = Math.Max(1, sourceSkillLevel),
                SourceSubRank = Math.Max(0, sourceSubRank),
                SourceProperties = CopyProperties(sourceProperties),
                HasPropertyComponent = statusProps != null,
                PropertyDefinitionResolved = true,
                CustomShieldValue = Math.Max(0L, customShieldValue),
                ShieldValue = Math.Max(0L, customShieldValue),
                ShieldRemainValue = Math.Max(0L, customShieldValue),
                Rate = 1.0,
                NumOfStart = 1,
                MaxFalloffCount = -1
            };
            if (hpDecrease != null)
            {
                AddHpTriggerStatuses(hpDecrease, result.HpTriggerStatuses);
                AddHpTriggerSkills(hpDecrease, result.HpTriggerSkills);
            }
            if (roundStart != null)
                AddHpAccTriggerEntries(roundStart, "StatusTriggerSkillCfgs", "SkillId",
                    result.RoundStartSkills, true);
            if (roundEnd != null)
                AddHpAccTriggerEntries(roundEnd, "TriggerSkillCfgs", "SkillId",
                    result.RoundEndSkills, true);
            if (skillStart != null)
            {
                AddHpAccTriggerEntries(skillStart, "Skills", "SkillId",
                    result.SkillStartSkills, true);
                object rawSettings;
                if (skillStart.TryGetValue("Settings", out rawSettings) && rawSettings != null)
                    foreach (object raw in Array(rawSettings, "FightStatusSkillStartComponent.Settings"))
                    {
                        Dictionary<string, object> setting = Object(raw,
                            "Fight status skill start setting");
                        result.SkillStartActionTypes.AddRange(IntArray(setting, "ActionTypes"));
                    }
            }
            if (skillEnd != null)
            {
                result.SkillEndFilteredActionTypes.AddRange(IntArray(skillEnd, "FilterTypes"));
                AddHpAccTriggerEntries(skillEnd, "StatusCfgs", "StatusId",
                    result.SkillEndStatuses, false);
            }
            if (hpAccDecrease != null)
            {
                AddHpAccTriggerEntries(hpAccDecrease, "StatusList", "StatusId",
                    result.HpAccTriggerStatuses, false);
                AddHpAccTriggerEntries(hpAccDecrease, "Skills", "SkillId",
                    result.HpAccTriggerSkills, true);
            }
            if (skillCondition != null)
            {
                if (skillCondition.TryGetValue("Settings", out object rawSettings)
                    && rawSettings != null)
                foreach (object raw in Array(rawSettings,
                    "FightStatusSkillConditionComponent.Settings"))
                {
                    Dictionary<string, object> setting = Object(raw,
                        "Fight status skill condition setting");
                    var rule = new FightBattleSkillConditionRule
                    {
                        ConditionType = Int(setting, "ConditionType"),
                        HpPerValue = Float(setting, "HpPerValue", -1f),
                        Round = Int(setting, "Round", -1),
                        ContrastiveRelation = Int(setting, "ContrastiveRelation"),
                        RoundTargetType = Int(setting, "RoundTargetType"),
                        SummonId = Int(setting, "SummonId"),
                        Count = Int(setting, "Count", -1),
                        GlobalRound = Int(setting, "GlobalRound", -1),
                        DurationRound = Bool(setting, "DurationRound", true)
                    };
                    if (rule.ConditionType == 4 && setting.TryGetValue("SummonPools",
                            out object rawPools) && rawPools != null)
                    {
                        foreach (object poolRaw in Array(rawPools,
                            "StatusSkillConditionSummonPoolSetting.SummonPools"))
                        {
                            Dictionary<string, object> pool = Object(poolRaw,
                                "status skill summon pool");
                            result.SummonConditionPools.Add(new FightBattleSummonPool
                            {
                                MonsterId = Int(pool, "SummonId"),
                                MaxSummonCount = Int(pool, "MaxSummonCount")
                            });
                        }
                    }
                    if (rule.ConditionType > 4) result.SkillConditionRulesSupported = false;
                    result.SkillConditionRules.Add(rule);
                }
                if (skillCondition.TryGetValue("SkillInfos", out object rawSkillInfos)
                    && rawSkillInfos != null)
                foreach (object raw in Array(rawSkillInfos,
                    "FightStatusSkillConditionComponent.SkillInfos"))
                {
                    Dictionary<string, object> info = Object(raw,
                        "Fight status conditional skill");
                    result.ConditionalSkills.Add(new FightBattleStatusSkillCondition
                    {
                        SkillEntityId = Int(info, "SkillEntityId"),
                        IsActive = Bool(info, "IsActive", true),
                        IsNoMeetSetNegation = Bool(info, "IsNoMeetSetNegation")
                    });
                }
            }
            if (statusProps != null)
            {
                if (statusProps.TryGetValue("TransitionPropCfgs", out object rawTransitions)
                    && rawTransitions != null)
                foreach (object raw in Array(rawTransitions, "FightStatusPropComponent.TransitionPropCfgs"))
                {
                    Dictionary<string, object> transition = Object(raw,
                        "FightStatusPropComponent.TransitionPropCfg");
                    result.PropertyTransitions.Add(new FightBattleStatusPropertyTransition
                    {
                        PropertyId = Int(transition, "Prop"),
                        TargetType = Int(transition, "Target"),
                        TargetPropertyId = Int(transition, "TargetProp"),
                        AdditionPropertyId = Int(transition, "AdditionProp"),
                        FixedAdditionPropertyId = Int(transition, "FixedAdditionProp")
                    });
                }
            }
            if (falloff != null)
            {
                result.FalloffTarget = Int(falloff, "FalloffTarget");
                result.SkillEntityClassId = Int(falloff, "SkillEntClassId");
                result.NumOfStart = Int(falloff, "NumOfStart", 1);
                result.MaxFalloffCount = Int(falloff, "MaxFalloffCount", -1);
                result.IsCheckSkillTrigger = Bool(falloff, "IsCheckSkillTrigger");
                result.DamageId = Int(falloff, "DamageId");
                result.RootSkillIds = IntArray(falloff, "RootSkillIds");
                result.RemoveOnSourceSkillStart = Bool(falloff, "RemoveOnSourceSkillStart");
                result.FalloffPercent = Int(falloff, "FalloffPercent");
                result.Rate = Number(falloff, "Rate", 1.0);
                result.Scope = Int(falloff, "Scope");
            }
            return result;
        }

        private static FightBattleStatusDamageReducePerInfo ParseDamageReducePer(
            Dictionary<string, object> component)
        {
            if (component == null) return null;
            Dictionary<string, object> statusType = Component(component, "StatusType");
            Dictionary<string, object> skillType = Component(component, "SkillType");
            var result = new FightBattleStatusDamageReducePerInfo
            {
                Rate = Float(component, "Rate", 1f),
                Block = Bool(component, "Block"),
                Crit = Bool(component, "Crit"),
                Damage = Bool(component, "Damage", true),
                Cure = Bool(component, "Cure"),
                RangeCondition = Int(component, "RangeCondition"),
                SkillTargetType = Int(component, "SkillTargetType", 4),
                ConditionCount = Int(component, "ConditionCount", 1),
                MaxCount = Int(component, "MaxCount", -1),
                AutoDestroy = Bool(component, "AutoDestory"),
                IsMutexByBetter = Bool(component, "IsMutexByBetter"),
                ScalePropByStatusTypeCount = Bool(component, "ScalePropByStatusTypeCount"),
                ActiveSkillScale = skillType == null ? 1f : Float(skillType, "ActiveSkillScale", 1f),
                NotActiveSkillScale = skillType == null ? 1f : Float(skillType, "NotActiveSkillScale", 1f)
            };
            if (statusType != null)
            {
                result.StatusType.IsCondition = Bool(statusType, "IsCondition", true);
                result.StatusType.Target = Int(statusType, "Target");
                result.StatusType.CountType = Int(statusType, "CountType", 1);
                result.StatusType.MaxStatusCount = Int(statusType, "MaxStatusCount", -1);
                result.StatusType.Unit = Float(statusType, "Unit", 1f);
                result.StatusType.IsAdditionCount = Bool(statusType, "IsAdditionCount");
                result.StatusType.IsOpenZeroCheckWhenCalcStatus =
                    Bool(statusType, "IsOpenZeroCheckWhenCalcStatus");
                result.StatusType.IsCalculateStack = Bool(statusType, "IsCalculateStack");
                result.StatusType.AnyStatusTypes.AddRange(IntArray(statusType, "AnyStatusTypes"));
                result.StatusType.AnyActionTypes.AddRange(IntArray(statusType, "AnyActionTypes"));
                result.Supported = IntArray(statusType, "AnyLogicTags").Length == 0
                    && result.StatusType.CountType >= 0 && result.StatusType.CountType <= 2;
                if (result.ScalePropByStatusTypeCount)
                    result.Supported = result.Supported
                        && (result.StatusType.Target == 1 || result.StatusType.Target == 2
                            || result.StatusType.Target == 3 || result.StatusType.Target == 5
                            || result.StatusType.Target == 6);
            }
            result.Supported = result.Supported && ArrayCount(component,
                "StatusReplacePropertyValues") == 0;
            return result;
        }

        private Dictionary<int, string> monsterNames;
        private Dictionary<int, int> monsterIconIds;

        /// <summary>Official monster table IconId used by portrait resources named G_&lt;IconId&gt;.</summary>
        public int MonsterIconId(int monsterId)
        {
            if (monsterId <= 0) return 0;
            if (monsterIconIds == null)
            {
                monsterIconIds = new Dictionary<int, int>();
                object raw;
                if (battleTables.TryGetValue("monster", out raw) && raw is List<object> rows)
                    foreach (object item in rows)
                    {
                        Dictionary<string, object> row = item as Dictionary<string, object>;
                        if (row == null) continue;
                        int id = Int(row, "Id");
                        int mappedIconId = Int(row, "IconId");
                        if (id > 0 && mappedIconId > 0 && !monsterIconIds.ContainsKey(id))
                            monsterIconIds.Add(id, mappedIconId);
                    }
            }
            return monsterIconIds.TryGetValue(monsterId, out int iconId) ? iconId : 0;
        }

        /// <summary>
        /// Official monster display name from the pinned monster table: CustomTypeName when the
        /// monster has a variant name, otherwise Memo. Returns null for unknown monsters.
        /// </summary>
        public string MonsterName(int monsterId)
        {
            if (monsterId <= 0) return null;
            if (monsterNames == null)
            {
                monsterNames = new Dictionary<int, string>();
                object raw;
                if (battleTables.TryGetValue("monster", out raw) && raw is List<object> rows)
                {
                    foreach (object item in rows)
                    {
                        Dictionary<string, object> row = item as Dictionary<string, object>;
                        if (row == null) continue;
                        int id = Int(row, "Id");
                        string name = Convert.ToString(row.TryGetValue("CustomTypeName", out object custom)
                            ? custom : null, CultureInfo.InvariantCulture);
                        if (string.IsNullOrWhiteSpace(name))
                            name = Convert.ToString(row.TryGetValue("Memo", out object memo) ? memo : null,
                                CultureInfo.InvariantCulture);
                        if (id > 0 && !string.IsNullOrWhiteSpace(name) && !monsterNames.ContainsKey(id))
                            monsterNames.Add(id, name.Trim());
                    }
                }
            }
            return monsterNames.TryGetValue(monsterId, out string result) ? result : null;
        }
        public int EntityCount => entities.Count;

        private static readonly IReadOnlyDictionary<int, long> EmptyProps =
            new Dictionary<int, long>();

        private sealed class SkillPropTable
        {
            public int MinLevel;
            public int MaxLevel;
            public Dictionary<int, Dictionary<int, long>> Rows;
        }

        private sealed class FightOfficialEntityProp
        {
            public int EntityId;
            public int RankPropId;
            public int GroupLevelPropId;
            public Dictionary<int, long> PropFactors = new Dictionary<int, long>();
        }

        private readonly Dictionary<int, SkillPropTable> skillPropTables =
            new Dictionary<int, SkillPropTable>();
        private readonly Dictionary<int, FightOfficialEntityProp> skillEntityProps =
            new Dictionary<int, FightOfficialEntityProp>();
        private readonly Dictionary<int, Dictionary<int, int>> skillGroupLevels =
            new Dictionary<int, Dictionary<int, int>>();
        private int cooldownPropertyId;


        private const long ActionBehaviourDiscard = 2;

        private void ParseStateMutex()
        {
            if (!battleTables.TryGetValue("state_mutex", out object rawRows)) return;
            var matrix = new Dictionary<int, Dictionary<int, long>>();
            foreach (object value in Array(rawRows, "state_mutex"))
            {
                Dictionary<string, object> row = Object(value, "state_mutex row");
                int fromType = Int(row, "From");
                if (!matrix.TryGetValue(fromType, out Dictionary<int, long> behaviours))
                    matrix[fromType] = behaviours = new Dictionary<int, long>();
                foreach (KeyValuePair<int, long> behaviour in ReadLongMap(row, "Behaviours"))
                    behaviours[behaviour.Key] = behaviour.Value;
            }

            foreach (KeyValuePair<int, Dictionary<int, long>> row in matrix)
                foreach (KeyValuePair<int, long> behaviour in row.Value)
                {
                    if (behaviour.Value != ActionBehaviourDiscard) continue;
                    if (!actionBlockersByProposedType.TryGetValue(behaviour.Key,
                        out List<int> blockers))
                        actionBlockersByProposedType[behaviour.Key] = blockers = new List<int>();
                    blockers.Add(row.Key);
                }
        }

        private List<int> BlockingStatusActionTypes(int proposedActionType)
        {
            return proposedActionType > 0
                && actionBlockersByProposedType.TryGetValue(proposedActionType,
                    out List<int> blockers)
                ? new List<int>(blockers) : new List<int>();
        }
        private void ParseSkillPropertyTables(Dictionary<string, object> skillCatalog)
        {
            Dictionary<string, object> props = Object(Required(skillCatalog, "props"), "skill props");
            foreach (KeyValuePair<string, object> pair in props)
            {
                int classId = IntKey(pair.Key, "skill prop class");
                Dictionary<string, object> table = Object(pair.Value, "skill prop " + pair.Key);
                var rows = new Dictionary<int, Dictionary<int, long>>();
                int min = int.MaxValue;
                int max = int.MinValue;
                Dictionary<string, object> sourceRows = Object(Required(table, "rows"),
                    "skill prop rows " + pair.Key);
                foreach (KeyValuePair<string, object> sourceRow in sourceRows)
                {
                    int level = IntKey(sourceRow.Key, "skill prop level");
                    var values = new Dictionary<int, long>();
                    foreach (KeyValuePair<string, object> value in Object(sourceRow.Value,
                        "skill prop row " + sourceRow.Key))
                        values[IntKey(value.Key, "skill prop id")] =
                            Convert.ToInt64(value.Value, CultureInfo.InvariantCulture);
                    rows[level] = values;
                    if (level < min) min = level;
                    if (level > max) max = level;
                }
                skillPropTables[classId] = new SkillPropTable
                {
                    MinLevel = min == int.MaxValue ? 0 : min,
                    MaxLevel = max == int.MinValue ? 0 : max,
                    Rows = rows
                };
            }

            Dictionary<string, object> tables = Object(Required(skillCatalog, "tables"),
                "skill tables");
            foreach (object item in Array(Required(tables, "entity_prop"), "entity_prop"))
            {
                Dictionary<string, object> row = Object(item, "entity_prop row");
                int entityId = Int(row, "EntityId");
                if (entityId <= 0) continue;
                skillEntityProps[entityId] = new FightOfficialEntityProp
                {
                    EntityId = entityId,
                    RankPropId = Int(row, "RankPropId"),
                    GroupLevelPropId = Int(row, "GroupLevelPropId"),
                    PropFactors = ReadLongMap(row, "PropFactors")
                };
            }
            foreach (object item in Array(Required(tables, "entity_prop_group_level"),
                "entity_prop_group_level"))
            {
                Dictionary<string, object> row = Object(item, "entity_prop_group_level row");
                int groupId = Int(row, "GroupId");
                if (groupId <= 0) continue;
                if (!skillGroupLevels.TryGetValue(groupId, out Dictionary<int, int> bySubRank))
                {
                    bySubRank = new Dictionary<int, int>();
                    skillGroupLevels.Add(groupId, bySubRank);
                }
                bySubRank[Int(row, "SubRank")] = Int(row, "LevelPropId");
            }

            foreach (KeyValuePair<string, object> property in skillPropertyNames)
                if (string.Equals(Convert.ToString(property.Value, CultureInfo.InvariantCulture), "CD",
                        StringComparison.Ordinal))
                    cooldownPropertyId = IntKey(property.Key, "cooldown property");
        }

        private IReadOnlyDictionary<int, long> PropRows(int classId, int level)
        {
            if (!skillPropTables.TryGetValue(classId, out SkillPropTable table)
                || table.Rows.Count == 0) return EmptyProps;
            if (table.Rows.TryGetValue(level, out Dictionary<int, long> exact)) return exact;
            if (level >= table.MaxLevel
                && table.Rows.TryGetValue(table.MaxLevel, out Dictionary<int, long> maximum)) return maximum;
            int nearest = table.MinLevel;
            foreach (int candidate in table.Rows.Keys)
                if (candidate <= level && candidate > nearest) nearest = candidate;
            return table.Rows.TryGetValue(nearest, out Dictionary<int, long> fallback)
                ? fallback : EmptyProps;
        }

        private Dictionary<string, object> FindMonsterGroup(int monsterId, int groupNo)
        {
            if (!battleTables.TryGetValue("monster_group", out object rawRows)
                || !(rawRows is List<object> rows)) return null;
            foreach (object item in rows)
            {
                Dictionary<string, object> row = Object(item, "monster_group row");
                if (Int(row, "MonsterId") == monsterId && Int(row, "GroupNo") == groupNo)
                    return row;
            }
            return null;
        }

        private void ParseMonsterHpPlies(string json)
        {
            Dictionary<string, object> root = Object(OfficialJson.Parse(json), "monster HP plies");
            string sourceManifest = Convert.ToString(Required(root, "manifest"),
                CultureInfo.InvariantCulture);
            if (!string.Equals(sourceManifest, ExpectedManifest, StringComparison.Ordinal))
                throw new InvalidOperationException("Monster HP plies table is not from the pinned original version.");
            foreach (object rawRow in Array(Required(root, "rows"), "monster HP plies rows"))
            {
                Dictionary<string, object> row = Object(rawRow, "monster HP plies row");
                int id = Int(row, "Id");
                int lowLevel = Int(row, "LowLevel");
                int plies = Int(row, "Plies");
                if (id <= 0 || lowLevel <= 0 || plies <= 0)
                    throw new FormatException("Official monster HP plies row has an invalid value.");
                if (!monsterHpPlies.TryGetValue(id, out List<MonsterHpPliesLevel> levels))
                {
                    levels = new List<MonsterHpPliesLevel>();
                    monsterHpPlies.Add(id, levels);
                }
                levels.Add(new MonsterHpPliesLevel { LowLevel = lowLevel, Plies = plies });
            }
        }

        private Dictionary<string, object> FindRolePropGroup(int propGroupId, int subRank)
        {
            if (!battleTables.TryGetValue("role_prop_group", out object rawRows)
                || !(rawRows is List<object> rows)) return null;
            Dictionary<string, object> fallback = null;
            foreach (object item in rows)
            {
                Dictionary<string, object> row = Object(item, "role_prop_group row");
                if (Int(row, "ID") != propGroupId) continue;
                int rowSubRank = Int(row, "SubRank");
                if (rowSubRank == subRank) return row;
                if (rowSubRank <= subRank && (fallback == null || rowSubRank > Int(fallback, "SubRank")))
                    fallback = row;
            }
            return fallback;
        }

        private Dictionary<int, long> ReadMonsterLevelProps(int classId, int level)
        {
            if (classId <= 0 || !battleProperties.TryGetValue(classId.ToString(CultureInfo.InvariantCulture),
                    out object rawTable)) return new Dictionary<int, long>();
            Dictionary<string, object> table = Object(rawTable, "monster prop class " + classId);
            Dictionary<string, object> rows = Object(Required(table, "rows"), "monster prop rows");
            if (rows.Count == 0) return new Dictionary<int, long>();
            string selectedLevel = null;
            int selected = int.MinValue;
            foreach (string key in rows.Keys)
            {
                int candidate = IntKey(key, "monster prop level");
                if (candidate <= level && candidate > selected)
                {
                    selected = candidate;
                    selectedLevel = key;
                }
            }
            if (selectedLevel == null)
            {
                foreach (string key in rows.Keys)
                {
                    int candidate = IntKey(key, "monster prop level");
                    if (candidate < selected)
                    {
                        selected = candidate;
                        selectedLevel = key;
                    }
                }
            }
            return ReadLongMap(rows, selectedLevel);
        }

        private static void AddProperties(Dictionary<int, long> target, IReadOnlyDictionary<int, long> source)
        {
            if (source == null) return;
            foreach (KeyValuePair<int, long> property in source)
                AddProperty(target, property.Key, property.Value);
        }

        private int PropertyId(string name)
        {
            foreach (KeyValuePair<string, object> property in skillPropertyNames)
                if (string.Equals(Convert.ToString(property.Value, CultureInfo.InvariantCulture), name,
                    StringComparison.Ordinal))
                    return IntKey(property.Key, "property ID");
            return 0;
        }

        private static long Value(IReadOnlyDictionary<int, long> properties, int propertyId)
        {
            return propertyId > 0 && properties != null
                && properties.TryGetValue(propertyId, out long value) ? value : 0L;
        }

        private static void AddProperty(Dictionary<int, long> properties, int propertyId, long value)
        {
            if (propertyId <= 0 || value == 0) return;
            properties[propertyId] = checked(Value(properties, propertyId) + value);
        }

        private void Increment(Dictionary<int, long> properties, string propertyName,
            string ratePropertyName)
        {
            int propertyId = PropertyId(propertyName);
            Increment(properties, propertyName, ratePropertyName, Value(properties, propertyId));
        }

        private void Increment(Dictionary<int, long> properties, string propertyName,
            string ratePropertyName, long basis)
        {
            int propertyId = PropertyId(propertyName);
            int ratePropertyId = PropertyId(ratePropertyName);
            long rate = Value(properties, ratePropertyId);
            if (propertyId <= 0 || rate == 0 || basis == 0) return;
            AddProperty(properties, propertyId, (long)(basis * (rate / 10000.0)));
        }

        private static Dictionary<int, long> ScaleProps(IReadOnlyDictionary<int, long> props,
            IReadOnlyDictionary<int, long> scaleProps, bool removeUnscaled)
        {
            var result = new Dictionary<int, long>();
            if (props == null) return result;
            foreach (KeyValuePair<int, long> prop in props)
            {
                if (scaleProps == null || !scaleProps.TryGetValue(prop.Key, out long scale))
                {
                    if (!removeUnscaled) result[prop.Key] = prop.Value;
                    continue;
                }
                result[prop.Key] = (long)(prop.Value * (scale / 10000.0));
            }
            return result;
        }

        internal static void VerifyPayloadForDerived(string name, string json, string expectedSha256,
            string manifest)
        {
            VerifyPayload(name, json, expectedSha256, manifest);
        }

        internal static void VerifyPayloadForDerived(string name, string json, string expectedSha256,
            string manifest, string expectedManifest)
        {
            VerifyPayload(name, json, expectedSha256, manifest, expectedManifest);
        }

        internal static Dictionary<string, object> ParseObject(string json, string path)
        {
            return Object(OfficialJson.Parse(json), path);
        }

        internal static Dictionary<string, object> ParseObject(object value, string path)
        {
            return Object(value, path);
        }

        internal static List<object> ParseArray(object value, string path)
        {
            return Array(value, path);
        }

        internal static object RequiredField(Dictionary<string, object> value, string key)
        {
            return Required(value, key);
        }

        internal static int ReadInt(Dictionary<string, object> value, string key, int fallback = 0)
        {
            return Int(value, key, fallback);
        }

        internal static int[] ReadIntArray(Dictionary<string, object> value, string key)
        {
            return IntArray(value, key);
        }

        internal static Dictionary<int, long> ReadLongMap(Dictionary<string, object> value, string key)
        {
            var result = new Dictionary<int, long>();
            object raw;
            if (!value.TryGetValue(key, out raw) || raw == null) return result;
            foreach (KeyValuePair<string, object> pair in Object(raw, key))
            {
                int id;
                if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    throw new FormatException("Invalid integer map key at " + key + ": " + pair.Key);
                result[id] = pair.Value == null ? 0 : Convert.ToInt64(pair.Value, CultureInfo.InvariantCulture);
            }
            return result;
        }

        /// <summary>Whether the (possibly enriched) catalog has this component for the entity.</summary>
        public bool HasComponent(int entityId, string componentName)
        {
            if (string.IsNullOrEmpty(componentName)) return false;
            return entities.TryGetValue(entityId, out Dictionary<string, object> components)
                && components.ContainsKey(componentName);
        }

        /// <summary>Reads a component dictionary for callers that implement optional effects.</summary>
        public bool TryGetComponent(int entityId, string componentName,
            out IReadOnlyDictionary<string, object> component)
        {
            component = null;
            if (string.IsNullOrEmpty(componentName)
                || !entities.TryGetValue(entityId, out Dictionary<string, object> components)
                || !components.TryGetValue(componentName, out object value)
                || !(value is Dictionary<string, object> dictionary)) return false;
            component = dictionary;
            return true;
        }

        private void AddEntities(Dictionary<string, object> root)
        {
            Dictionary<string, object> values = Object(Required(root, "entities"), "entities");
            foreach (KeyValuePair<string, object> pair in values)
            {
                int id;
                if (!int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0)
                    throw new FormatException("Official combat entity ID is invalid: " + pair.Key);
                Dictionary<string, object> incoming = Object(pair.Value, "entity " + pair.Key);
                Dictionary<string, object> existing;
                if (!entities.TryGetValue(id, out existing))
                {
                    entities.Add(id, incoming);
                    continue;
                }
                foreach (KeyValuePair<string, object> component in incoming)
                {
                    object previous;
                    if (!existing.TryGetValue(component.Key, out previous))
                    {
                        existing.Add(component.Key, component.Value);
                        continue;
                    }
                    if (!JsonEqual(previous, component.Value))
                        throw new InvalidOperationException("Official combat entity component conflicts between catalogs: "
                            + id + "/" + component.Key);
                }
            }
        }

        private Dictionary<string, object> RequiredEntity(int id)
        {
            Dictionary<string, object> value;
            if (!entities.TryGetValue(id, out value))
                throw new KeyNotFoundException("Official combat entity is absent: " + id);
            return value;
        }

        /// <summary>Reads <c>FightDamageComponent.MoveCfg</c> (knockback/pull on hit).</summary>
        private void AddHitMove(int damageId, FightBattleHitEntity output)
        {
            if (damageId <= 0 || output == null
                || !entities.TryGetValue(damageId, out Dictionary<string, object> components)
                || !components.TryGetValue("FightDamageComponent", out object raw)
                || !(raw is Dictionary<string, object> damage)
                || !damage.TryGetValue("MoveCfg", out object rawMove)
                || !(rawMove is Dictionary<string, object> move)) return;
            output.HitMove = ParseMove(move);
        }

        /// <summary>Reads <c>FightDamageComponent.DisperseStatus</c> (dispel on hit).</summary>
        /// <summary>Reads <c>FightDamageComponent.ChildSkillCfg</c> (child skill on damaged target).</summary>
        private void AddChildSkill(int damageId, FightBattleHitEntity output)
        {
            if (damageId <= 0 || output == null
                || !entities.TryGetValue(damageId, out Dictionary<string, object> components)
                || !components.TryGetValue("FightDamageComponent", out object raw)
                || !(raw is Dictionary<string, object> damage)
                || !damage.TryGetValue("ChildSkillCfg", out object rawChild)
                || !(rawChild is Dictionary<string, object> child)) return;
            int skillId = Int(child, "SkillId");
            if (skillId <= 0) return;
            output.ChildSkillId = skillId;
            output.ChildSkillRate = Float(child, "Rate", 1f);
            output.ChildSkillLessThanHpPer = Float(child, "LessThanHpPer", -1f);
        }

        private void AddDisperseStatus(int damageId, FightBattleHitEntity output)
        {
            if (damageId <= 0 || output == null
                || !entities.TryGetValue(damageId, out Dictionary<string, object> components)
                || !components.TryGetValue("FightDamageComponent", out object raw)
                || !(raw is Dictionary<string, object> damage)
                || !damage.TryGetValue("DisperseStatus", out object rawDisperse)
                || !(rawDisperse is Dictionary<string, object> disperse)) return;
            output.DisperseChance = Float(disperse, "BasePercent", 0f);
            output.DisperseAffectedByProp = Bool(disperse, "AffectedByProp");
            output.DisperseCount = Int(disperse, "Count");
            output.DisperseRandom = Bool(disperse, "IsRandom");
            AddIntArray(disperse, "TargetTypes", output.DisperseStatusTypes,
                new Queue<int>(), new Dictionary<int, FightBattleHitReference>());
        }

        /// <summary>
        /// Reads <c>FightDamageComponent.StatusList</c>: statuses applied when a hit carrying this
        /// damage id lands (target type, probability and remaining-HP condition).
        /// </summary>
        private void AddStatusApplications(int damageId, FightBattleHitEntity output)
        {
            if (damageId <= 0 || output == null
                || !entities.TryGetValue(damageId, out Dictionary<string, object> components)
                || !components.TryGetValue("FightDamageComponent", out object raw)
                || !(raw is Dictionary<string, object> damage)) return;
            output.StatusListRandomOne = Bool(damage, "StatusListRandomOne");
            List<FightBattleStatusApplication> applications = output.AppliedStatuses;
            if (!damage.TryGetValue("StatusList", out object list) || list == null) return;
            foreach (object item in Array(list, "StatusList"))
            {
                Dictionary<string, object> row = Object(item, "StatusList entry");
                int statusId = Int(row, "StatusId");
                if (statusId <= 0) continue;
                applications.Add(new FightBattleStatusApplication
                {
                    StatusId = statusId,
                    TargetType = Int(row, "TargetType", 2),
                    Chance = Float(row, "BasePercent", 1f),
                    AffectedByProp = Bool(row, "AffectedByProp", true),
                    CharacterTypeRateOverrides = ParseStatusRateOverrides(row),
                    LessThanHpPer = Float(row, "LessThanHpPer", -1f)
                });
            }
        }

        private static List<FightBattleStatusRateOverride> ParseStatusRateOverrides(
            Dictionary<string, object> row)
        {
            var result = new List<FightBattleStatusRateOverride>();
            if (row == null || !row.TryGetValue("OverrideRateByCharacterTypes", out object raw)
                || raw == null) return result;
            foreach (object item in Array(raw, "OverrideRateByCharacterTypes"))
            {
                Dictionary<string, object> value = Object(item, "status rate override");
                result.Add(new FightBattleStatusRateOverride
                {
                    CharacterType = Int(value, "CharacterType"),
                    Rate = Float(value, "OverrideRate", 0f),
                    AffectedByProp = Bool(value, "OverrideAffectedByProp")
                });
            }
            return result;
        }

        private static FightBattleSkillMove ParseMove(Dictionary<string, object> row,
            int offsetX = int.MinValue, int offsetY = int.MinValue, bool? hasConditions = null)
        {
            if (offsetX == int.MinValue)
            {
                offsetX = 0;
                offsetY = 0;
                if (row.TryGetValue("Offset", out object offsetValue) && offsetValue != null)
                {
                    Dictionary<string, object> offset = Object(offsetValue, "move offset");
                    offsetX = Int(offset, "X");
                    offsetY = Int(offset, "Y");
                }
            }
            bool conditions = hasConditions ?? (row.TryGetValue("DoMoveConditions", out object sourceConditions)
                && sourceConditions != null && Array(sourceConditions, "DoMoveConditions").Count > 0);
            return new FightBattleSkillMove
            {
                MoveBehaviourId = Int(row, "MoveBehaviourId"),
                Delay = Float(row, "Delay", 0f),
                OffsetX = offsetX,
                OffsetY = offsetY,
                TowardsTargetPos = Bool(row, "TowardsTargetPos"),
                Direction = Int(row, "SkillMoveDirection"),
                Distance = Int(row, "Distance", -1),
                BasePercent = Float(row, "BasePercent", 1f),
                AffectedByProp = Bool(row, "AffectedByProp"),
                CancelDiagonally = Bool(row, "CancelDiagonally"),
                HasConditions = conditions,
                Conditions = ParseMoveConditions(row)
            };
        }

        private static List<FightBattleSkillMoveCondition> ParseMoveConditions(
            Dictionary<string, object> row)
        {
            var result = new List<FightBattleSkillMoveCondition>();
            if (!row.TryGetValue("DoMoveConditions", out object sourceConditions)
                || sourceConditions == null) return result;
            foreach (object item in Array(sourceConditions, "DoMoveConditions"))
            {
                Dictionary<string, object> condition = Object(item, "move condition");
                object rawType;
                result.Add(new FightBattleSkillMoveCondition
                {
                    Type = condition.TryGetValue("_type", out rawType)
                        ? Convert.ToString(rawType, CultureInfo.InvariantCulture) : string.Empty,
                    Distance = Int(condition, "Distance")
                });
            }
            return result;
        }

        private static void AddHpTriggerSkills(Dictionary<string, object> component,
            List<int> output)
        {
            object raw;
            if (!component.TryGetValue("Skills", out raw) || raw == null) return;
            foreach (object item in Array(raw, "Skills"))
            {
                Dictionary<string, object> row = Object(item, "hp trigger skill");
                int skillId = Int(row, "SkillId");
                if (skillId > 0) output.Add(skillId);
            }
        }

        private static void AddHpTriggerStatuses(Dictionary<string, object> component,
            List<int> output)
        {
            object raw;
            if (!component.TryGetValue("StatusList", out raw) || raw == null) return;
            foreach (object item in Array(raw, "StatusList"))
            {
                Dictionary<string, object> row = Object(item, "hp trigger status");
                int statusId = Int(row, "StatusId");
                if (statusId > 0) output.Add(statusId);
            }
        }

        private static void AddHpAccTriggerEntries(Dictionary<string, object> component,
            string fieldName, string idField, List<FightBattleStatusStateTrigger> output,
            bool skill)
        {
            object raw;
            if (!component.TryGetValue(fieldName, out raw) || raw == null) return;
            foreach (object item in Array(raw, fieldName))
            {
                Dictionary<string, object> row = Object(item, "HP accumulated trigger");
                int entityId = Int(row, idField);
                if (entityId <= 0) continue;
                output.Add(new FightBattleStatusStateTrigger
                {
                    EntityId = entityId,
                    BasePercent = Float(row, "BasePercent", 1f),
                    AffectedByProp = Bool(row, "AffectedByProp"),
                    Source = skill ? Int(row, "Source") : 0,
                    Target = skill ? Int(row, "Target") : Int(row, "ApplyTarget"),
                    UseAI = skill && Bool(row, "UseAI"),
                    TargetPos = skill ? Int(row, "TargetPos") : 0,
                    StackedCountToDamageFactor = skill
                        && Bool(row, "StackedCountToDamageFactor")
                });
            }
        }

        private static bool SupportsRoundStart(Dictionary<string, object> component)
        {
            return Int(component, "ListenCharacterType") == 0
                && Int(component, "DelayRound") == 0
                && Int(component, "DelayRoundType") == 0
                && Int(component, "MeetCount", 1) == 1
                && Int(component, "RoundStartPhase") == 0
                && Float(component, "TargetHpPer", -1f) < 0f
                && !Bool(component, "TryAtStart")
                && !Bool(component, "TryAtNotForbidden")
                && !Bool(component, "StatusRandomOneTrigger")
                && !Bool(component, "RemoveOldStatusOnInvoke")
                && !Bool(component, "RemoveOldStatusOnEnd")
                && ArrayCount(component, "TargetActions") == 0
                && ArrayCount(component, "TriggerTargetNotStatus") == 0
                && ArrayCount(component, "StatusTriggerStatusCfgs") == 0
                && ArrayCount(component, "RoundStartHandles") == 0
                && ArrayCount(component, "StatusReplacePropertyValues") == 0
                && !HasNonNull(component, "FilterCondition")
                && SupportsRoundStartSkills(component);
        }

        private static bool SupportsRoundStartSkills(Dictionary<string, object> component)
        {
            if (!component.TryGetValue("StatusTriggerSkillCfgs", out object raw) || raw == null)
                return true;
            foreach (object item in Array(raw, "StatusTriggerSkillCfgs"))
            {
                Dictionary<string, object> trigger = Object(item, "round start trigger skill");
                if (Bool(trigger, "AffectedByProp") || Int(trigger, "Source") != 2
                    || Int(trigger, "Target") != 2 || Bool(trigger, "UseAI")
                    || Bool(trigger, "StackedCountToDamageFactor")
                    || (Int(trigger, "TargetPos") != 0 && Int(trigger, "TargetPos") != 1)
                    || ArrayCount(trigger, "StatusReplacePropertyValues") > 0
                    || ArrayCount(trigger, "TargetWithoutStatusIds") > 0) return false;
            }
            return true;
        }

        private static bool SupportsRoundEnd(Dictionary<string, object> component)
        {
            int listener = Int(component, "ListenTargetType");
            Dictionary<string, object> entityCount = Component(component, "EntityCount");
            if (listener < 1 || listener > 4
                || Int(component, "MaxCount", -1) < -1
                || Bool(component, "AutoRemove")
                || ArrayCount(component, "StatusReplacePropertyValues") > 0
                || ArrayCount(component, "TriggerStatusCfgs") > 0
                || HasNonNull(component, "FilterCondition")
                || entityCount != null && (ArrayCount(entityCount, "Range") > 0
                    || Int(entityCount, "EntityClassId") != 0
                    || Int(entityCount, "MaxCount") != 0
                    || Int(entityCount, "ResultOffset") != 0)) return false;
            object raw;
            if (!component.TryGetValue("TriggerSkillCfgs", out raw) || raw == null) return false;
            foreach (object item in Array(raw, "TriggerSkillCfgs"))
            {
                Dictionary<string, object> trigger = Object(item, "round end trigger skill");
                Dictionary<string, object> custom = Component(trigger, "CustomDamageInfo");
                Dictionary<string, object> propTarget = Component(trigger, "PropTarget");
                if (Bool(trigger, "AffectedByProp") || Int(trigger, "Source") != 2
                    || Int(trigger, "Target") != 5 || Bool(trigger, "UseAI")
                    || Bool(trigger, "StackedCountToDamageFactor")
                    || Int(trigger, "TargetPos") != 1
                    || Float(trigger, "BasePercent", 1f) < 0f
                    || Float(trigger, "BasePercent", 1f) > 1f
                    || ArrayCount(trigger, "StatusReplacePropertyValues") > 0
                    || ArrayCount(trigger, "TargetWithoutStatusIds") > 0
                    || propTarget != null && (Bool(propTarget, "IsEnemy")
                        || ArrayCount(propTarget, "RoleType") > 0
                        || Bool(propTarget, "IncludeApplicator")
                        || Int(propTarget, "PropType") != 0
                        || Bool(propTarget, "Heighest")
                        || Bool(propTarget, "SupportCustomFilter"))
                    || custom != null && (Int(custom, "TargetType") != 0
                        || Int(custom, "BaseProp") != 0 || Bool(custom, "BaseCurrentHp")
                        || Int(custom, "DamageProp") != 0
                        || Int(custom, "UpperLimitTarget") != 0
                        || Int(custom, "UpperLimitPropType") != 0
                        || Int(custom, "UpperLimitPercent") != 0)) return false;
            }
            return true;
        }

        private static bool SupportsSkillStart(Dictionary<string, object> component)
        {
            if (Int(component, "SkillStartSource") != 2
                || Int(component, "CD") != 0 || Bool(component, "ResetCDAtStart")
                || Int(component, "MaxCount", -1) < -1
                || Bool(component, "RemoveWhenNotEnoughTimes")
                || ArrayCount(component, "Settings") != 1
                || ArrayCount(component, "Cfgs") > 0
                || ArrayCount(component, "Status") > 0
                || ArrayCount(component, "StatusReplacePropertyValues") > 0) return false;

            Dictionary<string, object> setting = Object(
                Array(component["Settings"], "FightStatusSkillStartComponent.Settings")[0],
                "Fight status skill start setting");
            if (!string.Equals(StringField(setting, "_type"),
                    "StatusSkillConditionActionTypeSetting", StringComparison.Ordinal)
                || Int(setting, "ConditionType") != 7 || Int(setting, "TargetType") != 2)
                return false;
            int[] actionTypes = IntArray(setting, "ActionTypes");
            if (actionTypes.Length == 0) return false;
            foreach (int actionType in actionTypes)
                if (actionType != 2 && actionType != 6) return false;

            if (ArrayCount(component, "Skills") == 0) return false;
            foreach (object item in Array(component["Skills"], "Skills"))
            {
                Dictionary<string, object> trigger = Object(item, "skill start trigger skill");
                Dictionary<string, object> custom = Component(trigger, "CustomDamageInfo");
                Dictionary<string, object> propTarget = Component(trigger, "PropTarget");
                if (Bool(trigger, "AffectedByProp") || Int(trigger, "Source") != 3
                    || Int(trigger, "Target") != 2 || Bool(trigger, "UseAI")
                    || Bool(trigger, "StackedCountToDamageFactor")
                    || Int(trigger, "TargetPos") != 1
                    || Float(trigger, "BasePercent", 1f) < 0f
                    || Float(trigger, "BasePercent", 1f) > 1f
                    || ArrayCount(trigger, "StatusReplacePropertyValues") > 0
                    || ArrayCount(trigger, "TargetWithoutStatusIds") > 0
                    || propTarget != null && (Bool(propTarget, "IsEnemy")
                        || ArrayCount(propTarget, "RoleType") > 0
                        || Bool(propTarget, "IncludeApplicator")
                        || Int(propTarget, "PropType") != 0
                        || Bool(propTarget, "Heighest")
                        || Bool(propTarget, "SupportCustomFilter"))
                    || custom != null && (Int(custom, "TargetType") != 0
                        || Int(custom, "BaseProp") != 0 || Bool(custom, "BaseCurrentHp")
                        || Int(custom, "DamageProp") != 0
                        || Int(custom, "UpperLimitTarget") != 0
                        || Int(custom, "UpperLimitPropType") != 0
                        || Int(custom, "UpperLimitPercent") != 0)) return false;
            }
            return true;
        }

        private static bool SupportsSkillEnd(Dictionary<string, object> component)
        {
            if (Int(component, "MetCount", 1) <= 0
                || Int(component, "StatusEntClassId") != 0
                || Bool(component, "IsCheckSkillCondition")
                || ArrayCount(component, "ElementFilter") > 0
                || ArrayCount(component, "FilterSkillClassIds") > 0
                || ArrayCount(component, "StatusReplacePropertyValues") > 0
                || ArrayCount(component, "StatusCfgs") == 0) return false;
            foreach (object item in Array(component["StatusCfgs"], "StatusCfgs"))
            {
                Dictionary<string, object> trigger = Object(item, "skill end trigger status");
                Dictionary<string, object> propTarget = Component(trigger, "PropTarget");
                if (Bool(trigger, "AffectedByProp")
                    || (Int(trigger, "ApplyTarget") != 2 && Int(trigger, "ApplyTarget") != 3)
                    || Float(trigger, "BasePercent", 1f) < 0f
                    || Float(trigger, "BasePercent", 1f) > 1f
                    || ArrayCount(trigger, "StatusReplacePropertyValues") > 0
                    || propTarget != null && (Bool(propTarget, "IsEnemy")
                        || ArrayCount(propTarget, "RoleType") > 0
                        || Bool(propTarget, "IncludeApplicator")
                        || Int(propTarget, "PropType") != 0
                        || Bool(propTarget, "Heighest")
                        || Bool(propTarget, "SupportCustomFilter"))) return false;
            }
            return true;
        }

        private static int ArrayCount(Dictionary<string, object> source, string field)
        {
            return source.TryGetValue(field, out object value) && value != null
                ? Array(value, field).Count : 0;
        }

        private static bool HasNonNull(Dictionary<string, object> source, string field)
        {
            return source.TryGetValue(field, out object value) && value != null;
        }

        /// <summary>
        /// Reads <c>FightSkillExtSkillComponent.ExtSkill</c> entries that carry a fixed skill id.
        /// Rate-based providers are honoured; other provider types are ignored.
        /// </summary>
        /// <summary>
        /// Reads the status-stack replay condition. Only <c>StatusStackCountFilter</c> with an optional
        /// <c>ConsumeStackStatusHanldeCfg</c> is modelled; other filter/handle types are ignored.
        /// </summary>
        private static void AddSkillReplay(Dictionary<string, object> replay,
            FightBattleSkillDefinition output)
        {
            object raw;
            if (!replay.TryGetValue("FilterCondition", out raw) || !(raw is Dictionary<string, object> filter)
                || StringField(filter, "_type") != "StatusStackCountFilter") return;
            output.HasReplay = true;
            output.ReplayStatusId = Int(filter, "StatusId");
            output.ReplayStackCount = Int(filter, "StackCount");
            output.ReplayRelation = Int(filter, "Relation");
            if (!replay.TryGetValue("HandleCfgs", out object handleValue)
                || !(handleValue is List<object> handles)) return;
            foreach (object item in handles)
            {
                if (!(item is Dictionary<string, object> handle)
                    || StringField(handle, "_type") != "ConsumeStackStatusHanldeCfg") continue;
                output.ReplayConsumeStack = true;
                output.ReplayConsumeCount = Int(handle, "StackCount");
                if (output.ReplayStatusId <= 0) output.ReplayStatusId = Int(handle, "StatusId");
                break;
            }
        }

        private static string StringField(Dictionary<string, object> value, string key)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                ? Convert.ToString(raw, CultureInfo.InvariantCulture) : null;
        }

        private static void AddExtraSkills(Dictionary<string, object> component,
            List<FightBattleSkillExtra> output)
        {
            object raw;
            if (!component.TryGetValue("ExtSkill", out raw) || raw == null) return;
            foreach (object item in Array(raw, "ExtSkill"))
            {
                Dictionary<string, object> row = Object(item, "ext skill");
                if (!row.TryGetValue("ExtSkillIdProvider", out object providerValue)
                    || !(providerValue is Dictionary<string, object> provider)
                    || !provider.ContainsKey("FixedExtSkillId")) continue;
                int skillId = Int(provider, "FixedExtSkillId");
                if (skillId <= 0) continue;
                output.Add(new FightBattleSkillExtra
                {
                    SkillId = skillId,
                    Rate = Float(row, "Rate", 1f)
                });
            }
        }

        private static void AddSourceMoves(Dictionary<string, object> skill,
            List<FightBattleSkillMove> output)
        {
            object raw;
            if (!skill.TryGetValue("SourceMoveList", out raw) || raw == null) return;
            foreach (object item in Array(raw, "SourceMoveList"))
            {
                Dictionary<string, object> row = Object(item, "skill move");
                int offsetX = 0;
                int offsetY = 0;
                if (row.TryGetValue("Offset", out object offsetValue) && offsetValue != null)
                {
                    Dictionary<string, object> offset = Object(offsetValue, "skill move offset");
                    offsetX = Int(offset, "X");
                    offsetY = Int(offset, "Y");
                }
                bool hasConditions = false;
                if (row.TryGetValue("DoMoveConditions", out object conditions) && conditions != null)
                    hasConditions = Array(conditions, "DoMoveConditions").Count > 0;
                output.Add(ParseMove(row, offsetX, offsetY, hasConditions));
            }
            output.Sort((left, right) => left.Delay.CompareTo(right.Delay));
        }

        private FightBattleHitEntity CreateHitEntity(int id, Dictionary<string, object> components,
            Queue<int> pending, Dictionary<int, FightBattleHitReference> references)
        {
            Dictionary<string, object> summon = Component(components, "FightHitSummonComponent");
            Dictionary<string, object> damage = null;
            foreach (string name in new[] { "FightHitSummonComponent", "FightHitChainedComponent",
                "FightHitCustomTargetComponent", "FightHitRandomTargetComponent",
                "FightHitFixedComponent" })
            {
                damage = Component(components, name);
                if (damage != null) break;
            }
            Dictionary<string, object> hemophagia = Component(components, "FightHitExtHemophagiaComponent");
            Dictionary<string, object> chained = Component(components, "FightHitChainedComponent");
            Dictionary<string, object> custom = Component(components, "FightHitCustomTargetComponent");
            Dictionary<string, object> random = Component(components, "FightHitRandomTargetComponent");
            var result = new FightBattleHitEntity
            {
                EntityId = id,
                MiniHitTargetCount = 1,
                HemophagiaProperty = hemophagia == null ? 0 : Int(hemophagia, "HemophagiaPropType")
            };
            if (references.TryGetValue(id, out FightBattleHitReference reference))
            {
                result.Delay = reference.Delay;
                result.Anchor = reference.Anchor;
                result.IgnoreAICount = reference.IgnoreAICount;
            }
            if (damage != null)
            {
                result.HasFixedDamage = Int(damage, "DamageId") > 0
                    || Int(damage, "DamageType") != 0;
                result.DelayDistFactor = Float(damage, "DelayDistFactor", 0f);
                result.ActionType = Int(damage, "DamageActionType", 4);
                result.DamageId = Int(damage, "DamageId");
                AddStatusApplications(result.DamageId, result);
                AddDisperseStatus(result.DamageId, result);
                AddChildSkill(result.DamageId, result);
                AddHitMove(result.DamageId, result);
                result.DamageType = Int(damage, "DamageType");
                result.BasicProperty = Int(damage, "DamageBasicProp", 1);
                result.DamageProperty = Int(damage, "DamageProp");
                result.FixedDamageProperty = Int(damage, "FixedDamageProp");
                result.DamageParam = Long(damage, "DamageParam");
                result.IgnoreShield = Bool(damage, "DamageIgnoreShield");
                result.TrueDamage = Bool(damage, "IsTrueDamage");
                result.BreakResilience = Bool(damage, "IsBreakResilience");
            }
            if (summon != null) AddSummonConfig(summon, result);
            foreach (Dictionary<string, object> holder in new[] { chained, custom, random, damage })
            {
                if (holder == null || !holder.ContainsKey("Scope")) continue;
                result.AllowEmptyScope = Bool(holder, "AllowEmptyScope");
                foreach (object scopeItem in Array(holder["Scope"], "hit scope"))
                {
                    Dictionary<string, object> scope = Object(scopeItem, "hit scope entry");
                    object rawPos;
                    if (!scope.TryGetValue("Pos", out rawPos) || rawPos == null) continue;
                    Dictionary<string, object> pos = Object(rawPos, "hit scope position");
                    result.Scope.Add(new FightGridPoint(Int(pos, "X"), Int(pos, "Y")));
                }
                break;
            }
            if (chained != null) AddIntArray(chained, "NextHitList", result.ChainedHits, pending, references);
            if (custom != null) AddIntArray(custom, "HitList", result.CustomTargetHits, pending, references);
            if (random != null)
            {
                AddIntArray(random, "HitList", result.RandomTargetHits, pending, references);
                result.MiniHitTargetCount = Int(random, "MiniHitTargetCount", 1);
            }
            return result;
        }

        private static void AddSummonConfig(Dictionary<string, object> component,
            FightBattleHitEntity output)
        {
            if (component == null || output == null) return;
            output.IsSummon = true;
            if (!component.TryGetValue("SummonCfg", out object rawConfig) || rawConfig == null)
                return;
            Dictionary<string, object> config = Object(rawConfig, "FightHitSummonComponent.SummonCfg");
            output.SummonImmediateRound = Bool(config, "SummonImmediateRound");
            output.SummonInheritProps = Bool(config, "InheritProp");
            output.SummonReferenceType = Int(config, "ReferenceType");
            output.SummonLevelType = Int(config, "LevelType");
            output.SummonLifespanStatusId = Int(config, "LifespanStatusId");
            if (config.TryGetValue("SummonStatus", out object rawStatusIds) && rawStatusIds != null)
                output.SummonStatusIds.AddRange(IntArray(config, "SummonStatus"));
            if (config.TryGetValue("SummonPoolSetting", out object rawSetting) && rawSetting != null)
            {
                Dictionary<string, object> setting = Object(rawSetting, "SummonPoolSetting");
                output.SummonIsRandom = Bool(setting, "IsRandom");
                if (setting.TryGetValue("SummonPools", out object rawPools) && rawPools != null)
                    foreach (object rawPool in Array(rawPools, "SummonPoolSetting.SummonPools"))
                    {
                        Dictionary<string, object> pool = Object(rawPool, "SummonPoolStatusCfg");
                        Dictionary<string, object> poolConfig = Component(pool, "SummonPoolCfg");
                        if (poolConfig == null) poolConfig = pool;
                        var definition = new FightBattleSummonPool
                        {
                            MonsterId = Int(poolConfig, "SummonId"),
                            MaxSummonCount = Int(poolConfig, "MaxSummonCount")
                        };
                        if (pool.TryGetValue("SummonStatus", out object rawPoolStatuses)
                            && rawPoolStatuses != null)
                            definition.StatusIds.AddRange(IntArray(pool, "SummonStatus"));
                        output.SummonPools.Add(definition);
                    }
            }
            int directId = Int(config, "SummonId");
            if (output.SummonPools.Count == 0 && directId > 0)
            {
                var pool = new FightBattleSummonPool
                {
                    MonsterId = directId,
                    MaxSummonCount = int.MaxValue
                };
                pool.StatusIds.AddRange(output.SummonStatusIds);
                output.SummonPools.Add(pool);
            }
            if (component.TryGetValue("SummonScope", out object rawScopes) && rawScopes != null)
                foreach (object rawScope in Array(rawScopes,
                    "FightHitSummonComponent.SummonScope"))
                {
                    Dictionary<string, object> scope = Object(rawScope, "summon scope");
                    Dictionary<string, object> baseScope = Component(scope, "Base");
                    Dictionary<string, object> position = baseScope == null
                        ? null : Component(baseScope, "Pos");
                    if (position == null) continue;
                    Dictionary<string, object> offset = Component(scope, "SummonOffset");
                    output.SummonPositions.Add(new FightGridPoint(
                        Int(position, "X") + (offset == null ? 0 : Int(offset, "X")),
                        Int(position, "Y") + (offset == null ? 0 : Int(offset, "Y"))));
                }
        }

        private static void AddHitChildren(Dictionary<string, object> skill, Queue<int> pending,
            List<FightBattleHitRoot> roots, List<int> rootIds,
            Dictionary<int, FightBattleHitReference> references)
        {
            foreach (object item in Array(Required(skill, "HitList"), "skill HitList"))
            {
                Dictionary<string, object> entry = Object(item, "skill hit reference");
                int id = Int(entry, "ClassId");
                var root = new FightBattleHitRoot
                {
                    EntityId = id,
                    Delay = Float(entry, "Delay", 0f),
                    Anchor = Int(entry, "ActOnSouce"),
                    IgnoreAICount = Bool(entry, "IgnoreAICount")
                };
                roots.Add(root);
                rootIds.Add(id);
                pending.Enqueue(id);
                AddReference(references, id, root.Delay, root.Anchor, root.IgnoreAICount);
            }
        }

        private static void AddReference(Dictionary<int, FightBattleHitReference> references, int id,
            float delay, int anchor, bool ignoreAICount)
        {
            if (id <= 0 || references.ContainsKey(id)) return;
            references.Add(id, new FightBattleHitReference
            {
                Delay = delay,
                Anchor = anchor,
                IgnoreAICount = ignoreAICount
            });
        }

        private static void AddIntArray(Dictionary<string, object> component, string field,
            List<int> output, Queue<int> pending, Dictionary<int, FightBattleHitReference> references)
        {
            object raw;
            if (!component.TryGetValue(field, out raw) || raw == null) return;
            foreach (object item in Array(raw, field))
            {
                Dictionary<string, object> reference = item as Dictionary<string, object>;
                int id = reference == null
                    ? Convert.ToInt32(item, CultureInfo.InvariantCulture)
                    : Int(reference, "ClassId");
                output.Add(id);
                pending.Enqueue(id);
                if (reference != null)
                    AddReference(references, id, Float(reference, "Delay", 0f),
                        Int(reference, "ActOnSouce"), Bool(reference, "IgnoreAICount"));
            }
        }

        private sealed class FightBattleHitReference
        {
            public float Delay;
            public int Anchor;
            public bool IgnoreAICount;
        }

        private static float Float(Dictionary<string, object> value, string key, float fallback)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                ? Convert.ToSingle(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static List<FightPropertyValue> CopyProperties(IList<FightPropertyValue> properties)
        {
            var result = new List<FightPropertyValue>();
            if (properties == null) return result;
            foreach (FightPropertyValue value in properties)
                if (value != null) result.Add(new FightPropertyValue { Id = value.Id, Value = value.Value });
            return result;
        }

        private static void AddTable(List<FightFormulaTableRow> output,
            Dictionary<string, object> tables, string name, string keyField, string valueField)
        {
            foreach (object item in Array(Required(tables, name), name))
            {
                Dictionary<string, object> row = Object(item, name + " row");
                output.Add(new FightFormulaTableRow
                {
                    Key = Int(row, keyField),
                    Value = Int(row, valueField)
                });
            }
        }

        private static void VerifyPayload(string name, string json, string expectedSha256, string manifest)
        {
            VerifyPayload(name, json, expectedSha256, manifest, ExpectedManifest);
        }

        private static void VerifyPayload(string name, string json, string expectedSha256,
            string manifest, string expectedManifest)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException(name + " payload is empty.", nameof(json));
            if (!string.Equals(manifest, expectedManifest, StringComparison.Ordinal))
                throw new InvalidOperationException(name + " source manifest is not the pinned original version.");
            string actual;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                actual = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(name + " SHA-256 differs from its source manifest.");
        }

        private static Dictionary<string, object> Component(Dictionary<string, object> value, string key)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null ? Object(raw, key) : null;
        }

        private static List<FightPropertyValue> CopyProperties(IEnumerable<FightPropertyValue> properties)
        {
            if (properties == null) return null;
            var result = new List<FightPropertyValue>();
            foreach (FightPropertyValue property in properties)
                if (property != null) result.Add(new FightPropertyValue
                    { Id = property.Id, Value = property.Value });
            return result;
        }

        private static object Required(Dictionary<string, object> value, string key)
        {
            object result;
            if (!value.TryGetValue(key, out result) || result == null)
                throw new FormatException("Official combat field is missing: " + key);
            return result;
        }

        private static Dictionary<string, object> Object(object value, string path)
        {
            Dictionary<string, object> result = value as Dictionary<string, object>;
            if (result == null) throw new FormatException("Expected JSON object at " + path);
            return result;
        }

        private static List<object> Array(object value, string path)
        {
            List<object> result = value as List<object>;
            if (result == null) throw new FormatException("Expected JSON array at " + path);
            return result;
        }

        private static bool JsonEqual(object left, object right)
        {
            Dictionary<string, object> leftObject = left as Dictionary<string, object>;
            Dictionary<string, object> rightObject = right as Dictionary<string, object>;
            if (leftObject != null || rightObject != null)
            {
                if (leftObject == null || rightObject == null || leftObject.Count != rightObject.Count) return false;
                foreach (KeyValuePair<string, object> pair in leftObject)
                {
                    object value;
                    if (!rightObject.TryGetValue(pair.Key, out value) || !JsonEqual(pair.Value, value)) return false;
                }
                return true;
            }
            List<object> leftArray = left as List<object>;
            List<object> rightArray = right as List<object>;
            if (leftArray != null || rightArray != null)
            {
                if (leftArray == null || rightArray == null || leftArray.Count != rightArray.Count) return false;
                for (int i = 0; i < leftArray.Count; i++)
                    if (!JsonEqual(leftArray[i], rightArray[i])) return false;
                return true;
            }
            return Equals(left, right);
        }

        private static int Int(Dictionary<string, object> value, string key, int fallback = 0)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                ? Convert.ToInt32(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static int IntKey(string value, string path)
        {
            int result;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                throw new FormatException("Invalid integer at " + path + ": " + value);
            return result;
        }

        private static long Long(Dictionary<string, object> value, string key, long fallback = 0)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                ? Convert.ToInt64(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static double Number(Dictionary<string, object> value, string key, double fallback)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                ? Convert.ToDouble(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static bool Bool(Dictionary<string, object> value, string key, bool fallback = false)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                ? Convert.ToBoolean(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static int[] IntArray(Dictionary<string, object> value, string key)
        {
            object raw;
            if (!value.TryGetValue(key, out raw) || raw == null) return System.Array.Empty<int>();
            List<object> items = Array(raw, key);
            var result = new int[items.Count];
            for (int i = 0; i < result.Length; i++) result[i] = Convert.ToInt32(items[i], CultureInfo.InvariantCulture);
            return result;
        }

        private static List<List<int>> IntGroups(Dictionary<string, object> value, string key)
        {
            object raw;
            var result = new List<List<int>>();
            if (!value.TryGetValue(key, out raw) || raw == null) return result;
            foreach (object item in Array(raw, key))
            {
                Dictionary<string, object> group = Object(item, key + " entry");
                result.Add(new List<int>(IntArray(group, "CharacterTypes")));
            }
            return result;
        }
    }

    public sealed class FightOfficialSkillBinding
    {
        public int EntityId;
        public int Slot;
        public int ItemId;
        public int Rank;
        public int Level;
        public int SubRank;
        public FightSkillProfessionAvailability ProfessionAvailability;
        public long Cooldown;
        public int LastRound;
        public int ReleasedTimes;
        public long SkillCost;
        public IList<FightPropertyValue> Properties;
    }

    public sealed class FightOfficialStatusBinding
    {
        public int ClassId;
        public int InstanceId;
        public int CreatorId;
        public int RootSkillId;
        public int ApplyingSkillItemId;
        public int SkillRank = 1;
        public int SkillLevel = 1;
        public int SourceSubRank;
        public IList<FightPropertyValue> SourceProperties;
        public long CustomShieldValue;
    }

    public sealed class FightOfficialPassiveSkillBinding
    {
        public int ItemId;
        public int Rank = 1;
        public int Level = 1;
        public int SubRank;
    }

    internal sealed class OfficialJson
    {
        private readonly string text;
        private int position;

        private OfficialJson(string text) { this.text = text; }

        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var parser = new OfficialJson(text);
            object value = parser.Value();
            parser.Space();
            if (parser.position != text.Length) throw parser.Error("Trailing JSON content");
            return value;
        }

        private object Value()
        {
            Space();
            if (position >= text.Length) throw Error("Unexpected end of JSON");
            char ch = text[position];
            if (ch == '{') return ObjectValue();
            if (ch == '[') return ArrayValue();
            if (ch == '"') return StringValue();
            if (ch == 't') { Literal("true"); return true; }
            if (ch == 'f') { Literal("false"); return false; }
            if (ch == 'n') { Literal("null"); return null; }
            return NumberValue();
        }

        private Dictionary<string, object> ObjectValue()
        {
            position++;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            Space();
            if (Take('}')) return result;
            while (true)
            {
                Space();
                if (position >= text.Length || text[position] != '"') throw Error("Expected object key");
                string key = StringValue();
                Space();
                Expect(':');
                if (result.ContainsKey(key)) throw Error("Duplicate object key " + key);
                result.Add(key, Value());
                Space();
                if (Take('}')) return result;
                Expect(',');
            }
        }

        private List<object> ArrayValue()
        {
            position++;
            var result = new List<object>();
            Space();
            if (Take(']')) return result;
            while (true)
            {
                result.Add(Value());
                Space();
                if (Take(']')) return result;
                Expect(',');
            }
        }

        private string StringValue()
        {
            Expect('"');
            var result = new StringBuilder();
            while (position < text.Length)
            {
                char ch = text[position++];
                if (ch == '"') return result.ToString();
                if (ch < 0x20) throw Error("Control character in string");
                if (ch != '\\') { result.Append(ch); continue; }
                if (position >= text.Length) throw Error("Incomplete string escape");
                char escaped = text[position++];
                switch (escaped)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (position + 4 > text.Length) throw Error("Incomplete Unicode escape");
                        result.Append((char)int.Parse(text.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        position += 4;
                        break;
                    default: throw Error("Invalid string escape");
                }
            }
            throw Error("Unterminated string");
        }

        private object NumberValue()
        {
            int start = position;
            if (Take('-')) { }
            while (position < text.Length && char.IsDigit(text[position])) position++;
            bool floating = false;
            if (Take('.')) { floating = true; while (position < text.Length && char.IsDigit(text[position])) position++; }
            if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
            {
                floating = true;
                position++;
                if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++;
                while (position < text.Length && char.IsDigit(text[position])) position++;
            }
            if (start == position) throw Error("Invalid JSON value");
            string value = text.Substring(start, position - start);
            if (floating) return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            long integer;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)
                ? (object)integer : double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private void Literal(string value)
        {
            if (position + value.Length > text.Length || string.CompareOrdinal(text, position, value, 0, value.Length) != 0)
                throw Error("Invalid JSON literal");
            position += value.Length;
        }

        private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        private bool Take(char value) { if (position < text.Length && text[position] == value) { position++; return true; } return false; }
        private void Expect(char value) { if (!Take(value)) throw Error("Expected '" + value + "'"); }
        private FormatException Error(string message) { return new FormatException(message + " at offset " + position); }
    }
}
