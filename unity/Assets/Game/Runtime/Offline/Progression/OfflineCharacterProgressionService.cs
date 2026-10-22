using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Skills;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflineCharacterProgressionService
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialCharacterCatalog catalog;
        private readonly CharacterPropCalculator propertyCalculator;

        public OfflineCharacterProgressionService(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog catalog)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            propertyCalculator = new CharacterPropCalculator(catalog);
            RefreshDerivedProgress(session.Save.Player);
        }

        public OfflinePlayerProgress Player => session.Save.Player;
        public OfficialCharacterCatalog Catalog => catalog;
        public CharacterPropCalculator PropertyCalculator => propertyCalculator;
        public OfficialEquipmentCatalog EquipmentCatalog { get; private set; }

        public void AttachEquipmentCatalog(OfficialEquipmentCatalog equipment)
        {
            EquipmentCatalog = equipment;
            if (equipment != null)
                propertyCalculator.AddSource(new OfflineEquipmentPropSource(equipment, catalog.RankOfSubRank));
        }

        public OfflineSkillRules SkillRules { get; private set; }
        public IReadOnlyList<OfficialSkillRecommendation> SkillRecommendations { get; private set; }
            = Array.Empty<OfficialSkillRecommendation>();
        public OfflineSkillProgression SkillProgression { get; private set; }

        public void AttachSkillRules(OfflineSkillRules rules,
            IEnumerable<OfficialSkillRecommendation> recommendations = null)
        {
            SkillRules = rules;
            SkillProgression = rules == null ? null : new OfflineSkillProgression(rules);
            SkillRecommendations = recommendations == null
                ? Array.Empty<OfficialSkillRecommendation>() : recommendations.ToArray();
        }

        public IReadOnlyList<OfficialSkillRecommendation> RecommendationsForProfession(int profession)
        {
            return SkillRecommendations.Where(value => value != null && value.Profession == profession)
                .OrderBy(value => value.Id).ToArray();
        }

        /// <summary>Installs every owned skill of a recommendation into the current plan.</summary>
        public ProgressionResult ApplySkillRecommendation(OfficialSkillRecommendation recommendation)
        {
            if (recommendation == null || SkillProgression == null)
                return Invalid("Official skill recommendations are unavailable.");
            OfflineSkillProfile clone = Player.Skills.Clone();
            OfflineSkillPlayerContext context = SkillContext();
            int applied = 0;
            int skipped = 0;
            for (int i = 0; i < 4; i++)
            {
                applied += TryRecommend(clone, context, recommendation.ActiveSkillList, i, ref skipped);
                applied += TryRecommend(clone, context, recommendation.PassiveSkillList, i, ref skipped);
            }
            if (applied == 0)
                return new ProgressionResult
                {
                    Status = ProgressionResultStatus.InvalidState,
                    Message = "\u63a8\u8350\u65b9\u6848\u4e2d\u6ca1\u6709\u5df2\u62e5\u6709\u4e14\u53ef\u5b89\u88c5\u7684\u6280\u80fd\u3002"
                };
            ProgressionResult result = session.ApplyCharacterMutation(player => player.Skills = clone,
                "skill-recommend-" + recommendation.Id.ToString(CultureInfo.InvariantCulture) + "-"
                + Guid.NewGuid().ToString("N"));
            if (result.IsApplied)
                result.Message = "\u5df2\u5b89\u88c5 " + applied.ToString(CultureInfo.InvariantCulture)
                    + " \u4e2a\u63a8\u8350\u6280\u80fd\uff0c\u8df3\u8fc7 "
                    + skipped.ToString(CultureInfo.InvariantCulture) + " \u4e2a\u672a\u62e5\u6709\u6280\u80fd\u3002";
            return result;
        }

        private int TryRecommend(OfflineSkillProfile profile, OfflineSkillPlayerContext context, int[] skills,
            int slot, ref int skipped)
        {
            if (skills == null || slot >= skills.Length) return 0;
            int skillId = skills[slot];
            if (skillId <= 0) return 0;
            if (!profile.Skills.Any(value => value != null && value.ClassId == skillId))
            {
                skipped++;
                return 0;
            }
            OfflineSkillResult result = SkillProgression.Equip(profile, context, skillId, slot);
            if (result.IsApplied) return 1;
            skipped++;
            return 0;
        }

        public OfflineSkillPlayerContext SkillContext()
        {
            CharacterExperienceSnapshot experience = Experience();
            return new OfflineSkillPlayerContext
            {
                Profession = Player.Wardrobe.Profession,
                SubRank = Player.SubRank,
                BaseLevel = Player.Level,
                PlayerRank = Player.Rank,
                CurrentSeasonRank = Player.SeasonRank,
                CurrentSeasonId = 0,
                PlayerBlessLevel = experience.BlessLevel,
                CurrentProfessionChain = ProfessionChain(Player.Wardrobe.Profession)
            };
        }

        private HashSet<int> ProfessionChain(int profession)
        {
            var chain = new HashSet<int>();
            int current = profession;
            while (current > 0 && chain.Add(current))
            {
                int parent = 0;
                foreach (Dictionary<string, object> row in catalog.Rows("profession_base"))
                {
                    if (Convert.ToInt32(row["Profession"], CultureInfo.InvariantCulture) != current) continue;
                    parent = Convert.ToInt32(row["PrePro"], CultureInfo.InvariantCulture);
                    break;
                }
                current = parent;
            }
            return chain;
        }

        public ProgressionResult SelectSkillPlan(int plan)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            OfflineSkillProfile clone = Player.Skills.Clone();
            OfflineSkillResult result = SkillProgression.SelectPlan(clone, plan);
            return ApplySkillProfile(clone, result, "skill-plan-" + plan.ToString(CultureInfo.InvariantCulture));
        }

        public ProgressionResult RenameSkillPlan(int plan, string name)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            OfflineSkillProfile clone = Player.Skills.Clone();
            OfflineSkillResult result = SkillProgression.RenamePlan(clone, plan, name);
            return ApplySkillProfile(clone, result, "skill-rename-" + plan.ToString(CultureInfo.InvariantCulture));
        }

        public ProgressionResult UpgradeSkillSlot(int itemType, int slot)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            return RunSkillMutation((profile, context) =>
                SkillProgression.UpgradeSlot(profile, context, itemType, slot),
                "skill-slot-upgrade-" + itemType.ToString(CultureInfo.InvariantCulture)
                + "-" + slot.ToString(CultureInfo.InvariantCulture));
        }

        public ProgressionResult UpgradeAllSkillSlots()
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            return RunSkillMutation((profile, context) =>
                SkillProgression.UpgradeAllSlots(profile, context), "skill-slot-upgrade-all");
        }

        public ProgressionResult PromoteSkill(int skillId)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            return RunSkillMutation((profile, context) =>
                SkillProgression.PromoteSkill(profile, context, skillId),
                "skill-promote-" + skillId.ToString(CultureInfo.InvariantCulture));
        }

        public OfflineSkillDefinition SkillDefinition(int skillId)
        {
            return SkillRules?.Skills?.FirstOrDefault(value => value.ClassId == skillId);
        }

        public ProgressionResult PromoteAllSkills()
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            return RunSkillMutation((profile, context) =>
                SkillProgression.PromoteAllSkills(profile, context), "skill-promote-all");
        }

        private ProgressionResult RunSkillMutation(
            Func<OfflineSkillProfile, OfflineSkillPlayerContext, OfflineSkillResult> operation, string eventId)
        {
            OfflineSkillProfile clone = Player.Skills.Clone();
            SyncSkillItems(clone, Player.Inventory);
            Dictionary<int, long> before = SkillItemCounts(clone);
            OfflineSkillResult result = operation(clone, SkillContext());
            if (result == null) return Invalid("The skill operation returned no result.");
            if (!result.IsApplied)
                return new ProgressionResult
                {
                    Status = result.Code == OfflineSkillResultCode.Unchanged
                        ? ProgressionResultStatus.Unchanged : ProgressionResultStatus.InvalidState,
                    Message = result.Message
                };
            Dictionary<int, long> after = SkillItemCounts(clone);
            ProgressionResult applied = session.ApplyCharacterMutation(player =>
            {
                player.Skills = clone;
                foreach (KeyValuePair<int, long> entry in after)
                {
                    before.TryGetValue(entry.Key, out long previous);
                    long consumed = previous - entry.Value;
                    if (consumed <= 0) continue;
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == entry.Key);
                    if (stack == null) throw new InvalidOperationException("A skill cost item is missing.");
                    stack.Count = Math.Max(0, stack.Count - consumed);
                }
            }, eventId + "-" + Guid.NewGuid().ToString("N"));
            return applied.IsApplied ? new ProgressionResult { Status = ProgressionResultStatus.Applied } : applied;
        }

        private static Dictionary<int, long> SkillItemCounts(OfflineSkillProfile profile)
        {
            var counts = new Dictionary<int, long>();
            foreach (OfflineSkillItemCount item in profile.Items)
                if (item != null && item.ItemId > 0) counts[item.ItemId] = item.Count;
            return counts;
        }

        private void SyncSkillItems(OfflineSkillProfile profile, List<OfflineItemStack> inventory)
        {
            profile.Items.Clear();
            foreach (OfflineItemStack stack in inventory)
            {
                if (stack == null || stack.Count <= 0) continue;
                if (stack.ItemId != 1 && catalog.ItemType(stack.ItemId) != 111) continue;
                profile.Items.Add(new OfflineSkillItemCount { ItemId = stack.ItemId, Count = stack.Count });
            }
        }

        public ProgressionResult ToggleSkillFavorite(int skillId)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            OfflineSkillProfile clone = Player.Skills.Clone();
            OfflineSkillResult result = SkillProgression.ToggleFavorite(clone, skillId);
            return ApplySkillProfile(clone, result,
                "skill-favorite-" + skillId.ToString(CultureInfo.InvariantCulture));
        }

        public ProgressionResult EquipSkill(int skillId, int slot)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            OfflineSkillProfile clone = Player.Skills.Clone();
            OfflineSkillResult result = SkillProgression.Equip(clone, SkillContext(), skillId, slot);
            return ApplySkillProfile(clone, result, "skill-equip-" + skillId.ToString(CultureInfo.InvariantCulture));
        }

        public ProgressionResult UnequipSkill(int itemType, int slot)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            OfflineSkillProfile clone = Player.Skills.Clone();
            OfflineSkillResult result = SkillProgression.Unequip(clone, itemType, slot);
            return ApplySkillProfile(clone, result, "skill-unequip-" + itemType.ToString(CultureInfo.InvariantCulture)
                + "-" + slot.ToString(CultureInfo.InvariantCulture));
        }

        private ProgressionResult ApplySkillProfile(OfflineSkillProfile profile, OfflineSkillResult result,
            string eventId)
        {
            if (result == null) return Invalid("The skill operation returned no result.");
            if (!result.IsApplied)
                return new ProgressionResult
                {
                    Status = result.Code == OfflineSkillResultCode.Unchanged
                        ? ProgressionResultStatus.Unchanged : ProgressionResultStatus.InvalidState,
                    Message = result.Message
                };
            ProgressionResult applied = session.ApplyCharacterMutation(player => player.Skills = profile,
                eventId + "-" + Guid.NewGuid().ToString("N"));
            return applied.IsApplied ? new ProgressionResult { Status = ProgressionResultStatus.Applied } : applied;
        }

        public CharacterPropSnapshot EvaluateProps(long nowUnixSeconds)
        {
            return propertyCalculator.Evaluate(Player, nowUnixSeconds);
        }

        public void AddPropertySource(IOfflineCharacterPropSource source)
        {
            propertyCalculator.AddSource(source);
        }
        public IReadOnlyDictionary<int, long> BaseBattleProperties()
        {
            return catalog.CalcCharacterBaseBattleProperties(Player.Level, Player.SubRank);
        }

        public IReadOnlyDictionary<int, long> AppearanceProperties(long nowUnixSeconds)
        {
            Dictionary<int, long> expiryTimes = Player.Wardrobe.Expiries
                .ToDictionary(value => value.ItemId, value => value.ExpiresAtUnixSeconds);
            return catalog.AppearanceProperties(Player.Wardrobe.AppearanceLevel, Player.Inventory,
                expiryTimes, nowUnixSeconds);
        }

        public ProgressionResult InitializeWardrobe(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A wardrobe initialization event ID is required.");
            return session.ApplyCharacterMutation(player =>
            {
                OfflineWardrobeProgress wardrobe = player.Wardrobe;
                if (wardrobe.BaseSkins.Count == 0)
                    wardrobe.BaseSkins = catalog.DefaultSkins(wardrobe.Sex, wardrobe.Profession)
                        .OrderBy(pair => pair.Key).Select(pair => new OfflineAppearanceSkin
                        { Type = pair.Key, Id = pair.Value }).ToList();
                var dyeByType = wardrobe.Dyes.ToDictionary(value => value.Type);
                foreach (KeyValuePair<int, OfflineAppearanceDye> dye in catalog.DefaultDyes())
                    if (!dyeByType.ContainsKey(dye.Key)) wardrobe.Dyes.Add(dye.Value);
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public CharacterExperienceSnapshot Experience()
        {
            return Experience(Player.Experience, Player.BlessingExperience);
        }

        public CharacterExperienceSnapshot Experience(long experience, long blessingExperience)
        {
            OfflinePlayerProgress player = Player;
            int rank = RankOf(player.SubRank);
            int highestSubRank = HighestSubRank(rank);
            int blessLimit = catalog.LevelLimits.Where(row => RankOf(row.SubRank) == rank)
                .Select(row => row.PlayerBlessLevel).DefaultIfEmpty(0).Max();
            bool blessingOpen = player.HasActiveSeasonRecord && player.SubRank == highestSubRank
                && blessLimit > 0 && player.Level >= OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits);
            int seasonBase = Math.Min(player.Level, OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits));
            bool atSeasonRankLimit = player.HasActiveSeasonRecord && player.SubRank >= highestSubRank;
            return OfflineCharacterExperience.Calculate(experience, blessingExperience,
                player.SubRank, player.SeasonRank, seasonBase, blessingOpen,
                player.HasActiveSeasonRecord && !atSeasonRankLimit,
                atSeasonRankLimit, blessLimit, catalog.CumulativeLevelExperience,
                catalog.LevelLimits, catalog.BlessLevels);
        }

        public ProgressionResult AwardExperience(long amount, string eventId)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A positive official experience award and event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                RefreshDerivedProgress(player);
                CharacterExperienceSnapshot current = Snapshot(player);
                long remainder = amount;
                if (current.IsBlessing)
                {
                    player.BlessingExperience = checked(player.BlessingExperience + remainder);
                    remainder = 0;
                }
                if (remainder > 0)
                {
                    long nextLimit = CumulativeAt(OfflineCharacterExperience.BaseLevelLimit(
                        player.SubRank + (current.IsStoredExperience ? 1 : 0), catalog.LevelLimits));
                    long accepted = current.IsStoredExperience
                        ? Math.Min(remainder, Math.Max(0, nextLimit - player.Experience)) : remainder;
                    player.Experience = checked(player.Experience + accepted);
                }
                RefreshDerivedProgress(player);
            }, eventId);
        }

        public ProgressionResult UpgradeSubRank(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A rank-up event ID is required.");
            Dictionary<string, object> nextRank = catalog.Find("player_subrank", Player.SubRank + 1, "SubRank");
            if (nextRank == null) return Invalid("The next official subrank is not configured.");
            return session.ApplyCharacterMutation(player =>
            {
                if (player.SubRank + 1 != Integer(nextRank, "SubRank"))
                    throw new InvalidOperationException("The character rank changed; reopen the rank panel.");
                int requiredLevel = OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits);
                if (player.Level < requiredLevel) throw new InvalidOperationException("The official level requirement is not met.");
                Dictionary<string, object> costs = nextRank.TryGetValue("Items", out object value)
                    && value is Dictionary<string, object> map ? map : new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> cost in costs)
                {
                    int itemId = int.Parse(cost.Key, CultureInfo.InvariantCulture);
                    long count = Convert.ToInt64(cost.Value, CultureInfo.InvariantCulture);
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(item => item.ItemId == itemId);
                    if (count < 0 || stack == null || stack.Count < count)
                        throw new InvalidOperationException("Required rank-up materials are missing.");
                }
                foreach (KeyValuePair<string, object> cost in costs)
                {
                    int itemId = int.Parse(cost.Key, CultureInfo.InvariantCulture);
                    long count = Convert.ToInt64(cost.Value, CultureInfo.InvariantCulture);
                    player.Inventory.First(item => item.ItemId == itemId).Count -= count;
                }
                int previousRank = player.Rank;
                player.SubRank++;
                player.Rank = RankOf(player.SubRank);
                if (player.Rank > previousRank)
                {
                    OfflineRankUnlockTime unlock = player.RankUnlockTimes.FirstOrDefault(value => value.Rank == player.Rank);
                    if (unlock == null)
                        player.RankUnlockTimes.Add(unlock = new OfflineRankUnlockTime { Rank = player.Rank });
                    unlock.UnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
                }
                RefreshDerivedProgress(player);
            }, eventId);
        }

        public ProgressionResult ClaimRankTutorial(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A tutorial claim event ID is required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (player.RankTutorialRewardClaimed) throw new InvalidOperationException("The rank tutorial reward was already claimed.");
                if (player.Level < 10) throw new InvalidOperationException("The rank tutorial is not unlocked.");
                if (!player.RankTutorialSeen) throw new InvalidOperationException("Read the rank tutorial before claiming its reward.");
                AddItem(player, 43003, 2);
                AddItem(player, 1, 2000);
                player.RankTutorialRewardClaimed = true;
            }, eventId);
        }

        public bool HasPendingRankTutorial => Player.Level >= 10 && !Player.RankTutorialRewardClaimed;

        public ProgressionResult MarkRankTutorialSeen(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A tutorial view event ID is required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (player.Level < 10) throw new InvalidOperationException("The rank tutorial is not unlocked.");
                player.RankTutorialSeen = true;
            }, eventId);
        }

        /// <summary>Original <c>Player.OnEquipUpdate</c>: wear an equipment instance and sync its appearance skin.</summary>
        public ProgressionResult WearEquipment(OfficialEquipmentCatalog equipment, long instanceId, string eventId)
        {
            if (equipment == null || instanceId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment instance and event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (!OfflineEquipmentOperations.Wear(player.Equipment, equipment, instanceId,
                        player.Wardrobe.Profession))
                    throw new InvalidOperationException("This equipment cannot be worn by the current character.");
                SynchronizeEquipmentSkins(player, equipment);
            }, eventId);
        }

        public ProgressionResult UpgradeEquipment(int position, string eventId)
        {
            if (EquipmentCatalog == null || position <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment position and event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                bool upgraded = OfflineEquipmentOperations.Upgrade(player, EquipmentCatalog, position,
                    player.SeasonRank, Experience().BlessLevel, player.Level, player.MaxProfessionRank);
                if (!upgraded) throw new InvalidOperationException("Equipment upgrade conditions are not met.");
            }, eventId);
        }

        public IReadOnlyList<int> GemCandidates(long instanceId)
        {
            return EquipmentCatalog == null ? Array.Empty<int>()
                : OfflineEquipmentOperations.GemCandidates(Player, EquipmentCatalog, instanceId);
        }

        public ProgressionResult InlayGem(long instanceId, int gemItemId, string eventId)
        {
            if (EquipmentCatalog == null || instanceId <= 0 || gemItemId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment, gem and event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (!OfflineEquipmentOperations.InlayGem(player, EquipmentCatalog, instanceId, gemItemId,
                        condition => EvaluateGemCondition(condition)))
                    throw new InvalidOperationException("The gem cannot be inlaid.");
            }, eventId);
        }

        public ProgressionResult TakeOffGem(long instanceId, int gemSlot, string eventId)
        {
            if (EquipmentCatalog == null || instanceId <= 0 || gemSlot < 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment, gem slot and event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (!OfflineEquipmentOperations.TakeOffGem(player, instanceId, gemSlot))
                    throw new InvalidOperationException("The gem cannot be removed.");
            }, eventId);
        }

        private bool EvaluateGemCondition(Dictionary<string, object> condition)
        {
            if (condition == null || EquipmentCatalog == null) return false;
            object rawType = condition.TryGetValue("_type", out object type) ? type : null;
            string conditionType = rawType == null ? string.Empty
                : Convert.ToString(rawType, CultureInfo.InvariantCulture);
            if (conditionType == "CompositeAndCondition")
            {
                if (!(condition.TryGetValue("Ids", out object ids) && ids is List<object> list)) return false;
                foreach (object id in list)
                    if (!EvaluateGemConditionId(Convert.ToInt32(id, CultureInfo.InvariantCulture))) return false;
                return true;
            }
            return EvaluateGemConditionNode(condition);
        }

        private bool EvaluateGemConditionId(int conditionId)
        {
            Dictionary<string, object> row = EquipmentCatalog.Find("conditions", conditionId);
            if (row == null || !(row.TryGetValue("Condition", out object value)
                && value is Dictionary<string, object> condition)) return false;
            return EvaluateGemCondition(condition);
        }

        private bool EvaluateGemConditionNode(Dictionary<string, object> condition)
        {
            string conditionType = Convert.ToString(condition.TryGetValue("_type", out object value) ? value : null,
                CultureInfo.InvariantCulture);
            int level = Convert.ToInt32(condition.TryGetValue("Level", out object levelValue) ? levelValue : 0,
                CultureInfo.InvariantCulture);
            switch (conditionType)
            {
                case "LevelCondition":
                    return Player.Level >= level;
                case "EquipLevelCondition":
                {
                    int position = Convert.ToInt32(condition.TryGetValue("EquipPos", out object pos) ? pos : 0,
                        CultureInfo.InvariantCulture);
                    OfflineEquipmentSlotProgress slot = Player.Equipment.SlotProgress
                        .FirstOrDefault(item => item.Position == position);
                    if (slot == null) return false;
                    int slotLevel = slot.BaseLevel + slot.BlessLevels
                        .Where(item => item.SeasonId == Player.SeasonRank).Select(item => item.Level)
                        .DefaultIfEmpty(0).Max();
                    return slotLevel >= level;
                }
                default:
                    return false;
            }
        }

        public ProgressionResult SetEquipmentLocked(long instanceId, bool locked, string eventId)
        {
            if (instanceId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment instance and event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (!OfflineEquipmentOperations.SetLocked(player.Equipment, instanceId, locked))
                    throw new InvalidOperationException("The equipment lock state cannot be changed.");
            }, eventId);
        }

        public ProgressionResult DecomposeEquipment(long instanceId, string eventId,
            out IReadOnlyDictionary<int, long> rewards)
        {
            rewards = new Dictionary<int, long>();
            if (EquipmentCatalog == null || instanceId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment instance and event ID are required.");
            IReadOnlyDictionary<int, long> gained = null;
            ProgressionResult result = session.ApplyCharacterMutation(player =>
            {
                gained = OfflineEquipmentOperations.Decompose(player, EquipmentCatalog, instanceId);
                if (gained == null) throw new InvalidOperationException("The equipment cannot be decomposed.");
            }, eventId);
            if (gained != null) rewards = gained;
            return result;
        }

        public ProgressionResult TakeOffEquipped(int position, string eventId)
        {
            if (EquipmentCatalog == null || position <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment position and event ID are required.");
            return TakeOffEquipment(EquipmentCatalog, position, eventId);
        }

        public EquipmentUpgradeQuote QuoteEquipmentUpgrade(int position)
        {
            if (EquipmentCatalog == null || position <= 0) return null;
            return OfflineEquipmentOperations.QuoteUpgrade(Player, EquipmentCatalog, position,
                Player.SeasonRank, Experience().BlessLevel, Player.Level, Player.MaxProfessionRank);
        }

        public ProgressionResult TakeOffEquipment(OfficialEquipmentCatalog equipment, int position, string eventId)
        {
            if (equipment == null || position <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment position and event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                if (!OfflineEquipmentOperations.TakeOff(player.Equipment, position))
                    throw new InvalidOperationException("There is no equipment in this slot.");
                SynchronizeEquipmentSkins(player, equipment);
            }, eventId);
        }

        /// <summary>
        /// Original <c>EquipSkinDict</c>: worn equipment supplies the body and weapon skins, while
        /// the remaining parts keep the wardrobe or base skin.
        /// </summary>
        private void SynchronizeEquipmentSkins(OfflinePlayerProgress player, OfficialEquipmentCatalog equipment)
        {
            player.Wardrobe.WornSkins.RemoveAll(skin => skin.Type == 5 || skin.Type == 7 || skin.Type == 8);
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped.OrderBy(value => value.Position))
            {
                OfflineEquipmentInstance instance = player.Equipment.Instances
                    .FirstOrDefault(value => value.InstanceId == slot.InstanceId);
                if (instance == null) continue;
                Dictionary<string, object> row = equipment.Find("equip", instance.ItemId);
                if (row == null || !row.TryGetValue("AppendAppearanceItemId", out object raw)
                    || Convert.ToInt32(raw, CultureInfo.InvariantCulture) <= 0) continue;
                int appearanceItemId = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                if (!catalog.Appearances.TryGetValue(appearanceItemId, out WardrobeItemDefinition appearance)
                    || appearance.SkinBySex == null) continue;
                if (!appearance.SkinBySex.TryGetValue(player.Wardrobe.Sex, out int skinId)
                    || skinId <= 0) continue;
                int type = slot.Position == 10 ? 7 : slot.Position == 14 ? 8 : slot.Position == 12 ? 5 : 0;
                if (type == 0) continue;
                player.Wardrobe.WornSkins.RemoveAll(skin => skin.Type == type);
                player.Wardrobe.WornSkins.Add(new OfflineAppearanceSkin { Type = type, Id = skinId });
            }
            player.Wardrobe.WornSkins = player.Wardrobe.WornSkins.OrderBy(skin => skin.Type).ToList();
        }

        public ProgressionResult WearAppearance(int itemId, long nowUnixSeconds, string eventId)
        {
            return session.ApplyCharacterMutation(player =>
            {
                WardrobeAppearanceState state = ReadAppearance(player.Wardrobe);
                var owned = player.Inventory.ToDictionary(item => item.ItemId, item => item.Count);
                if (!OfflineWardrobeRules.TryWear(state, itemId, owned, catalog.Appearances,
                    catalog.Skins, catalog.AllowedSkins, nowUnixSeconds))
                    throw new InvalidOperationException("This appearance cannot be worn by the current character.");
                WriteAppearance(player.Wardrobe, state);
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public ProgressionResult TakeOffAppearance(int type, string eventId)
        {
            return session.ApplyCharacterMutation(player =>
            {
                WardrobeAppearanceState state = ReadAppearance(player.Wardrobe);
                if (!OfflineWardrobeRules.TakeOff(state, type))
                    throw new InvalidOperationException("There is no wardrobe item in this slot.");
                WriteAppearance(player.Wardrobe, state);
            }, eventId);
        }

        public ProgressionResult WearTitle(int itemId, string eventId)
        {
            return session.ApplyCharacterMutation(player =>
            {
                if (!IsOwned(player, itemId) || ItemType(itemId) != 339)
                    throw new InvalidOperationException("The selected title is not owned.");
                player.Wardrobe.WornTitleId = itemId;
                if (Expiry(player.Wardrobe, itemId) > 0 && !player.Wardrobe.LimitedTitleCollection.Contains(itemId))
                    player.Wardrobe.LimitedTitleCollection.Add(itemId);
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public ProgressionResult TakeOffTitle(string eventId)
        {
            return session.ApplyCharacterMutation(player => player.Wardrobe.WornTitleId = 0, eventId);
        }

        public ProgressionResult SetDye(int type, int colorId, int hue, int saturation, int value,
            string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || !catalog.IsValidDye(type, colorId, hue, saturation, value))
                return Invalid("The selected color is outside the official dye configuration.");
            return session.ApplyCharacterMutation(player =>
            {
                OfflineAppearanceDye dye = player.Wardrobe.Dyes.FirstOrDefault(entry => entry.Type == type);
                if (dye == null)
                {
                    dye = new OfflineAppearanceDye { Type = type };
                    player.Wardrobe.Dyes.Add(dye);
                }
                dye.ColorId = colorId;
                dye.Hue = hue;
                dye.Saturation = saturation;
                dye.Value = value;
            }, eventId);
        }

        public ProgressionResult RecordTimedAppearanceAward(int itemId, int count, long nowUnixSeconds,
            string eventId)
        {
            if (itemId <= 0 || count <= 0 || nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(eventId)
                || !catalog.Appearances.ContainsKey(itemId) && catalog.ItemType(itemId) != 339)
                return Invalid("A valid timed appearance award is required.");
            Dictionary<string, object> config = catalog.FindTimedAppearance(itemId);
            if (config == null || Integer(config, "Duration") <= 0)
                return Invalid("The appearance has no supported official time limit configuration.");
            return session.ApplyCharacterMutation(player =>
            {
                AddItem(player, itemId, count);
                OfflineTimedAppearance expiry = player.Wardrobe.Expiries.FirstOrDefault(value => value.ItemId == itemId);
                long previous = expiry == null ? nowUnixSeconds : expiry.ExpiresAtUnixSeconds;
                long next = OfflineCharacterExperience.ExtendTimedAppearance(nowUnixSeconds, previous,
                    Integer(config, "Duration"), Integer(config, "MaxDuration"), count,
                    Boolean(config, "CanOverlapTime"));
                if (expiry == null)
                {
                    expiry = new OfflineTimedAppearance { ItemId = itemId };
                    player.Wardrobe.Expiries.Add(expiry);
                }
                expiry.ExpiresAtUnixSeconds = next;
                if (catalog.ItemType(itemId) == 339 && !player.Wardrobe.LimitedTitleCollection.Contains(itemId))
                    player.Wardrobe.LimitedTitleCollection.Add(itemId);
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public ProgressionResult SynchronizeExpiredItems(long nowUnixSeconds, string eventId)
        {
            if (Player.Wardrobe.Expiries.All(expiry => nowUnixSeconds <= expiry.ExpiresAtUnixSeconds))
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            return session.ApplyCharacterMutation(player =>
            {
                foreach (OfflineTimedAppearance expiry in player.Wardrobe.Expiries.ToArray())
                {
                    if (nowUnixSeconds <= expiry.ExpiresAtUnixSeconds) continue;
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(item => item.ItemId == expiry.ItemId);
                    if (stack != null) player.Inventory.Remove(stack);
                    player.Wardrobe.WornItems.RemoveAll(item => ItemIdForType(player.Wardrobe, item.Type) == expiry.ItemId);
                    player.Wardrobe.WearTimes.RemoveAll(item => !player.Wardrobe.WornItems.Any(worn => worn.Type == item.Type));
                    if (player.Wardrobe.WornTitleId == expiry.ItemId) player.Wardrobe.WornTitleId = 0;
                    player.Wardrobe.Expiries.Remove(expiry);
                }
                RefreshWardrobeProgress(player);
            }, eventId);
        }

        public WardrobeResolution ResolveAppearance()
        {
            return OfflineWardrobeRules.Resolve(ReadAppearance(Player.Wardrobe),
                catalog.Appearances, catalog.Skins, catalog.AllowedSkins);
        }

        public WardrobeConflictTip PreviewAppearanceConflict(int itemId, long nowUnixSeconds)
        {
            return OfflineWardrobeRules.PreviewWear(ReadAppearance(Player.Wardrobe), itemId, nowUnixSeconds,
                catalog.Appearances, catalog.Skins, catalog.AllowedSkins);
        }

        public bool CanWearAppearance(int itemId, long nowUnixSeconds)
        {
            if (Player.Wardrobe.WornItems.Any(item => item.Id == itemId)) return true;
            var owned = Player.Inventory.ToDictionary(item => item.ItemId, item => item.Count);
            if (!owned.ContainsKey(itemId)) owned[itemId] = 1;
            WardrobeAppearanceState state = ReadAppearance(Player.Wardrobe);
            return OfflineWardrobeRules.TryWear(state, itemId, owned, catalog.Appearances,
                catalog.Skins, catalog.AllowedSkins, nowUnixSeconds);
        }

        private CharacterExperienceSnapshot Snapshot(OfflinePlayerProgress player)
        {
            return OfflineCharacterExperience.Calculate(player.Experience, player.BlessingExperience,
                player.SubRank, player.SeasonRank, Math.Min(player.Level,
                    OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits)),
                player.HasActiveSeasonRecord && player.SubRank == HighestSubRank(RankOf(player.SubRank)),
                player.HasActiveSeasonRecord, player.HasActiveSeasonRecord
                    && player.SubRank >= HighestSubRank(player.SeasonRank), BlessLimit(player.SeasonRank),
                catalog.CumulativeLevelExperience, catalog.LevelLimits, catalog.BlessLevels);
        }

        private int BlessLimit(int rank)
        {
            return catalog.LevelLimits.Where(row => RankOf(row.SubRank) == rank)
                .Select(row => row.PlayerBlessLevel).DefaultIfEmpty(0).Max();
        }

        private int HighestSubRank(int rank)
        {
            return catalog.Rows("player_subrank").Select(row => Integer(row, "SubRank"))
                .Where(value => RankOf(value) == rank).DefaultIfEmpty(0).Max();
        }

        private long CumulativeAt(int level)
        {
            IReadOnlyList<long> values = catalog.CumulativeLevelExperience;
            return values[Math.Max(0, Math.Min(level, values.Count - 1))];
        }

        private void RefreshDerivedProgress(OfflinePlayerProgress player)
        {
            player.Rank = RankOf(player.SubRank);
            CharacterExperienceSnapshot snapshot = Snapshot(player);
            player.Level = snapshot.Level;
            player.SeasonBaseLevel = OfflineCharacterExperience.BaseLevelLimit(player.SubRank, catalog.LevelLimits);
            RefreshWardrobeProgress(player);
        }

        private void RefreshWardrobeProgress(OfflinePlayerProgress player)
        {
            player.Wardrobe.FashionExperience = catalog.FashionExperience(player.Inventory,
                player.Wardrobe.LimitedTitleCollection);
            int calculated = catalog.AppearanceLevel(player.Wardrobe.FashionExperience);
            // Original SyncAppearanceLevelFromExp only lowers the stored level; raising it requires
            // the wardrobe upgrade button (CanUpgradeAppearanceLevel / TryUpgradeAppearanceLevel).
            if (calculated < player.Wardrobe.AppearanceLevel)
                player.Wardrobe.AppearanceLevel = calculated;
        }

        public int AppearanceLevelMax => catalog.AppearanceLevelMax;

        public bool IsMaxAppearanceLevel => catalog.IsMaxAppearanceLevel(Player.Wardrobe.AppearanceLevel);

        public bool CanUpgradeAppearanceLevel(out int level, out int newLevel)
        {
            level = Player.Wardrobe.AppearanceLevel;
            newLevel = catalog.AppearanceLevel(Player.Wardrobe.FashionExperience);
            return newLevel > level;
        }

        public float AppearanceLevelFill()
        {
            int level = Player.Wardrobe.AppearanceLevel;
            if (catalog.IsMaxAppearanceLevel(level)) return 1f;
            long current = Player.Wardrobe.FashionExperience - catalog.AppearanceLevelExperience(level);
            long required = catalog.AppearanceLevelNextExperience(level) - catalog.AppearanceLevelExperience(level);
            return required <= 0 ? 0f : Math.Max(0f, Math.Min(1f, (float)current / required));
        }

        public ProgressionResult UpgradeAppearanceLevel(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("An appearance level event ID is required.");
            return session.ApplyCharacterMutation(player =>
            {
                int level = player.Wardrobe.AppearanceLevel;
                long experience = catalog.FashionExperience(player.Inventory, player.Wardrobe.LimitedTitleCollection);
                int newLevel = catalog.AppearanceLevel(experience);
                if (newLevel <= level)
                    throw new InvalidOperationException("The appearance experience is not enough to upgrade.");
                player.Wardrobe.FashionExperience = experience;
                player.Wardrobe.AppearanceLevel = newLevel;
            }, eventId);
        }

        public IReadOnlyDictionary<int, long> AppearancePropertiesGained(int fromLevel, int toLevel)
        {
            return catalog.AppearancePropertiesBetween(fromLevel, toLevel);
        }

        private WardrobeAppearanceState ReadAppearance(OfflineWardrobeProgress saved)
        {
            var state = new WardrobeAppearanceState { Sex = saved.Sex, Profession = saved.Profession };
            foreach (OfflineAppearanceSkin value in saved.BaseSkins) state.BaseSkinByType[value.Type] = value.Id;
            foreach (OfflineAppearanceSkin value in saved.WornSkins) state.EquippedSkinBySlot[value.Type] = value.Id;
            foreach (OfflineAppearanceSkin value in saved.WornItems) state.AppendedItemByType[value.Type] = value.Id;
            foreach (OfflineAppearanceWearTime value in saved.WearTimes) state.WearTimeByType[value.Type] = value.UnixSeconds;
            foreach (OfflineAppearanceDye value in saved.Dyes) state.DyeByType[value.Type] = value;
            return state;
        }

        private static void WriteAppearance(OfflineWardrobeProgress saved, WardrobeAppearanceState state)
        {
            saved.WornSkins = state.EquippedSkinBySlot.OrderBy(pair => pair.Key)
                .Select(pair => new OfflineAppearanceSkin { Type = pair.Key, Id = pair.Value }).ToList();
            saved.WornItems = state.AppendedItemByType.OrderBy(pair => pair.Key)
                .Select(pair => new OfflineAppearanceSkin { Type = pair.Key, Id = pair.Value }).ToList();
            saved.WearTimes = state.WearTimeByType.OrderBy(pair => pair.Key)
                .Select(pair => new OfflineAppearanceWearTime { Type = pair.Key, UnixSeconds = pair.Value }).ToList();
            saved.Dyes = state.DyeByType.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        }

        private bool IsOwned(OfflinePlayerProgress player, int itemId)
        {
            return player.Inventory.Any(item => item.ItemId == itemId && item.Count > 0);
        }

        private int ItemType(int itemId)
        {
            return catalog.ItemType(itemId);
        }

        private static long Expiry(OfflineWardrobeProgress wardrobe, int itemId)
        {
            OfflineTimedAppearance expiry = wardrobe.Expiries.FirstOrDefault(value => value.ItemId == itemId);
            return expiry == null ? 0 : expiry.ExpiresAtUnixSeconds;
        }

        private static int ItemIdForType(OfflineWardrobeProgress wardrobe, int type)
        {
            OfflineAppearanceSkin entry = wardrobe.WornItems.FirstOrDefault(value => value.Type == type);
            return entry == null ? 0 : entry.Id;
        }

        private static void AddItem(OfflinePlayerProgress player, int itemId, long count)
        {
            OfflineItemStack stack = player.Inventory.FirstOrDefault(item => item.ItemId == itemId);
            if (stack == null)
            {
                stack = new OfflineItemStack { ItemId = itemId };
                player.Inventory.Add(stack);
            }
            stack.Count = checked(stack.Count + count);
        }

        private int RankOf(int subRank)
        {
            return catalog.RankOfSubRank(subRank);
        }

        private static int Integer(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static bool Boolean(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) && value is bool result && result;
        }

        private static ProgressionResult Invalid(string message)
        {
            return new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = message };
        }
    }
}
