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
            VerifyPayload("skill-rules.json", skillRulesJson, skillRulesSha256, skillManifest);
            VerifyPayload("battle-rules.json", battleRulesJson, battleRulesSha256, battleManifest);
            var catalog = new FightOfficialCombatCatalog();
            Dictionary<string, object> skillCatalog = Object(OfficialJson.Parse(skillRulesJson), "skill-rules.json");
            Dictionary<string, object> battleCatalog = Object(OfficialJson.Parse(battleRulesJson), "battle-rules.json");
            catalog.AddEntities(skillCatalog);
            catalog.AddEntities(battleCatalog);
            catalog.skillPropertyNames = Object(Required(skillCatalog, "prop_names"), "prop_names");
            catalog.battleTables = Object(Required(battleCatalog, "tables"), "battle tables");
            catalog.ParseSkillPropertyTables(skillCatalog);
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
                SkillTargetType = Int(skill, "SkillTargetType"),
                ElementType = Int(skill, "ElementType"),
                LimitedTimes = Int(skill, "LimitedTimes", -1),
                ResetCooldownAtStart = Bool(skill, "ResetCDAtStart"),
                TargetType = Int(skill, "TargetType", 1),
                Range = range,
                CanTriggerChild = Bool(skill, "CanTriggerChild", true),
                ResetReleasedTimeOnNextWave = Bool(skill, "ResetReleasedTimeOnNextWave"),
                HasCustomDamage = false,
                RangeVerified = true,
                PriorityProperty = ai == null ? 0 : Int(ai, "AiPriorityPropType"),
                PriorityFindMax = ai == null || Bool(ai, "AiPriorityFindMax", true),
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
                        binding.InstanceId, binding.RootSkillId, binding.ApplyingSkillItemId));
                }
            }
            unit.Skills.AddRange(assembledSkills);
            unit.SkillState.Slots.AddRange(assembledSlots);
            unit.Statuses.AddRange(assembledStatuses);
        }

        public FightBattleStatusState CreateStatus(int classId, int ownerId, int creatorId,
            int instanceId = 0, int rootSkillId = 0, int applyingSkillItemId = 0)
        {
            Dictionary<string, object> components = RequiredEntity(classId);
            Dictionary<string, object> status = Component(components, "FightStatusComponent");
            Dictionary<string, object> falloff = Component(components, "FightStatusDamageFalloffComponent");
            Dictionary<string, object> hpDecrease =
                Component(components, "FightStatusHpDecreaseUnitComponent");
            if (status == null) throw new InvalidOperationException("Official entity is not a status: " + classId);

            int durationRound = Int(status, "DurationRound");
            var result = new FightBattleStatusState
            {
                InstanceId = instanceId,
                ClassId = classId,
                StatusType = Int(status, "StatusType"),
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
                Rate = 1.0,
                NumOfStart = 1,
                MaxFalloffCount = -1
            };
            if (hpDecrease != null)
            {
                AddHpTriggerStatuses(hpDecrease, result.HpTriggerStatuses);
                AddHpTriggerSkills(hpDecrease, result.HpTriggerSkills);
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

        private Dictionary<int, string> monsterNames;

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
                    LessThanHpPer = Float(row, "LessThanHpPer", -1f)
                });
            }
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
                HasConditions = conditions
            };
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
                result.HasFixedDamage = true;
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
            }
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
            if (string.IsNullOrEmpty(json)) throw new ArgumentException(name + " payload is empty.", nameof(json));
            if (!string.Equals(manifest, ExpectedManifest, StringComparison.Ordinal))
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
    }

    public sealed class FightOfficialSkillBinding
    {
        public int EntityId;
        public int Slot;
        public int ItemId;
        public int Rank;
        public int Level;
        public int SubRank;
        public long Cooldown;
        public int LastRound;
        public int ReleasedTimes;
        public IList<FightPropertyValue> Properties;
    }

    public sealed class FightOfficialStatusBinding
    {
        public int ClassId;
        public int InstanceId;
        public int CreatorId;
        public int RootSkillId;
        public int ApplyingSkillItemId;
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
