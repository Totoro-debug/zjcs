using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Skills
{
    public sealed class OfflineSkillProgression
    {
        private const int ActiveSkillItemType = 100;
        private const int PassiveSkillItemType = 101;
        private const int SlotCount = 4;
        private readonly OfflineSkillRules rules;

        public OfflineSkillProgression(OfflineSkillRules rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>
        /// Repairs the null containers Unity's JSON reader can leave behind when a save predates the
        /// skill profile. Only structural defaults are filled; invalid content still fails
        /// <see cref="IsValid"/>.
        /// </summary>
        public static void Normalize(OfflineSkillProfile profile)
        {
            if (profile == null) return;
            if (profile.BlessRecords == null) profile.BlessRecords = new List<OfflineSkillBlessRecord>();
            if (profile.Skills == null) profile.Skills = new List<OfflineOwnedSkill>();
            if (profile.Items == null) profile.Items = new List<OfflineSkillItemCount>();
            if (profile.FavoriteSkills == null) profile.FavoriteSkills = new List<int>();
            if (profile.ActiveSlotLevels == null) profile.ActiveSlotLevels = new[] { 1, 1, 1, 1 };
            if (profile.PassiveSlotLevels == null) profile.PassiveSlotLevels = new[] { 1, 1, 1, 1 };
            if (profile.Plans == null) profile.Plans = OfflineSkillProfile.CreatePlans();
            for (int i = 0; i < profile.Plans.Length; i++)
            {
                OfflineSkillPlan plan = profile.Plans[i];
                if (plan == null) continue;
                if (plan.PetIndex == 0) plan.PetIndex = -1;
                if (plan.ActiveSkills == null) plan.ActiveSkills = new int[SlotCount];
                if (plan.PassiveSkills == null) plan.PassiveSkills = new int[SlotCount];
                if (string.IsNullOrWhiteSpace(plan.Name)) plan.Name = "方案" + (i + 1);
            }
        }

        public static bool IsValid(OfflineSkillProfile profile)
        {
            if (profile == null || profile.SeniorMaterialSpentOnLevelUp < 0 || profile.Version != 1 || profile.SelectedPlan < 0 || profile.SelectedPlan >= 3
                || profile.Plans == null || profile.Plans.Length != 3
                || !ValidLevels(profile.ActiveSlotLevels) || !ValidLevels(profile.PassiveSlotLevels)
                || profile.BlessRecords == null || profile.Skills == null || profile.Items == null
                || profile.FavoriteSkills == null)
                return false;

            var seasons = new HashSet<int>();
            foreach (OfflineSkillBlessRecord record in profile.BlessRecords)
                if (record == null || record.SeasonId <= 0 || !seasons.Add(record.SeasonId)
                    || !ValidBlessLevels(record.ActiveSlotLevels) || !ValidBlessLevels(record.PassiveSlotLevels)) return false;

            var owned = new HashSet<int>();
            foreach (OfflineOwnedSkill skill in profile.Skills)
                if (skill == null || skill.ClassId <= 0 || skill.Rank <= 0 || !owned.Add(skill.ClassId)) return false;
            var favoriteIds = new HashSet<int>();
            foreach (int favorite in profile.FavoriteSkills)
                if (favorite <= 0 || !favoriteIds.Add(favorite)) return false;
            var itemIds = new HashSet<int>();
            foreach (OfflineSkillItemCount item in profile.Items)
                if (item == null || item.ItemId <= 0 || item.Count < 0 || !itemIds.Add(item.ItemId)) return false;

            foreach (OfflineSkillPlan plan in profile.Plans)
            {
                if (plan == null || string.IsNullOrWhiteSpace(plan.Name) || plan.Name.Length > 16
                    || plan.PetIndex < -1
                    || !ValidSlots(plan.ActiveSkills) || !ValidSlots(plan.PassiveSkills)) return false;
                if (!UniqueNonzero(plan.ActiveSkills) || !UniqueNonzero(plan.PassiveSkills)) return false;
            }
            return true;
        }

        public bool IsValidProfile(OfflineSkillProfile profile)
        {
            if (!IsValid(profile)) return false;
            foreach (OfflineOwnedSkill skill in profile.Skills)
            {
                OfflineSkillDefinition definition = FindDefinition(skill.ClassId);
                if (definition == null || definition.MaximumRank <= 0 || skill.Rank > definition.MaximumRank) return false;
            }
            foreach (OfflineSkillPlan plan in profile.Plans)
            {
                if (!ValidEquippedSkills(profile, plan.ActiveSkills, ActiveSkillItemType)
                    || !ValidEquippedSkills(profile, plan.PassiveSkills, PassiveSkillItemType)) return false;
            }
            return true;
        }

        /// <summary>
        /// Grants the official initial active skill of the chosen starting weapon. Used by the
        /// character flow and by battle entry so an existing save without a plan still fights with
        /// the configured first skill instead of an empty list.
        /// </summary>
        public static bool EnsureInitialSkill(OfflineSkillProfile profile, int itemId)
        {
            if (!IsValid(profile) || itemId <= 0) return false;
            OfflineSkillPlan plan = profile.Plans[profile.SelectedPlan];
            bool changed = false;
            if (Array.IndexOf(plan.ActiveSkills, itemId) < 0 && AllEmpty(plan.ActiveSkills))
            {
                plan.ActiveSkills[0] = itemId;
                changed = true;
            }
            if (!profile.Skills.Exists(skill => skill != null && skill.ClassId == itemId))
            {
                profile.Skills.Add(new OfflineOwnedSkill { ClassId = itemId, Rank = 1 });
                changed = true;
            }
            return changed;
        }

        public static int[] EquippedActiveSkills(OfflineSkillProfile profile)
        {
            if (!IsValid(profile)) return Array.Empty<int>();
            return (int[])profile.Plans[profile.SelectedPlan].ActiveSkills.Clone();
        }

        public static int[] EquippedPassiveSkills(OfflineSkillProfile profile)
        {
            if (!IsValid(profile)) return Array.Empty<int>();
            return (int[])profile.Plans[profile.SelectedPlan].PassiveSkills.Clone();
        }

        public static int OwnedRank(OfflineSkillProfile profile, int classId)
        {
            if (profile == null || profile.Skills == null) return 0;
            foreach (OfflineOwnedSkill skill in profile.Skills)
                if (skill != null && skill.ClassId == classId) return skill.Rank;
            return 0;
        }

        private static bool AllEmpty(int[] values)
        {
            if (values == null) return false;
            foreach (int value in values) if (value != 0) return false;
            return true;
        }

        public OfflineSkillResult ToggleFavorite(OfflineSkillProfile profile, int skillId)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (skillId <= 0 || FindOwned(profile, skillId) == null)
                return Result(OfflineSkillResultCode.SkillNotOwned);
            if (!profile.FavoriteSkills.Remove(skillId)) profile.FavoriteSkills.Add(skillId);
            return Result(OfflineSkillResultCode.Applied);
        }

        public static bool IsFavorite(OfflineSkillProfile profile, int skillId)
        {
            return profile != null && profile.FavoriteSkills != null && profile.FavoriteSkills.Contains(skillId);
        }

        public OfflineSkillResult SelectPlan(OfflineSkillProfile profile, int plan)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (plan < 0 || plan >= profile.Plans.Length) return Result(OfflineSkillResultCode.InvalidArgument);
            if (profile.SelectedPlan == plan) return Result(OfflineSkillResultCode.Unchanged);
            profile.SelectedPlan = plan;
            return Result(OfflineSkillResultCode.Applied);
        }

        public OfflineSkillResult RenamePlan(OfflineSkillProfile profile, int plan, string name)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (plan < 0 || plan >= profile.Plans.Length || string.IsNullOrWhiteSpace(name)
                || name.Trim().Length > 16) return Result(OfflineSkillResultCode.InvalidArgument);
            string normalized = name.Trim();
            if (profile.Plans[plan].Name == normalized) return Result(OfflineSkillResultCode.Unchanged);
            profile.Plans[plan].Name = normalized;
            return Result(OfflineSkillResultCode.Applied);
        }

        public OfflineSkillResult Equip(OfflineSkillProfile profile, OfflineSkillPlayerContext player,
            int skillId, int slot)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (player == null || slot < 0 || slot >= SlotCount) return Result(OfflineSkillResultCode.InvalidArgument);
            OfflineOwnedSkill owned = FindOwned(profile, skillId);
            OfflineSkillDefinition definition = FindDefinition(skillId);
            if (owned == null) return Result(OfflineSkillResultCode.SkillNotOwned);
            if (definition == null || !IsAvailable(definition, player)) return Result(OfflineSkillResultCode.SkillUnavailable);

            OfflineSkillPlan plan = profile.Plans[profile.SelectedPlan];
            int[] slots = Slots(plan, definition.ItemType);
            if (slots == null) return Result(OfflineSkillResultCode.SkillUnavailable);
            int previous = Array.IndexOf(slots, skillId);
            if (previous == slot) return Result(OfflineSkillResultCode.Unchanged);
            int displaced = slots[slot];
            if (previous >= 0) slots[previous] = displaced;
            slots[slot] = skillId;
            return Result(OfflineSkillResultCode.Applied);
        }

        public OfflineSkillResult Unequip(OfflineSkillProfile profile, int itemType, int slot)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (slot < 0 || slot >= SlotCount) return Result(OfflineSkillResultCode.InvalidArgument);
            int[] slots = Slots(profile.Plans[profile.SelectedPlan], itemType);
            if (slots == null) return Result(OfflineSkillResultCode.InvalidArgument);
            if (slots[slot] == 0) return Result(OfflineSkillResultCode.Unchanged);
            slots[slot] = 0;
            return Result(OfflineSkillResultCode.Applied);
        }

        public int SlotLevel(OfflineSkillProfile profile, int itemType, int slot, int seasonId = -1)
        {
            if (profile == null || slot < 0 || slot >= SlotCount) return 0;
            int[] values = itemType == ActiveSkillItemType ? profile.ActiveSlotLevels
                : itemType == PassiveSkillItemType ? profile.PassiveSlotLevels : null;
            if (values == null || values.Length != SlotCount) return 0;
            int bless = 0;
            if (seasonId >= 0 && profile.BlessRecords != null)
                foreach (OfflineSkillBlessRecord record in profile.BlessRecords)
                    if (record.SeasonId == seasonId)
                    {
                        int[] blessLevels = itemType == ActiveSkillItemType
                            ? record.ActiveSlotLevels : record.PassiveSlotLevels;
                        if (blessLevels != null && blessLevels.Length == SlotCount) bless = blessLevels[slot];
                        break;
                    }
            return values[slot] + bless;
        }

        public OfflineSkillResult UpgradeSlot(OfflineSkillProfile profile, OfflineSkillPlayerContext player,
            int itemType, int slot)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (player == null || slot < 0 || slot >= SlotCount
                || (itemType != ActiveSkillItemType && itemType != PassiveSkillItemType))
                return Result(OfflineSkillResultCode.InvalidArgument);
            OfflineSkillProfile candidate = profile.Clone();
            Dictionary<int, long> cost;
            OfflineSkillResultCode quote = QuoteSlotUpgrade(candidate, player,
                SlotLevel(candidate, itemType, slot, player.CurrentSeasonId), out cost, out int next);
            if (quote != OfflineSkillResultCode.Applied) return Result(quote);
            if (!CanPay(candidate, cost)) return Result(OfflineSkillResultCode.InsufficientItems, cost);
            Pay(candidate, cost);
            SetSlotLevel(candidate, player, itemType, slot, next);
            CopyProgression(candidate, profile);
            return Result(OfflineSkillResultCode.Applied, cost, 1);
        }

        public OfflineSkillResult UpgradeAllSlots(OfflineSkillProfile profile, OfflineSkillPlayerContext player)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (player == null) return Result(OfflineSkillResultCode.InvalidArgument);
            OfflineSkillProfile candidate = profile.Clone();
            var total = new Dictionary<int, long>();
            int changed = 0;
            while (true)
            {
                int kind = ActiveSkillItemType;
                int slot = 0;
                int minimum = SlotLevel(candidate, kind, slot, player.CurrentSeasonId);
                foreach (int itemType in new[] { ActiveSkillItemType, PassiveSkillItemType })
                    for (int index = 0; index < SlotCount; index++)
                    {
                        int level = SlotLevel(candidate, itemType, index, player.CurrentSeasonId);
                        if (level < minimum) { kind = itemType; slot = index; minimum = level; }
                    }

                Dictionary<int, long> cost;
                OfflineSkillResultCode quote = QuoteSlotUpgrade(candidate, player, minimum, out cost, out int next);
                if (quote != OfflineSkillResultCode.Applied) break;
                Dictionary<int, long> combined = Add(total, cost);
                if (!CanPay(profile, combined)) break;
                total = combined;
                SetSlotLevel(candidate, player, kind, slot, next);
                changed++;
            }
            if (changed == 0) return Result(OfflineSkillResultCode.NoUpgradeAvailable);
            Pay(candidate, total);
            CopyProgression(candidate, profile);
            return Result(OfflineSkillResultCode.Applied, total, changed);
        }

        public OfflineSkillResult PromoteSkill(OfflineSkillProfile profile, OfflineSkillPlayerContext player, int skillId)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (player == null) return Result(OfflineSkillResultCode.InvalidArgument);
            OfflineSkillProfile candidate = profile.Clone();
            OfflineOwnedSkill owned = FindOwned(candidate, skillId);
            OfflineSkillDefinition definition = FindDefinition(skillId);
            if (owned == null) return Result(OfflineSkillResultCode.SkillNotOwned);
            if (definition == null || !IsAvailable(definition, player)) return Result(OfflineSkillResultCode.SkillUnavailable);
            int rank = owned.Rank;
            if (rank >= definition.MaximumRank) return Result(OfflineSkillResultCode.NoUpgradeAvailable);
            OfflineSkillRankCost row = FindRankCost(definition, rank);
            if (row == null || !ValidCost(row.Items)) return Result(OfflineSkillResultCode.NoUpgradeAvailable);
            Dictionary<int, long> cost = Cost(row.Items);
            if (!CanPay(candidate, cost)) return Result(OfflineSkillResultCode.InsufficientItems, cost);
            Pay(candidate, cost);
            owned.Rank++;
            CopyProgression(candidate, profile);
            return Result(OfflineSkillResultCode.Applied, cost, 1);
        }

        public OfflineSkillResult PromoteAllSkills(OfflineSkillProfile profile, OfflineSkillPlayerContext player)
        {
            if (!IsValidProfile(profile)) return Result(OfflineSkillResultCode.InvalidProfile);
            if (player == null) return Result(OfflineSkillResultCode.InvalidArgument);
            OfflineSkillProfile candidate = profile.Clone();
            var aggregate = new Dictionary<int, long>();
            var ranks = new List<KeyValuePair<OfflineOwnedSkill, int>>();
            foreach (OfflineOwnedSkill owned in candidate.Skills)
            {
                OfflineSkillDefinition definition = FindDefinition(owned.ClassId);
                if (definition == null || !IsAvailable(definition, player)) continue;
                int nextRank = owned.Rank;
                var skillCost = new Dictionary<int, long>();
                while (nextRank < definition.MaximumRank)
                {
                    OfflineSkillRankCost row = FindRankCost(definition, nextRank);
                    if (row == null || !ValidCost(row.Items)) break;
                    Dictionary<int, long> nextCost = Add(skillCost, Cost(row.Items));
                    if (!CanPay(profile, nextCost)) break;
                    skillCost = nextCost;
                    nextRank++;
                }
                if (nextRank == owned.Rank) continue;
                ranks.Add(new KeyValuePair<OfflineOwnedSkill, int>(owned, nextRank));
                aggregate = Add(aggregate, skillCost);
            }
            if (ranks.Count == 0) return Result(OfflineSkillResultCode.NoUpgradeAvailable);
            if (!CanPay(profile, aggregate)) return Result(OfflineSkillResultCode.InsufficientItems, aggregate);
            Pay(candidate, aggregate);
            foreach (KeyValuePair<OfflineOwnedSkill, int> item in ranks)
                item.Key.Rank = item.Value;
            CopyProgression(candidate, profile);
            return Result(OfflineSkillResultCode.Applied, aggregate, ranks.Count);
        }

        private OfflineSkillResultCode QuoteSlotUpgrade(OfflineSkillProfile profile,
            OfflineSkillPlayerContext player, int current, out Dictionary<int, long> cost, out int next)
        {
            cost = new Dictionary<int, long>();
            next = current + 1;
            int baseLimit = BaseLevelLimit(player.SubRank);
            if (baseLimit <= 0) return OfflineSkillResultCode.LevelLimit;
            if (next > baseLimit && player.CurrentSeasonId <= 0) return OfflineSkillResultCode.LevelLimit;
            OfflineSkillLevelLimit limit = FindLevelLimit(player);
            if (limit == null) return OfflineSkillResultCode.LevelLimit;
            if (next > baseLimit && (player.PlayerRank != player.CurrentSeasonRank
                || !AllBaseSlotsAtLimit(profile, baseLimit))) return OfflineSkillResultCode.LevelLimit;
            int available = limit.SkillLevel;
            if (player.PlayerRank == player.CurrentSeasonRank) available += limit.SkillBlessLevel;
            if (next > available) return OfflineSkillResultCode.LevelLimit;

            OfflineSkillItemCount[] items;
            if (next <= baseLimit)
            {
                OfflineSkillLevelCost row = FindLevelCost(next);
                if (row == null) return OfflineSkillResultCode.NoUpgradeAvailable;
                items = row.Items;
            }
            else
            {
                int rank = player.CurrentSeasonRank;
                OfflineSkillBlessCost row = FindBlessCost(rank, next - baseLimit);
                if (row == null) return OfflineSkillResultCode.NoUpgradeAvailable;
                items = row.Items;
            }
            if (!ValidCost(items)) return OfflineSkillResultCode.NoUpgradeAvailable;
            cost = Cost(items);
            return OfflineSkillResultCode.Applied;
        }

        private int BaseLevelLimit(int subRank)
        {
            int rank = subRank < 2 ? subRank : 2 + (subRank - 2) / 3;
            foreach (OfflineSkillSubRankLimit row in rules.BaseLevelLimits ?? Array.Empty<OfflineSkillSubRankLimit>())
                if (row != null && row.SubRank == rank) return row.SkillLevel;
            return 0;
        }

        private static bool AllBaseSlotsAtLimit(OfflineSkillProfile profile, int limit)
        {
            foreach (int level in profile.ActiveSlotLevels) if (level < limit) return false;
            foreach (int level in profile.PassiveSlotLevels) if (level < limit) return false;
            return true;
        }

        private OfflineSkillLevelLimit FindLevelLimit(OfflineSkillPlayerContext player)
        {
            foreach (OfflineSkillLevelLimit row in rules.LevelLimits ?? Array.Empty<OfflineSkillLevelLimit>())
                if (row != null && row.SubRank == player.SubRank && row.PlayerLevel == player.BaseLevel
                    && row.PlayerBlessLevel == player.PlayerBlessLevel) return row;
            return null;
        }

        private OfflineSkillLevelCost FindLevelCost(int level)
        {
            foreach (OfflineSkillLevelCost row in rules.SlotLevelCosts ?? Array.Empty<OfflineSkillLevelCost>())
                if (row != null && row.Level == level) return row;
            return null;
        }

        private OfflineSkillBlessCost FindBlessCost(int rank, int level)
        {
            foreach (OfflineSkillBlessCost row in rules.SlotBlessCosts ?? Array.Empty<OfflineSkillBlessCost>())
                if (row != null && row.Rank == rank && row.BlessLevel == level) return row;
            return null;
        }

        private OfflineSkillDefinition FindDefinition(int id)
        {
            foreach (OfflineSkillDefinition definition in rules.Skills ?? Array.Empty<OfflineSkillDefinition>())
                if (definition != null && definition.ClassId == id) return definition;
            return null;
        }

        private static OfflineOwnedSkill FindOwned(OfflineSkillProfile profile, int id)
        {
            foreach (OfflineOwnedSkill skill in profile.Skills)
                if (skill != null && skill.ClassId == id) return skill;
            return null;
        }

        private bool ValidEquippedSkills(OfflineSkillProfile profile, int[] slots, int itemType)
        {
            foreach (int id in slots)
            {
                if (id == 0) continue;
                OfflineSkillDefinition definition = FindDefinition(id);
                if (FindOwned(profile, id) == null || definition == null || definition.ItemType != itemType)
                    return false;
            }
            return true;
        }

        private static OfflineSkillRankCost FindRankCost(OfflineSkillDefinition definition, int rank)
        {
            foreach (OfflineSkillRankCost row in definition.RankCosts ?? Array.Empty<OfflineSkillRankCost>())
                if (row != null && row.CurrentRank == rank) return row;
            return null;
        }

        /// <summary>Public availability check used by the skill list profession mismatch marker.</summary>
        public static bool IsAvailableFor(OfflineSkillDefinition definition, OfflineSkillPlayerContext player)
        {
            return definition != null && player != null && IsAvailable(definition, player);
        }

        private static bool IsAvailable(OfflineSkillDefinition definition, OfflineSkillPlayerContext player)
        {
            if (definition.ItemType != ActiveSkillItemType && definition.ItemType != PassiveSkillItemType) return false;
            // Starting skills are unlocked by the first profession rank and stay available without a
            // profession restriction (original initial-weapon skill flow).
            if (definition.AvailableWithoutProfession) return true;
            if (player.Profession <= 0) return false;
            foreach (int profession in definition.AvailableProfessions ?? Array.Empty<int>())
                if (profession == player.Profession
                    || (player.CurrentProfessionChain != null && player.CurrentProfessionChain.Contains(profession))) return true;
            return false;
        }

        private void SetSlotLevel(OfflineSkillProfile profile, OfflineSkillPlayerContext player,
            int itemType, int slot, int target)
        {
            int[] levels = itemType == ActiveSkillItemType ? profile.ActiveSlotLevels : profile.PassiveSlotLevels;
            int baseLimit = BaseLevelLimit(player.SubRank);
            levels[slot] = Math.Min(target, baseLimit);
            if (target <= baseLimit || player.CurrentSeasonId <= 0) return;
            OfflineSkillBlessRecord record = null;
            foreach (OfflineSkillBlessRecord candidate in profile.BlessRecords)
                if (candidate.SeasonId == player.CurrentSeasonId) { record = candidate; break; }
            if (record == null)
            {
                record = new OfflineSkillBlessRecord { SeasonId = player.CurrentSeasonId };
                profile.BlessRecords.Add(record);
            }
            int[] blessLevels = itemType == ActiveSkillItemType ? record.ActiveSlotLevels : record.PassiveSlotLevels;
            blessLevels[slot] = Math.Max(0, target - baseLimit);
        }

        private static void CopyProgression(OfflineSkillProfile source, OfflineSkillProfile target)
        {
            target.SelectedPlan = source.SelectedPlan;
            target.Plans = source.Plans;
            target.ActiveSlotLevels = source.ActiveSlotLevels;
            target.PassiveSlotLevels = source.PassiveSlotLevels;
            target.BlessRecords = source.BlessRecords;
            target.Skills = source.Skills;
            target.Items = source.Items;
            target.FavoriteSkills = source.FavoriteSkills;
        }

        private static Dictionary<int, long> Cost(OfflineSkillItemCount[] items)
        {
            var result = new Dictionary<int, long>();
            foreach (OfflineSkillItemCount item in items)
                result[item.ItemId] = result.TryGetValue(item.ItemId, out long current) ? current + item.Count : item.Count;
            return result;
        }

        private static Dictionary<int, long> Add(IDictionary<int, long> left, IDictionary<int, long> right)
        {
            var result = new Dictionary<int, long>(left);
            foreach (KeyValuePair<int, long> item in right)
                result[item.Key] = result.TryGetValue(item.Key, out long current) ? checked(current + item.Value) : item.Value;
            return result;
        }

        private static bool CanPay(OfflineSkillProfile profile, IDictionary<int, long> cost)
        {
            foreach (KeyValuePair<int, long> required in cost)
            {
                long count = 0;
                foreach (OfflineSkillItemCount item in profile.Items)
                    if (item.ItemId == required.Key) { count = item.Count; break; }
                if (required.Value < 0 || count < required.Value) return false;
            }
            return true;
        }

        private static void Pay(OfflineSkillProfile profile, IDictionary<int, long> cost)
        {
            foreach (KeyValuePair<int, long> required in cost)
                foreach (OfflineSkillItemCount item in profile.Items)
                    if (item.ItemId == required.Key) { item.Count -= required.Value; break; }
        }

        private static int[] Slots(OfflineSkillPlan plan, int itemType)
        {
            return itemType == ActiveSkillItemType ? plan.ActiveSkills
                : itemType == PassiveSkillItemType ? plan.PassiveSkills : null;
        }

        private static bool ValidLevels(int[] levels)
        {
            if (levels == null || levels.Length != SlotCount) return false;
            foreach (int level in levels) if (level < 1 || level > 620) return false;
            return true;
        }

        private static bool ValidBlessLevels(int[] levels)
        {
            if (levels == null || levels.Length != SlotCount) return false;
            foreach (int level in levels) if (level < 0 || level > 620) return false;
            return true;
        }

        private static bool ValidSlots(int[] slots)
        {
            if (slots == null || slots.Length != SlotCount) return false;
            foreach (int skillId in slots) if (skillId < 0) return false;
            return true;
        }

        private static bool UniqueNonzero(int[] slots)
        {
            var ids = new HashSet<int>();
            foreach (int skillId in slots) if (skillId != 0 && !ids.Add(skillId)) return false;
            return true;
        }

        private static bool ValidCost(OfflineSkillItemCount[] items)
        {
            if (items == null) return false;
            var ids = new HashSet<int>();
            foreach (OfflineSkillItemCount item in items)
                if (item == null || item.ItemId <= 0 || item.Count < 0 || !ids.Add(item.ItemId)) return false;
            return true;
        }

        private static OfflineSkillResult Result(OfflineSkillResultCode code,
            IDictionary<int, long> cost = null, int changed = 0)
        {
            return new OfflineSkillResult(code, cost, changed);
        }
    }
}
