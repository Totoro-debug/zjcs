using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Skills;

namespace Zjcs.Offline.Progression
{
    /// <summary>Original SkillPropChunck passive-property calculation for the selected plan.</summary>
    public sealed class OfflineSkillPropSource : IOfflineCharacterPropSource
    {
        private const int ActiveSkillItemType = 100;
        private const int PassiveSkillItemType = 101;

        private sealed class SkillRatingEntry
        {
            public int ClassId;
            public int Rank;
            public int EffectiveLevel;
            public float BaseScore;
        }

        private readonly OfficialCharacterCatalog catalog;
        private readonly OfflineSkillRules rules;
        private readonly Dictionary<int, OfflineSkillDefinition> skills;
        private readonly Dictionary<int, int> professionRanks;

        public OfflineSkillPropSource(OfficialCharacterCatalog characterCatalog, OfflineSkillRules skillRules)
        {
            catalog = characterCatalog ?? throw new ArgumentNullException(nameof(characterCatalog));
            rules = skillRules ?? throw new ArgumentNullException(nameof(skillRules));
            skills = (rules.Skills ?? Array.Empty<OfflineSkillDefinition>())
                .Where(value => value != null && value.ClassId > 0)
                .GroupBy(value => value.ClassId).ToDictionary(group => group.Key, group => group.First());
            professionRanks = SkillProfessionRanks(characterCatalog);
        }

        public int HandleType => CharacterPropHandleType.Skill;

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player)
        {
            var result = new Dictionary<int, long>();
            if (player?.Skills?.Plans == null || player.Skills.SelectedPlan < 0
                || player.Skills.SelectedPlan >= player.Skills.Plans.Length) return result;
            OfflineSkillPlan plan = player.Skills.Plans[player.Skills.SelectedPlan];
            if (plan == null) return result;
            AddPlanSkills(result, player, plan.ActiveSkills, ActiveSkillItemType);
            AddPlanSkills(result, player, plan.PassiveSkills, PassiveSkillItemType);
            return result;
        }

        public long CalculateCombatRating(OfflinePlayerProgress player, IReadOnlyDictionary<int, long> props,
            CharacterPropScoreContext context)
        {
            return CalculateCombatRating(player, props, context, true);
        }

        public long CalculateCombatRating(OfflinePlayerProgress player, IReadOnlyDictionary<int, long> props,
            CharacterPropScoreContext context, bool usePlayerReferenceProps = true)
        {
            if (player?.Skills?.Skills == null || context == null) return 0L;
            Dictionary<string, object> settings = catalog.Rows("game_settings").FirstOrDefault();
            if (settings == null) return 0L;
            Dictionary<int, float> professionRankFactors = FloatMap(settings, "SkillProfessionRankFactors");
            int skillRankAttack = Integer(settings, "SkillRankAttack");
            int skillScoreLevelFactor = Integer(settings, "SkillScoreLevelFactor");
            float skillRankScore = Number(settings, "SkillRankScore");
            float skillLevelScore = Number(settings, "SkillLevelScore");
            int[] skillScoreBase = IntegerList(settings, "SkillScoreBase");
            int activeSlots = NestedInteger(settings, "SkillTypes", ActiveSkillItemType.ToString(CultureInfo.InvariantCulture));
            int passiveSlots = NestedInteger(settings, "SkillTypes", PassiveSkillItemType.ToString(CultureInfo.InvariantCulture));
            int slotTotal = activeSlots + passiveSlots;
            if (slotTotal <= 0) return 0L;

            int profession = player.Wardrobe == null ? 0 : player.Wardrobe.Profession;
            List<SkillRatingEntry> active = OwnedSkills(player, ActiveSkillItemType, profession, skillRankAttack,
                professionRankFactors);
            List<SkillRatingEntry> passive = OwnedSkills(player, PassiveSkillItemType, profession, skillRankAttack,
                professionRankFactors);
            AssignEffectiveLevels(active, player, ActiveSkillItemType);
            AssignEffectiveLevels(passive, player, PassiveSkillItemType);

            float qualityAttack = QualityAttack(active, activeSlots, slotTotal)
                + QualityAttack(passive, passiveSlots, slotTotal);
            float scoreBaseRating = (float)context.ByProps(context.BasicRef.RateFormateBaseProps,
                null, skillScoreBase);
            long qualityRating = (long)(skillRankScore * qualityAttack * scoreBaseRating);
            if (skillScoreLevelFactor > 0)
                qualityRating = (long)((float)qualityRating
                    * context.LevelNumber(skillScoreLevelFactor, Math.Max(1, player.Level)));

            float levelAttack = SkillLevelAttack(active, activeSlots, slotTotal, player, context, skillLevelScore,
                    skillScoreBase, usePlayerReferenceProps)
                + SkillLevelAttack(passive, passiveSlots, slotTotal, player, context, skillLevelScore,
                    skillScoreBase, usePlayerReferenceProps);
            if (skillScoreLevelFactor > 0)
                levelAttack = (long)(levelAttack
                    * context.LevelNumber(skillScoreLevelFactor, Math.Max(1, player.Level)));
            return qualityRating + (long)levelAttack;
        }

        private List<SkillRatingEntry> OwnedSkills(OfflinePlayerProgress player, int itemType, int profession,
            int rankAttackId, Dictionary<int, float> professionRankFactors)
        {
            var result = new List<SkillRatingEntry>();
            foreach (OfflineOwnedSkill owned in player.Skills.Skills.Where(value => value != null)
                         .GroupBy(value => value.ClassId).Select(group => group.First()))
            {
                if (owned.ClassId <= 0 || owned.Rank <= 0
                    || !skills.TryGetValue(owned.ClassId, out OfflineSkillDefinition definition)
                    || !IsAvailable(definition, profession)) continue;
                Dictionary<string, object> item = catalog.Find("item", owned.ClassId);
                Dictionary<string, object> skill = catalog.Find("skill", owned.ClassId);
                if (item == null || skill == null || Integer(item, "ItemType") != itemType) continue;

                int professionRank = professionRanks.TryGetValue(owned.ClassId, out int rank) ? rank : 0;
                float rankFactor = professionRankFactors.TryGetValue(professionRank, out float factor)
                    ? factor : 1f;
                result.Add(new SkillRatingEntry
                {
                    ClassId = owned.ClassId,
                    Rank = owned.Rank,
                    BaseScore = rankFactor * (catalog.LevelNumberLerp(rankAttackId, owned.Rank))
                });
            }
            return result.OrderByDescending(value => value.Rank).ToList();
        }

        private void AssignEffectiveLevels(List<SkillRatingEntry> entries, OfflinePlayerProgress player, int itemType)
        {
            if (entries.Count == 0) return;
            int[] source = itemType == ActiveSkillItemType
                ? player.Skills.ActiveSlotLevels : player.Skills.PassiveSlotLevels;
            int count = Math.Max(1, source == null ? 0 : source.Length);
            int[] levels = new int[count];
            for (int i = 0; i < count; i++) levels[i] = SlotLevel(player, itemType, i);
            Array.Sort(levels, (left, right) => right.CompareTo(left));
            for (int i = 0; i < entries.Count; i++)
                entries[i].EffectiveLevel = levels[Math.Min(i, levels.Length - 1)];
        }

        private static float QualityAttack(List<SkillRatingEntry> entries, int slotCount, int slotTotal)
        {
            float result = 0f;
            List<SkillRatingEntry> sorted = entries.OrderByDescending(value => value.BaseScore).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                float value = sorted[i].BaseScore;
                if (i >= slotCount) value *= 0.1f;
                result += value / slotTotal;
            }
            return result;
        }

        private float SkillLevelAttack(List<SkillRatingEntry> entries, int slotCount, int slotTotal,
            OfflinePlayerProgress player, CharacterPropScoreContext context, float skillLevelScore,
            int[] skillScoreBase, bool usePlayerReferenceProps)
        {
            if (entries.Count == 0) return 0f;
            List<int> levels = entries.OrderByDescending(value => value.EffectiveLevel)
                .Select(value => value.EffectiveLevel).ToList();
            List<int> skillIds = entries.OrderByDescending(value => value.BaseScore)
                .Select(value => value.ClassId).ToList();
            float result = 0f;
            for (int i = 0; i < levels.Count; i++)
            {
                int level = levels[i];
                Dictionary<int, long> referenceProps;
                if (usePlayerReferenceProps)
                {
                    referenceProps = new Dictionary<int, long>(
                        catalog.CalcCharacterBaseBattleProperties(level, player.SubRank));
                    CharacterPropFormula.Merge(referenceProps,
                        catalog.CalcProfessionLevelProperties(player.ProfessionLevels));
                    MergeProfessionFeatureProps(referenceProps,
                        player.Wardrobe == null ? 0 : player.Wardrobe.Profession);
                }
                else
                {
                    referenceProps = new Dictionary<int, long>();
                    CharacterPropFormula.CalcCombatRatingRefProps(level, player.SubRank, context.Props,
                        catalog.GetLevelProps, referenceProps, true);
                }
                CharacterPropFormula.HandlePropRate(referenceProps, context.Props);
                SkillRatingEntry skill = entries.First(value => value.ClassId == skillIds[i]);
                float value = skillLevelScore * skill.BaseScore
                    * (float)context.ByProps(referenceProps, null, skillScoreBase);
                if (i >= slotCount) value *= 0.1f;
                result += value / slotTotal;
            }
            return result;
        }

        private void MergeProfessionFeatureProps(Dictionary<int, long> target, int profession)
        {
            Dictionary<string, object> row = catalog.Find("profession_base", profession, "Profession");
            if (row == null || !(row.TryGetValue("FeatureProps", out object raw)
                && raw is Dictionary<string, object> featureProps)) return;
            foreach (KeyValuePair<string, object> entry in featureProps)
            {
                int property = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                long value = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
                target[property] = target.TryGetValue(property, out long current) ? current + value : value;
            }
        }

        private static Dictionary<int, int> SkillProfessionRanks(OfficialCharacterCatalog characterCatalog)
        {
            var result = new Dictionary<int, int>();
            foreach (Dictionary<string, object> profession in characterCatalog.Rows("profession_base"))
            {
                if (!(profession.TryGetValue("Skills", out object rawSkills) && rawSkills is List<object> skills)
                    || skills.Count == 0) continue;
                int rank = Integer(profession, "Rank");
                foreach (object skill in skills)
                    result[Convert.ToInt32(skill, CultureInfo.InvariantCulture)] = rank;
                if (profession.TryGetValue("FightUsedSkills", out object rawFightSkills)
                    && rawFightSkills is List<object> fightSkills)
                    foreach (object skill in fightSkills)
                        result[Convert.ToInt32(skill, CultureInfo.InvariantCulture)] = rank;
            }
            return result;
        }

        private void AddPlanSkills(Dictionary<int, long> result, OfflinePlayerProgress player,
            int[] equipped, int itemType)
        {
            if (equipped == null) return;
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int skillId = equipped[slot];
                if (skillId <= 0 || !skills.TryGetValue(skillId, out OfflineSkillDefinition definition)
                    || !IsAvailable(definition, player.Wardrobe == null ? 0 : player.Wardrobe.Profession)) continue;
                int rank = OfflineSkillProgression.OwnedRank(player.Skills, skillId);
                if (rank <= 0) continue;
                Dictionary<string, object> skill = catalog.Find("skill", skillId);
                if (skill == null) continue;
                int level = SlotLevel(player, itemType, slot);
                int groupId = Integer(skill, "PassiveGroupLevelPropId");
                Dictionary<string, object> group = catalog.Rows("entity_prop_group_level").FirstOrDefault(row =>
                    Integer(row, "GroupId") == groupId && Integer(row, "SubRank") == player.SubRank);
                int levelPropId = group == null ? 0 : Integer(group, "LevelPropId");
                Dictionary<int, long> levelProps = Copy(catalog.GetLevelProps(levelPropId, level));
                int rankPropId = Integer(skill, "PassiveRankPropId");
                if (rankPropId > 0)
                    levelProps = Scale(levelProps, catalog.GetLevelProps(rankPropId, rank), false);
                levelProps = Scale(levelProps, LongMap(skill, "PassivePropFactors"), true);
                int rankFactorId = Integer(skill, "PassiveRankFactorId");
                if (rankFactorId > 0)
                    ScaleInPlace(levelProps, catalog.GetLevelProps(rankFactorId, rank));
                CharacterPropFormula.Merge(result, levelProps);
            }
        }

        private bool IsAvailable(OfflineSkillDefinition skill, int profession)
        {
            if (profession <= 0) return skill.AvailableWithoutProfession;
            return (skill.AvailableProfessions ?? Array.Empty<int>()).Contains(profession);
        }

        private static int SlotLevel(OfflinePlayerProgress player, int itemType, int slot)
        {
            int[] levels = itemType == ActiveSkillItemType
                ? player.Skills.ActiveSlotLevels : player.Skills.PassiveSlotLevels;
            int level = levels != null && slot < levels.Length ? levels[slot] : 1;
            foreach (OfflineSkillBlessRecord record in player.Skills.BlessRecords ?? new List<OfflineSkillBlessRecord>())
            {
                if (record == null || record.SeasonId != player.SeasonRank) continue;
                int[] bless = itemType == ActiveSkillItemType ? record.ActiveSlotLevels : record.PassiveSlotLevels;
                if (bless != null && slot < bless.Length) level += bless[slot];
                break;
            }
            return Math.Max(1, level);
        }

        private static Dictionary<int, long> Copy(IReadOnlyDictionary<int, long> values)
        {
            return values == null ? new Dictionary<int, long>() : new Dictionary<int, long>(values);
        }

        private static Dictionary<int, long> Scale(IReadOnlyDictionary<int, long> values,
            IReadOnlyDictionary<int, long> factors, bool removeUnscaled)
        {
            var result = new Dictionary<int, long>();
            if (values == null) return result;
            foreach (KeyValuePair<int, long> entry in values)
            {
                if (factors != null && factors.TryGetValue(entry.Key, out long factor))
                    result[entry.Key] = (long)(entry.Value * (factor / 10000.0));
                else if (!removeUnscaled)
                    result[entry.Key] = entry.Value;
            }
            return result;
        }

        private static void ScaleInPlace(Dictionary<int, long> values, IReadOnlyDictionary<int, long> factors)
        {
            if (factors == null) return;
            foreach (int property in values.Keys.ToArray())
                if (factors.TryGetValue(property, out long factor))
                    values[property] = (long)(values[property] * (factor / 10000.0));
        }

        private static Dictionary<int, long> LongMap(Dictionary<string, object> row, string key)
        {
            var result = new Dictionary<int, long>();
            if (row == null || !row.TryGetValue(key, out object raw)
                || !(raw is Dictionary<string, object> values)) return result;
            foreach (KeyValuePair<string, object> entry in values)
                result[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                    = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
            return result;
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static float Number(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : 0f;
        }

        private static int[] IntegerList(Dictionary<string, object> row, string key)
        {
            if (row == null || !row.TryGetValue(key, out object raw) || !(raw is List<object> values))
                return Array.Empty<int>();
            return values.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray();
        }

        private static int NestedInteger(Dictionary<string, object> row, string mapKey, string key)
        {
            if (row == null || !row.TryGetValue(mapKey, out object raw)
                || !(raw is Dictionary<string, object> values) || !values.TryGetValue(key, out object value)) return 0;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static Dictionary<int, float> FloatMap(Dictionary<string, object> row, string key)
        {
            var result = new Dictionary<int, float>();
            if (row == null || !row.TryGetValue(key, out object raw)
                || !(raw is Dictionary<string, object> values)) return result;
            foreach (KeyValuePair<string, object> entry in values)
                result[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                    = Convert.ToSingle(entry.Value, CultureInfo.InvariantCulture);
            return result;
        }
    }
}
