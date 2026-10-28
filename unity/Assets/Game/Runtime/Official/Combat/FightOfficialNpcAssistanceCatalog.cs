using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public sealed class FightOfficialNpcInviteRule
    {
        public int PlayType;
        public int OneNpcMaxInviteCount;
        public int MaxInviteCount;
        public int RefreshCycle;
        public int MaxPlayer;
    }

    public sealed class FightOfficialNpcAssistanceCatalog
    {
        public const string ExpectedManifest = "871_156598";
        private const int CompanionNpcType = 4;
        private readonly Dictionary<int, Dictionary<string, object>> npcs = new Dictionary<int, Dictionary<string, object>>();
        private readonly Dictionary<int, List<Dictionary<string, object>>> levelDetails =
            new Dictionary<int, List<Dictionary<string, object>>>();
        private readonly Dictionary<int, Dictionary<string, object>> professions =
            new Dictionary<int, Dictionary<string, object>>();
        private readonly Dictionary<int, Dictionary<string, object>> inviteRules =
            new Dictionary<int, Dictionary<string, object>>();
        private readonly Dictionary<int, Dictionary<string, object>> conditions =
            new Dictionary<int, Dictionary<string, object>>();
        private readonly Dictionary<int, int> itemRanks = new Dictionary<int, int>();
        private readonly FightOfficialCombatCatalog combat;
        private readonly FightOfficialSkillItemCatalog skillItems;
        private readonly FightDamageFormula formula;

        private FightOfficialNpcAssistanceCatalog(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightDamageFormula formula)
        {
            this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
            this.skillItems = skillItems ?? throw new ArgumentNullException(nameof(skillItems));
            this.formula = formula ?? throw new ArgumentNullException(nameof(formula));
        }

        public static FightOfficialNpcAssistanceCatalog Load(string rulesJson, string rulesSha256,
            string manifest, FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog skillItems,
            FightDamageFormula formula)
        {
            FightOfficialCombatCatalog.VerifyPayloadForDerived("npc-assistance-rules.json", rulesJson,
                rulesSha256, manifest, ExpectedManifest);
            Dictionary<string, object> rules = FightOfficialCombatCatalog.ParseObject(rulesJson,
                "npc-assistance-rules.json");
            if (!rules.TryGetValue("manifest", out object payloadManifest)
                || !string.Equals(Convert.ToString(payloadManifest, CultureInfo.InvariantCulture),
                    manifest, StringComparison.Ordinal))
                throw new FormatException("Official NPC assistance payload manifest differs from its receipt.");
            var catalog = new FightOfficialNpcAssistanceCatalog(combat, skillItems, formula);
            foreach (Dictionary<string, object> row in Rows(rules, "conditions"))
                catalog.conditions.Add(Int(row, "ClassId"), FightOfficialCombatCatalog.ParseObject(
                    row.TryGetValue("Condition", out object condition) ? condition : null,
                    "condition row"));
            foreach (Dictionary<string, object> row in Rows(rules, "npc"))
                catalog.npcs.Add(Int(row, "Id"), row);
            foreach (Dictionary<string, object> row in Rows(rules, "npc_level_detail"))
            {
                int classId = Int(row, "ClassId");
                if (!catalog.levelDetails.TryGetValue(classId, out List<Dictionary<string, object>> list))
                    catalog.levelDetails.Add(classId, list = new List<Dictionary<string, object>>());
                list.Add(row);
            }
            foreach (Dictionary<string, object> row in Rows(rules, "npc_profession_detail"))
                catalog.professions.Add(Int(row, "ClassId"), row);
            foreach (Dictionary<string, object> row in Rows(rules, "player_robot_invite"))
                catalog.inviteRules.Add(Int(row, "PlayType"), row);
            foreach (Dictionary<string, object> row in Rows(rules, "skill"))
                catalog.itemRanks[Int(row, "ClassId")] = Int(row, "Rank");
            catalog.Validate();
            return catalog;
        }

        public IReadOnlyList<int> FightAssistNpcClassIds()
        {
            return npcs.Values.Where(row => Int(row, "NpcType") == CompanionNpcType
                    && Bool(row, "CanDevelop")
                    && Int(row, "FightEntityId") > 0
                    && levelDetails.ContainsKey(Int(row, "Id")))
                .Select(row => Int(row, "Id")).OrderBy(id => id).ToArray();
        }

        // TeamInviteNpc calls GetNpcs(MapExploreBoss), whose argument is an
        // EXCLUDED type. NpcInfo.IsFightAssist then checks entity + level data.
        // Preserve source order; the original invitation panel sorts afterwards.
        public IReadOnlyList<int> TeamInviteNpcClassIds()
        {
            return npcs.Values.Where(row => Int(row, "NpcType") != 2
                    && Int(row, "FightEntityId") > 0
                    && levelDetails.ContainsKey(Int(row, "Id")))
                .Select(row => Int(row, "Id")).ToArray();
        }

        public bool CanInviteTeamNpc(int npcClassId, bool encountered, bool meetsAssistCondition)
        {
            if (!npcs.TryGetValue(npcClassId, out Dictionary<string, object> npc)
                || Int(npc, "NpcType") == 2 || Int(npc, "FightEntityId") <= 0
                || !levelDetails.ContainsKey(npcClassId) || !meetsAssistCondition)
                return false;
            return !Bool(npc, "IsOnlyMeetCanAssist") || encountered;
        }

        public string NpcNameKey(int npcClassId) => RequiredNpc(npcClassId, "NameKey");

        public string NpcIconPath(int npcClassId) => RequiredNpc(npcClassId, "Icon");

        public bool RequiresEncounter(int npcClassId) => Bool(RequiredNpcRow(npcClassId), "IsOnlyMeetCanAssist");

        public Dictionary<string, object> AssistCondition(int npcClassId)
        {
            Dictionary<string, object> npc = RequiredNpcRow(npcClassId);
            if (!npc.TryGetValue("AssistCondition", out object condition) || condition == null)
                throw new FormatException("Official NPC assist condition is missing: " + npcClassId);
            return FightOfficialCombatCatalog.ParseObject(condition, "NPC assist condition " + npcClassId);
        }

        public bool CanInviteNpc(int npcClassId, bool encountered, bool meetsAssistCondition)
        {
            if (!npcs.TryGetValue(npcClassId, out Dictionary<string, object> npc)
                || Int(npc, "NpcType") != CompanionNpcType || !Bool(npc, "CanDevelop")
                || Int(npc, "FightEntityId") <= 0
                || !levelDetails.ContainsKey(npcClassId) || !meetsAssistCondition)
                return false;
            return !Bool(npc, "IsOnlyMeetCanAssist") || encountered;
        }

        public bool AssistConditionMet(int npcClassId, int playerLevel,
            IReadOnlyDictionary<int, int> friendshipLevels, Func<string, int> storyCount)
        {
            return npcs.TryGetValue(npcClassId, out Dictionary<string, object> npc)
                && npc.TryGetValue("AssistCondition", out object raw)
                && raw is Dictionary<string, object> condition
                && EvaluateAssistCondition(condition, playerLevel, friendshipLevels, storyCount,
                    new HashSet<int>());
        }

        private bool EvaluateAssistCondition(Dictionary<string, object> condition, int playerLevel,
            IReadOnlyDictionary<int, int> friendshipLevels, Func<string, int> storyCount,
            HashSet<int> visiting)
        {
            if (condition == null) return false;
            switch (Text(condition, "_type"))
            {
                case "NoneCondition":
                    return true;
                case "LevelCondition":
                    return Compare(playerLevel, Int(condition, "Level"), Text(condition, "Op"));
                case "NpcFriendshipLevelCondition":
                    int npcId = Int(condition, "NpcClassId");
                    int level = friendshipLevels != null && friendshipLevels.TryGetValue(npcId,
                        out int friendshipLevel) ? friendshipLevel : 0;
                    bool met = level >= Int(condition, "FriendshipLevel");
                    return Bool(condition, "IsReverse") ? !met : met;
                case "CompositeAndCondition":
                    foreach (int id in IntArray(condition, "Ids"))
                        if (!EvaluateAssistConditionId(id, playerLevel, friendshipLevels, storyCount, visiting))
                            return false;
                    return true;
                case "CompositeOrCondition":
                    foreach (int id in IntArray(condition, "Ids"))
                        if (EvaluateAssistConditionId(id, playerLevel, friendshipLevels, storyCount, visiting))
                            return true;
                    return false;
                case "StoryCountCondition":
                    return storyCount != null && Compare(storyCount(Text(condition, "StoryKey")),
                        Int(condition, "Count"), Text(condition, "Op"));
                default:
                    return false;
            }
        }

        private bool EvaluateAssistConditionId(int conditionId, int playerLevel,
            IReadOnlyDictionary<int, int> friendshipLevels, Func<string, int> storyCount,
            HashSet<int> visiting)
        {
            if (conditionId <= 0 || !visiting.Add(conditionId)) return false;
            try
            {
                return conditions.TryGetValue(conditionId, out Dictionary<string, object> condition)
                    && EvaluateAssistCondition(condition, playerLevel, friendshipLevels, storyCount, visiting);
            }
            finally
            {
                visiting.Remove(conditionId);
            }
        }

        public FightOfficialNpcInviteRule InviteRule(int playType)
        {
            if (!inviteRules.TryGetValue(playType, out Dictionary<string, object> row))
                throw new KeyNotFoundException("Official NPC invitation rule is missing: " + playType);
            return new FightOfficialNpcInviteRule
            {
                PlayType = playType,
                OneNpcMaxInviteCount = Int(row, "OnePlayerMaxInviteCount"),
                MaxInviteCount = Int(row, "MaxInviteCount", -1),
                RefreshCycle = Int(row, "CdType"),
                MaxPlayer = Int(row, "MaxPlayer")
            };
        }

        public FightBattleUnitState CreateUnit(int unitId, int masterId, int level, int rank,
            int subRank, int x, int y, int moveMask, int npcClassId)
        {
            return CreateNpcUnit(unitId, masterId, level, rank, subRank, x, y, moveMask, npcClassId, false);
        }

        public FightBattleUnitState CreateTeamInviteUnit(int unitId, int masterId, int level, int rank,
            int subRank, int x, int y, int moveMask, int npcClassId)
        {
            return CreateNpcUnit(unitId, masterId, level, rank, subRank, x, y, moveMask, npcClassId, true);
        }

        private FightBattleUnitState CreateNpcUnit(int unitId, int masterId, int level, int rank,
            int subRank, int x, int y, int moveMask, int npcClassId, bool teamInvite)
        {
            if (unitId <= 0 || masterId <= 0 || level <= 0 || rank < 0 || subRank < 0
                || moveMask <= 0 || npcClassId <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitId), "Official NPC battle unit inputs are invalid.");
            if (!(teamInvite ? CanInviteTeamNpc(npcClassId, true, true) : CanInviteNpc(npcClassId, true, true)))
                throw new InvalidOperationException("NPC is not configured as an assist combatant: " + npcClassId);

            Dictionary<string, object> detail = ResolveLevelDetail(npcClassId, rank, level,
                out int effectiveLevel);
            int professionId = Int(detail, "ProfessionDetailId");
            if (!professions.TryGetValue(professionId, out Dictionary<string, object> profession))
                throw new InvalidOperationException("Official NPC profession detail is missing: " + professionId);
            Dictionary<int, long> factors = LongMap(profession, "PropFactors");
            Dictionary<int, long> extras = LongMap(profession, "ExtraProps");
            IReadOnlyDictionary<int, long> calculated = combat.CalculateNpcProps(Int(detail, "PropGroupId"),
                factors, extras, effectiveLevel, subRank);
            var properties = new Dictionary<int, long>(calculated);
            var active = ReadSkillList(profession, "ActiveSkills", "ActiveSkillRanks", effectiveLevel,
                Int(profession, "SkillLevelOffset"));
            var passives = ReadSkillList(profession, "PassiveSkills", "PassiveSkillRanks", effectiveLevel,
                Int(profession, "SkillLevelOffset"));

            foreach (NpcSkillBinding passive in passives)
            {
                FightOfficialSkillItem item = skillItems.Required(passive.ItemId);
                if (item.PassiveRankPropId <= 0 && item.PassiveGroupLevelPropId <= 0) continue;
                IReadOnlyDictionary<int, long> additions = combat.CalculatePassiveSkillProps(
                    item.PassiveRankPropId, item.PassiveGroupLevelPropId, item.PassiveRankFactorId,
                    item.PassivePropFactors, passive.Rank, passive.Level, subRank);
                Merge(properties, additions);
            }
            ApplyFinalPassiveProperties(properties);

            int maxHpProperty = combat.PropertyType("MaxHp");
            if (maxHpProperty <= 0 || !properties.TryGetValue(maxHpProperty, out long maxHp) || maxHp <= 0)
                throw new InvalidOperationException("Official NPC MaxHp is missing: " + npcClassId);
            var unit = new FightBattleUnitState
            {
                Id = unitId,
                MasterId = masterId,
                Side = 1,
                CharacterType = 1,
                NpcClassId = npcClassId,
                Level = effectiveLevel,
                SubRank = subRank,
                Profession = Int(profession, "Profession"),
                X = x,
                Y = y,
                Hp = maxHp,
                MaxHp = maxHp,
                LastMaxHp = maxHp,
                MoveMask = moveMask,
                Properties = properties.Select(value => new FightPropertyValue
                    { Id = value.Key, Value = value.Value }).ToList()
            };
            unit.MoveDist = combat.CalculateMoveDist(properties);
            unit.Turn.Id = unit.Id;
            unit.Turn.MasterId = unit.MasterId;
            unit.Turn.Hp = unit.Hp;
            int speedProperty = combat.PropertyType("Speed");
            if (speedProperty <= 0 || !properties.TryGetValue(speedProperty, out long speed)
                || speed < 0 || speed > Int32.MaxValue)
                throw new InvalidOperationException("Official NPC Speed is missing or invalid: " + npcClassId);
            unit.Turn.Speed = (int)speed;

            var activeBindings = new List<FightOfficialSkillBinding>();
            foreach (NpcSkillBinding skill in active)
            {
                FightOfficialSkillItem item = skillItems.Required(skill.ItemId);
                if (item.EntityId <= 0)
                    throw new InvalidOperationException("Official NPC active skill has no battle entity: " + skill.ItemId);
                activeBindings.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId,
                    ItemId = item.ItemId,
                    Slot = skill.Slot,
                    Rank = skill.Rank,
                    Level = skill.Level,
                    SubRank = subRank,
                    Cooldown = combat.CalculateSkillCooldown(item.EntityId, skill.Rank, skill.Level,
                        subRank, properties),
                    Properties = combat.CalculateSkillProps(item.EntityId, skill.Rank, skill.Level, subRank)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList()
                });
            }
            var passiveBindings = passives.Select(value => new FightOfficialPassiveSkillBinding
            {
                ItemId = value.ItemId,
                Rank = value.Rank,
                Level = value.Level,
                SubRank = subRank
            }).ToList();
            List<FightOfficialStatusBinding> statuses = combat.CreatePassiveStatusBindings(unit, skillItems,
                passiveBindings);
            combat.AssembleUnit(unit, activeBindings, statuses);
            if (unit.Skills.Count == 0)
                throw new InvalidOperationException("Official NPC has no active battle skills: " + npcClassId);
            return unit;
        }

        private List<NpcSkillBinding> ReadSkillList(Dictionary<string, object> profession, string idsKey,
            string ranksKey, int level, int levelOffset)
        {
            int[] ids = IntArray(profession, idsKey);
            int[] ranks = IntArray(profession, ranksKey);
            if (ids.Length != ranks.Length)
                throw new FormatException("Official NPC skill and rank list lengths differ: " + idsKey);
            var result = new List<NpcSkillBinding>(ids.Length);
            for (int index = 0; index < ids.Length; index++)
            {
                if (ids[index] <= 0) continue;
                if (!itemRanks.TryGetValue(ids[index], out int minimumRank))
                    throw new KeyNotFoundException("Official NPC skill item is missing: " + ids[index]);
                result.Add(new NpcSkillBinding
                {
                    Slot = index,
                    ItemId = ids[index],
                    Rank = Math.Max(ranks[index], minimumRank),
                    Level = Math.Max(1, checked(level + levelOffset))
                });
            }
            return result;
        }

        private void ApplyFinalPassiveProperties(Dictionary<int, long> properties)
        {
            ApplyScale("MaxHpScale", "BaseMaxHp", "MaxHp");
            ApplyScale("AttackScale", "BaseAttack", "Attack");
            ApplyScale("DefenceScale", "BaseDefence", "Defence");
            ApplyScale("SpeedScale", "BaseSpeed", "Speed");
            ApplyScale("EffectDodgeScale", "BaseEffectDodge", "EffectDodge");
            ApplyFixed("FixedAttack", "Attack");
            ApplyFixed("FixedDefence", "Defence");
            ApplyFixed("FixedMaxHp", "MaxHp");
            ApplyFixed("FixedSpeed", "Speed");
            ApplyFixed("FixedDmgReduce", "DmgReduce");
            ApplyFixed("FixedDmgAdd", "DmgAdd");
            ApplyFixed("FixedCritPower", "CritPower");
            ApplyFixed("FixedBlockValue", "BlockValue");
            ApplyFixed("FixedMoveDist", "MoveDist");

            int finalHpScaleId = combat.PropertyType("FinalHpScale");
            int maxHpId = combat.PropertyType("MaxHp");
            if (finalHpScaleId > 0 && maxHpId > 0
                && properties.TryGetValue(finalHpScaleId, out long scaleValue))
            {
                double scale = formula.ReadProperty(properties, finalHpScaleId);
                properties[maxHpId] = (long)(Property(properties, maxHpId) * (1.0 + scale));
                properties.Remove(finalHpScaleId);
            }

            void ApplyScale(string scaleName, string baseName, string targetName)
            {
                int scaleId = combat.PropertyType(scaleName);
                int baseId = combat.PropertyType(baseName);
                int targetId = combat.PropertyType(targetName);
                if (scaleId <= 0 || baseId <= 0 || targetId <= 0
                    || !properties.TryGetValue(scaleId, out long rawScale)) return;
                long target = Property(properties, targetId);
                long baseValue = Property(properties, baseId);
                properties[targetId] = Math.Max(1L,
                    target + (long)((target + baseValue) * formula.ReadProperty(properties, scaleId)));
                properties.Remove(scaleId);
            }

            void ApplyFixed(string fixedName, string targetName)
            {
                int fixedId = combat.PropertyType(fixedName);
                int targetId = combat.PropertyType(targetName);
                if (fixedId <= 0 || targetId <= 0 || !properties.ContainsKey(fixedId)) return;
                long value = (long)formula.ReadProperty(properties, fixedId);
                properties[targetId] = checked(Property(properties, targetId) + value);
            }
        }

        private static long Property(IReadOnlyDictionary<int, long> values, int id) =>
            values.TryGetValue(id, out long value) ? value : 0L;

        private Dictionary<string, object> ResolveLevelDetail(int npcClassId, int rank, int level,
            out int effectiveLevel)
        {
            List<Dictionary<string, object>> rows = levelDetails[npcClassId];
            List<Dictionary<string, object>> rankRows = rows.Where(row => Int(row, "Rank") == rank)
                .OrderBy(row => Int(row, "MinLevel")).ToList();
            if (rankRows.Count == 0)
            {
                int fallbackRank = rows.Max(row => Int(row, "Rank"));
                rank = fallbackRank;
                rankRows = rows.Where(row => Int(row, "Rank") == rank)
                    .OrderBy(row => Int(row, "MinLevel")).ToList();
            }
            int minimum = rankRows.Min(row => Int(row, "MinLevel"));
            int maximum = rankRows.Max(row => Int(row, "MaxLevel"));
            effectiveLevel = Math.Max(minimum, Math.Min(maximum, level));
            int selectedLevel = effectiveLevel;
            return rankRows.FirstOrDefault(row => selectedLevel >= Int(row, "MinLevel")
                    && selectedLevel <= Int(row, "MaxLevel"))
                ?? throw new InvalidOperationException("Official NPC level detail range is incomplete: "
                    + npcClassId + "/" + rank + "/" + effectiveLevel);
        }

        private void Validate()
        {
            if (npcs.Count == 0 || levelDetails.Count == 0 || professions.Count == 0 || inviteRules.Count == 0)
                throw new FormatException("Official NPC assistance tables are incomplete.");
            foreach (Dictionary<string, object> row in npcs.Values)
                if (Int(row, "Id") <= 0 || Int(row, "NpcType") < 0)
                    throw new FormatException("Official NPC row is malformed.");
        }

        private Dictionary<string, object> RequiredNpcRow(int npcClassId)
        {
            if (!npcs.TryGetValue(npcClassId, out Dictionary<string, object> row))
                throw new KeyNotFoundException("Official NPC is missing: " + npcClassId);
            return row;
        }

        private string RequiredNpc(int npcClassId, string field)
        {
            Dictionary<string, object> row = RequiredNpcRow(npcClassId);
            if (!row.TryGetValue(field, out object value))
                throw new FormatException("Official NPC " + field + " is missing: " + npcClassId);
            if (!(value is string text) || string.IsNullOrEmpty(text))
                throw new FormatException("Official NPC " + field + " is missing: " + npcClassId);
            return text;
        }

        private static List<Dictionary<string, object>> Rows(Dictionary<string, object> root, string key)
        {
            if (!root.TryGetValue(key, out object raw) || raw == null)
                throw new FormatException("Official NPC assistance table is missing: " + key);
            return FightOfficialCombatCatalog.ParseArray(raw, key)
                .Select(value => FightOfficialCombatCatalog.ParseObject(value, key + " row")).ToList();
        }

        private static Dictionary<int, long> LongMap(Dictionary<string, object> row, string key) =>
            FightOfficialCombatCatalog.ReadLongMap(row, key);

        private static int[] IntArray(Dictionary<string, object> row, string key) =>
            FightOfficialCombatCatalog.ReadIntArray(row, key);

        private static int Int(Dictionary<string, object> row, string key, int fallback = 0) =>
            FightOfficialCombatCatalog.ReadInt(row, key, fallback);

        private static string Text(Dictionary<string, object> row, string key) =>
            row.TryGetValue(key, out object raw) ? Convert.ToString(raw,
                System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

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

        private static bool Bool(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object raw) && raw is bool value && value;
        }

        private static void Merge(Dictionary<int, long> target, IReadOnlyDictionary<int, long> source)
        {
            foreach (KeyValuePair<int, long> entry in source)
                target[entry.Key] = checked((target.TryGetValue(entry.Key, out long current) ? current : 0L)
                    + entry.Value);
        }

        private sealed class NpcSkillBinding
        {
            public int Slot;
            public int ItemId;
            public int Rank;
            public int Level;
        }
    }
}
