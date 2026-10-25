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
        private static readonly int[] ConfiguredPropertyHandles =
        {
            CharacterPropHandleType.Treasure, CharacterPropHandleType.TreasureSuit,
            CharacterPropHandleType.Cooking, CharacterPropHandleType.Extra,
            CharacterPropHandleType.NpcFriendshipLevel, CharacterPropHandleType.NpcTalentTree,
            CharacterPropHandleType.LostGoddess, CharacterPropHandleType.PeriodIcCar,
            CharacterPropHandleType.Legion, CharacterPropHandleType.EquipCabinet,
            CharacterPropHandleType.Astrological
        };

        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialCharacterCatalog catalog;
        private readonly CharacterPropCalculator propertyCalculator;
        private readonly OfflinePetPropSource petPropertySource;

        public OfflineCharacterProgressionService(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog catalog)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            propertyCalculator = new CharacterPropCalculator(catalog);
            foreach (int handleType in ConfiguredPropertyHandles)
                propertyCalculator.AddSource(new OfflineCharacterConfiguredPropSource(catalog, handleType));
            propertyCalculator.AddSource(new OfflineGoddessPropSource(catalog, () => session.Save.World));
            petPropertySource = new OfflinePetPropSource(catalog);
            propertyCalculator.AddSource(petPropertySource);
            PetProgression = new OfflinePetProgressionService(session, CalculatePetCombatRating);
            LegionBuildings = new OfflineLegionBuildingProgressionService(session, catalog);
            RefreshDerivedProgress(session.Save.Player);
        }

        public OfflinePlayerProgress Player => session.Save.Player;
        public OfficialCharacterCatalog Catalog => catalog;
        public int FriendshipLevel(int npcClassId, long experience, int subRank)
        {
            return session.FriendshipLevel(npcClassId, experience, subRank);
        }

        public IReadOnlyList<int> MeetableNpcCandidates()
        {
            return catalog.Rows("npc").Select(row => Integer(row, "Id"))
                .Where(npcId => npcId > 0 && CanMeetNpc(npcId))
                .Distinct().OrderBy(npcId => npcId).ToArray();
        }

        public bool CanMeetNpc(int npcClassId)
        {
            Dictionary<string, object> npc = catalog.Find("npc", npcClassId, "Id");
            if (npc == null || (Player.CharacterProperties?.NpcFriendships
                ?? new List<OfflineNpcFriendship>()).Any(value => value != null
                    && value.NpcClassId == npcClassId)) return false;
            return TryNpcMeetRequirements(npc, Player, out _);
        }

        public IReadOnlyDictionary<int, long> NpcMeetItemRequirements(int npcClassId)
        {
            Dictionary<string, object> npc = catalog.Find("npc", npcClassId, "Id");
            return npc != null && TryNpcMeetRequirements(npc, Player, out Dictionary<int, long> items,
                requireConditionMet: false)
                ? items : new Dictionary<int, long>();
        }

        public ProgressionResult MeetNpc(int npcClassId, string eventId)
        {
            if (npcClassId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid NPC and meet event ID are required.");
            if (Player.AppliedCharacterEventIds.Contains(eventId))
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            Dictionary<string, object> npc = catalog.Find("npc", npcClassId, "Id");
            if (npc == null || !CanMeetNpc(npcClassId))
                return Invalid("The NPC meet condition is not satisfied or the NPC was already met.");
            Dictionary<int, long> requiredItems = NpcMeetItemRequirements(npcClassId)
                .ToDictionary(value => value.Key, value => value.Value);
            int defaultLevel = Integer(catalog.Rows("game_settings").FirstOrDefault(),
                "NpcDefaultFriendshipLevel");
            Dictionary<string, object> initialFriendship = catalog.Rows("npc_friendship_level")
                .FirstOrDefault(value => Integer(value, "Id") == npcClassId
                    && Integer(value, "Level") == defaultLevel);
            long initialExperience = Long(initialFriendship, "FriendshipExp");
            if (defaultLevel <= 0 || initialFriendship == null || initialExperience < 0)
                return Invalid("The official initial NPC friendship state is unavailable.");

            return session.ApplyCharacterMutation(player =>
            {
                OfflineCharacterPropertyProgress properties = player.CharacterProperties
                    ?? (player.CharacterProperties = new OfflineCharacterPropertyProgress());
                OfflineCharacterPropertyProgress.Normalize(properties);
                if (properties.NpcFriendships.Any(value => value.NpcClassId == npcClassId))
                    throw new InvalidOperationException("This NPC has already been met.");
                if (!TryNpcMeetRequirements(npc, player, out Dictionary<int, long> currentRequirements))
                    throw new InvalidOperationException("The NPC meet condition changed before the transaction was saved.");
                foreach (KeyValuePair<int, long> requirement in currentRequirements)
                {
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == requirement.Key);
                    if (stack == null || stack.Count < requirement.Value)
                        throw new InvalidOperationException("NPC meet items changed before the transaction was saved.");
                }
                foreach (KeyValuePair<int, long> requirement in currentRequirements)
                    player.Inventory.First(value => value.ItemId == requirement.Key).Count -= requirement.Value;
                properties.NpcFriendships.Add(new OfflineNpcFriendship
                {
                    NpcClassId = npcClassId,
                    Level = defaultLevel,
                    FriendshipExp = initialExperience
                });
            }, eventId);
        }

        public CharacterPropCalculator PropertyCalculator => propertyCalculator;
        public OfficialEquipmentCatalog EquipmentCatalog { get; private set; }
        public OfflineGemForgeService GemForge { get; private set; }
        public OfflinePetProgressionService PetProgression { get; }
        public OfflineLegionBuildingProgressionService LegionBuildings { get; }

        public void AttachEquipmentCatalog(OfficialEquipmentCatalog equipment)
        {
            EquipmentCatalog = equipment;
            GemForge = null;
            if (equipment != null)
            {
                var equipmentProperties = new OfflineEquipmentPropSource(equipment, catalog.RankOfSubRank);
                propertyCalculator.AddSource(equipmentProperties);
                propertyCalculator.AddSource(new OfflineEquipmentResonancePropSource(equipmentProperties));
                propertyCalculator.AddSource(new OfflineGemPropSource(equipment));
                int moneyItemId = 0;
                if (equipment.GameSettings is IDictionary<string, object> settings
                    && settings.TryGetValue("MoneyId", out object value))
                    moneyItemId = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                GemForge = new OfflineGemForgeService(session, equipment,
                    OfflineGemForgeService.LoadDefault(), moneyItemId);
            }
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
            if (rules != null) propertyCalculator.AddSource(new OfflineSkillPropSource(catalog, rules));
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
                // Single-player seasons are represented by the current season rank, matching the
                // equipment blessing records that already use Player.SeasonRank as their season id.
                CurrentSeasonId = Player.SeasonRank,
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

        public int PreviewUpgradeAllSkillSlots()
        {
            return PreviewUpgradeAllSkillSlotChanges().Count;
        }

        public IReadOnlyList<OfflineSkillSlotUpgradeChange> PreviewUpgradeAllSkillSlotChanges()
        {
            if (SkillProgression == null) return Array.Empty<OfflineSkillSlotUpgradeChange>();
            OfflineSkillProfile profile = Player.Skills.Clone();
            SyncSkillItems(profile, Player.Inventory);
            OfflineSkillPlayerContext context = SkillContext();
            var changes = new List<OfflineSkillSlotUpgradeChange>();
            while (true)
            {
                int itemType = 100;
                int slot = 0;
                int minimum = SkillProgression.SlotLevel(profile, itemType, slot, context.CurrentSeasonId);
                foreach (int kind in new[] { 100, 101 })
                    for (int index = 0; index < 4; index++)
                    {
                        int level = SkillProgression.SlotLevel(profile, kind, index, context.CurrentSeasonId);
                        if (level < minimum) { itemType = kind; slot = index; minimum = level; }
                    }
                OfflineSkillResult result = SkillProgression.UpgradeSlot(profile, context, itemType, slot);
                if (result == null || !result.IsApplied) break;
                changes.Add(new OfflineSkillSlotUpgradeChange
                {
                    ItemType = itemType,
                    Slot = slot,
                    FromLevel = minimum,
                    ToLevel = SkillProgression.SlotLevel(profile, itemType, slot, context.CurrentSeasonId)
                });
            }
            return changes;
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

        public int SkillMergePiece(int skillId)
        {
            return catalog.SkillMergePiece(skillId);
        }

        /// <summary>Merges the official skill piece count into the skill item and records ownership.</summary>
        public ProgressionResult MergeSkill(int skillId)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            OfflineSkillDefinition definition = SkillDefinition(skillId);
            if (definition == null || definition.PieceItemId <= 0)
                return Invalid("The selected skill cannot be merged.");
            if (Player.Skills.Skills.Any(value => value != null && value.ClassId == skillId))
                return Invalid("The skill is already owned.");
            int pieces = catalog.SkillMergePiece(skillId);
            OfflineItemStack stack = Player.Inventory.FirstOrDefault(value => value.ItemId == definition.PieceItemId);
            if (pieces <= 0 || stack == null || stack.Count < pieces)
                return Invalid("Not enough skill pieces.");
            return session.ApplyCharacterMutation(player =>
            {
                OfflineItemStack target = player.Inventory
                    .FirstOrDefault(value => value.ItemId == definition.PieceItemId);
                if (target == null || target.Count < pieces)
                    throw new InvalidOperationException("Not enough skill pieces.");
                target.Count -= pieces;
                OfflineItemStack skill = player.Inventory.FirstOrDefault(value => value.ItemId == skillId);
                if (skill == null) player.Inventory.Add(new OfflineItemStack { ItemId = skillId, Count = 1 });
                else skill.Count += 1;
                player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = skillId, Rank = 1 });
            }, "skill-merge-" + skillId.ToString(CultureInfo.InvariantCulture) + "-"
                + Guid.NewGuid().ToString("N"));
        }

        public ProgressionResult SwapSkillSlots(int itemType, int fromSlot, int toSlot)
        {
            if (SkillProgression == null) return Invalid("Official skill rules are unavailable.");
            if ((itemType != 100 && itemType != 101) || fromSlot < 0 || fromSlot > 3 || toSlot < 0 || toSlot > 3)
                return Invalid("A valid skill type and slot pair are required.");
            OfflineSkillProfile clone = Player.Skills.Clone();
            OfflineSkillPlan plan = clone.Plans[clone.SelectedPlan];
            int[] slots = itemType == 100 ? plan.ActiveSkills : plan.PassiveSkills;
            if (slots == null || fromSlot >= slots.Length || toSlot >= slots.Length)
                return Invalid("The selected skill plan has no such slot.");
            if (slots[fromSlot] == slots[toSlot])
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            int swapped = slots[fromSlot];
            slots[fromSlot] = slots[toSlot];
            slots[toSlot] = swapped;
            return session.ApplyCharacterMutation(player => player.Skills = clone,
                "skill-swap-" + itemType.ToString(CultureInfo.InvariantCulture) + "-"
                + fromSlot.ToString(CultureInfo.InvariantCulture) + "-"
                + toSlot.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
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
            return propertyCalculator.Evaluate(Player, session.Save.World, nowUnixSeconds);
        }

        public void AddPropertySource(IOfflineCharacterPropSource source)
        {
            propertyCalculator.AddSource(source);
        }

        public bool CanActivateNpcTalentNode(int npcClassId, int nodeId)
        {
            return CanActivateNpcTalentNode(Player, npcClassId, nodeId);
        }

        public ProgressionResult ActivateNpcTalentNode(int npcClassId, int nodeId, string eventId)
        {
            if (npcClassId <= 0 || nodeId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid NPC talent node and event ID are required.");
            if (!CanActivateNpcTalentNode(Player, npcClassId, nodeId))
                return Invalid("The NPC talent node is locked or its talent points are insufficient.");

            return session.ApplyCharacterMutation(player =>
            {
                Dictionary<string, object> node = FindNpcTalentNode(npcClassId, nodeId);
                Dictionary<string, object> npcTalent = FindNpcTalentItem(npcClassId);
                OfflineNpcTalentTree tree = GetOrCreateNpcTalentTree(player, npcClassId);
                int talentItemId = npcTalent == null ? 0 : Integer(npcTalent, "TalentItemId");
                long cost = node == null ? 0 : Long(node, "CostTalentPointCount");
                OfflineItemStack points = player.Inventory.FirstOrDefault(value => value.ItemId == talentItemId);
                if (!CanActivateNpcTalentNode(player, npcClassId, nodeId)
                    || cost > 0 && (points == null || points.Count < cost))
                    throw new InvalidOperationException("The NPC talent node changed before the operation was saved.");
                if (cost > 0) points.Count -= cost;
                tree.ActiveNodeIds.Add(nodeId);
            }, eventId);
        }

        public ProgressionResult TransformNpcGiftsToTalentPoints(int npcClassId,
            IReadOnlyDictionary<int, long> gifts, string eventId)
        {
            if (npcClassId <= 0 || gifts == null || gifts.Count == 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("Select NPC gifts and provide a valid event ID.");

            Dictionary<string, object> npcTalent = FindNpcTalentItem(npcClassId);
            Dictionary<string, object> gameSettings = catalog.Rows("game_settings").FirstOrDefault();
            if (npcTalent == null || gameSettings == null
                || !gameSettings.TryGetValue("NpcTalentTreeSetting", out object rawTalentSettings)
                || !(rawTalentSettings is Dictionary<string, object> talentSettings)
                || !talentSettings.TryGetValue("GiftQuality2TalentExp", out object rawGiftExp)
                || !(rawGiftExp is Dictionary<string, object> giftExpByQuality))
                return Invalid("Official NPC talent gift settings are unavailable.");

            int ratio = Integer(talentSettings, "TalentExp2TalentPointRatio");
            int talentItemId = Integer(npcTalent, "TalentItemId");
            int transformConditionId = Integer(npcTalent, "TransformGiftUnlockConditionId");
            if (ratio <= 0 || talentItemId <= 0
                || transformConditionId > 0
                    && !catalog.EvaluateCondition(transformConditionId, NpcTalentConditionContext(Player, npcClassId, 0)))
                return Invalid("NPC talent gift conversion is not unlocked.");

            var selected = new Dictionary<int, long>();
            long addedExp = 0;
            foreach (KeyValuePair<int, long> gift in gifts)
            {
                if (gift.Key <= 0 || gift.Value <= 0 || catalog.Find("npc_gift", gift.Key) == null)
                    return Invalid("Only owned official NPC gifts can be converted.");
                OfflineItemStack owned = Player.Inventory.FirstOrDefault(value => value.ItemId == gift.Key);
                Dictionary<string, object> item = catalog.FindItem(gift.Key);
                if (owned == null || owned.Count < gift.Value || item == null
                    || !giftExpByQuality.TryGetValue(Integer(item, "Quality").ToString(CultureInfo.InvariantCulture),
                        out object rawExp))
                    return Invalid("An NPC gift is missing or has no official talent experience value.");
                long expPerGift = Convert.ToInt64(rawExp, CultureInfo.InvariantCulture);
                if (expPerGift < 0) return Invalid("An NPC gift has an invalid talent experience value.");
                addedExp = checked(addedExp + checked(expPerGift * gift.Value));
                selected.Add(gift.Key, gift.Value);
            }
            if (addedExp <= 0) return Invalid("The selected NPC gifts provide no talent experience.");

            OfflineCharacterPropertyProgress properties = Player.CharacterProperties
                ?? new OfflineCharacterPropertyProgress();
            OfflineNpcTalentTree currentTree = (properties.NpcTalentTrees ?? new List<OfflineNpcTalentTree>())
                .FirstOrDefault(value => value != null && value.NpcClassId == npcClassId);
            long currentExp = currentTree == null ? 0 : currentTree.TalentExp;
            long unspentPoints = InventoryCount(Player, talentItemId);
            long remainingPoints = Math.Max(0L,
                LockedNpcTalentPointCost(npcClassId, currentTree?.ActiveNodeIds) - unspentPoints);
            if (remainingPoints <= 0) return Invalid("This NPC talent tree has reached its point limit.");

            long totalExp = checked(currentExp + addedExp);
            long gainedPoints = Math.Min(totalExp / ratio, remainingPoints);
            long remainingExp = totalExp / ratio >= remainingPoints ? 0 : totalExp % ratio;
            return session.ApplyCharacterMutation(player =>
            {
                OfflineNpcTalentTree tree = GetOrCreateNpcTalentTree(player, npcClassId);
                foreach (KeyValuePair<int, long> gift in selected)
                {
                    OfflineItemStack stack = player.Inventory.First(value => value.ItemId == gift.Key);
                    if (stack.Count < gift.Value)
                        throw new InvalidOperationException("NPC gifts changed before conversion was saved.");
                    stack.Count -= gift.Value;
                }
                if (gainedPoints > 0)
                {
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == talentItemId);
                    if (stack == null)
                        player.Inventory.Add(new OfflineItemStack { ItemId = talentItemId, Count = gainedPoints });
                    else
                        stack.Count = checked(stack.Count + gainedPoints);
                }
                tree.TalentExp = remainingExp;
            }, eventId);
        }

        private bool CanActivateNpcTalentNode(OfflinePlayerProgress player, int npcClassId, int nodeId)
        {
            Dictionary<string, object> node = FindNpcTalentNode(npcClassId, nodeId);
            Dictionary<string, object> npcTalent = FindNpcTalentItem(npcClassId);
            if (node == null || npcTalent == null) return false;
            int nodeType = Integer(node, "NodeType");
            if (nodeType == 0 || nodeType == 3) return false;
            OfflineNpcTalentTree tree = (player.CharacterProperties?.NpcTalentTrees
                ?? new List<OfflineNpcTalentTree>()).FirstOrDefault(value => value != null
                    && value.NpcClassId == npcClassId);
            if (tree?.ActiveNodeIds?.Contains(nodeId) == true) return false;
            int conditionId = Integer(node, "UnlockCondition");
            if (conditionId > 0 && !catalog.EvaluateCondition(conditionId,
                    NpcTalentConditionContext(player, npcClassId, nodeId))) return false;
            int talentItemId = Integer(npcTalent, "TalentItemId");
            long cost = Long(node, "CostTalentPointCount");
            return talentItemId > 0 && cost >= 0 && InventoryCount(player, talentItemId) >= cost;
        }

        private OfficialConditionContext NpcTalentConditionContext(OfflinePlayerProgress player,
            int npcClassId, int nodeId)
        {
            var context = new OfficialConditionContext
            {
                Level = player.Level,
                CurrentTalentTreeNpcClassId = npcClassId,
                CurrentTalentTreeNodeId = nodeId
            };
            OfflineCharacterPropertyProgress properties = player.CharacterProperties
                ?? new OfflineCharacterPropertyProgress();
            foreach (OfflineNpcFriendship friendship in properties.NpcFriendships ?? new List<OfflineNpcFriendship>())
                if (friendship != null && friendship.NpcClassId > 0)
                    context.NpcFriendshipLevels[friendship.NpcClassId] = friendship.Level;
            foreach (OfflineNpcTalentTree tree in properties.NpcTalentTrees ?? new List<OfflineNpcTalentTree>())
                if (tree != null && tree.NpcClassId > 0)
                    context.NpcTalentTreeActiveNodeIds[tree.NpcClassId]
                        = new HashSet<int>(tree.ActiveNodeIds ?? new List<int>());
            if (!context.NpcTalentTreeActiveNodeIds.ContainsKey(npcClassId))
                context.NpcTalentTreeActiveNodeIds[npcClassId] = new HashSet<int>();
            return context;
        }

        private Dictionary<string, object> FindNpcTalentNode(int npcClassId, int nodeId)
        {
            return catalog.Rows("npc_talent_tree_map").FirstOrDefault(row =>
                Integer(row, "NpcClassId") == npcClassId && Integer(row, "NodeId") == nodeId);
        }

        private Dictionary<string, object> FindNpcTalentItem(int npcClassId)
        {
            return catalog.Rows("npc_talent_item").FirstOrDefault(row =>
                Integer(row, "NpcClassId") == npcClassId);
        }

        private static OfflineNpcTalentTree GetOrCreateNpcTalentTree(OfflinePlayerProgress player, int npcClassId)
        {
            if (player.CharacterProperties == null)
                player.CharacterProperties = new OfflineCharacterPropertyProgress();
            OfflineCharacterPropertyProgress.Normalize(player.CharacterProperties);
            OfflineNpcTalentTree tree = player.CharacterProperties.NpcTalentTrees
                .FirstOrDefault(value => value.NpcClassId == npcClassId);
            if (tree != null) return tree;
            tree = new OfflineNpcTalentTree { NpcClassId = npcClassId };
            player.CharacterProperties.NpcTalentTrees.Add(tree);
            return tree;
        }

        private long LockedNpcTalentPointCost(int npcClassId, IReadOnlyCollection<int> activeNodeIds)
        {
            var active = new HashSet<int>(activeNodeIds ?? Array.Empty<int>());
            long total = 0;
            foreach (Dictionary<string, object> node in catalog.Rows("npc_talent_tree_map"))
            {
                if (Integer(node, "NpcClassId") != npcClassId || active.Contains(Integer(node, "NodeId")))
                    continue;
                total = checked(total + Math.Max(0L, Long(node, "CostTalentPointCount")));
            }
            return total;
        }

        private static long InventoryCount(OfflinePlayerProgress player, int itemId)
        {
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            return stack == null ? 0 : stack.Count;
        }

        public IReadOnlyDictionary<int, long> BaseBattleProperties()
        {
            return catalog.CalcCharacterBaseBattleProperties(Player.Level, Player.SubRank);
        }

        private long CalculatePetCombatRating(OfflinePetInstance pet)
        {
            int level = Math.Max(1, Player.Level);
            int subRank = Math.Max(1, Player.SubRank);
            var context = new CharacterPropScoreContext
            {
                Level = level,
                SubRank = subRank,
                Props = propertyCalculator.Props,
                BasicRef = CharacterPropBasicRef.Build(level, subRank, propertyCalculator.Props,
                    propertyCalculator.LevelProps)
            };
            return petPropertySource.CalculatePetCombatRating(Player, pet, context);
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
                RecordEquipCabinetScore(player);
            }, eventId);
        }

        public int CurrentEquipCabinetScore()
        {
            return CalculateEquipCabinetScore(Player);
        }

        public int LegionMapSkillLevel(int skillClassId)
        {
            return (Player.CharacterProperties?.LegionMapSkills ?? new List<OfflineLegionMapSkill>())
                .FirstOrDefault(value => value != null && value.SkillClassId == skillClassId)?.Level ?? 0;
        }

        public int LegionLibraryLevel
        {
            get
            {
                int level = Player.CharacterProperties?.LegionLibraryLevel ?? 1;
                return level > 0 ? level : 1;
            }
        }

        public IReadOnlyDictionary<int, long> LegionMapSkillUpgradeCost(int skillClassId)
        {
            int nextLevel = LegionMapSkillLevel(skillClassId) + 1;
            Dictionary<string, object> row = catalog.Rows("legion_map_skill_study_level").FirstOrDefault(value =>
                Integer(value, "SkillClassId") == skillClassId && Integer(value, "Level") == nextLevel);
            if (row == null || !row.TryGetValue("LevelUpCost", out object rawCost)
                || !(rawCost is Dictionary<string, object> cost)) return new Dictionary<int, long>();
            var result = new Dictionary<int, long>();
            foreach (KeyValuePair<string, object> entry in cost)
            {
                int itemId = Convert.ToInt32(entry.Key, CultureInfo.InvariantCulture);
                long count = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
                if (itemId <= 0 || count <= 0) return new Dictionary<int, long>();
                result[itemId] = count;
            }
            return result;
        }

        public ProgressionResult UpgradeLegionMapSkill(int skillClassId, string eventId)
        {
            if (skillClassId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid guild skill and event ID are required.");
            Dictionary<string, object> definition = catalog.Find("legion_map_skill", skillClassId, "SkillClassId");
            if (definition == null) return Invalid("The official guild skill is unavailable.");
            int libraryLevel = LegionLibraryLevel;
            int currentLevel = LegionMapSkillLevel(skillClassId);
            IReadOnlyList<Dictionary<string, object>> studyLevels = catalog.Rows("legion_map_skill_study_level");
            int maxLevel = studyLevels.Where(value => Integer(value, "SkillClassId") == skillClassId)
                .Select(value => Integer(value, "Level")).DefaultIfEmpty(0).Max();
            if (currentLevel >= maxLevel) return Invalid("The guild skill has reached its official maximum level.");
            if (libraryLevel < Integer(definition, "UnlockLevel"))
                return Invalid("The library level does not unlock this guild skill.");

            if (definition.TryGetValue("TermSkillArr", out object rawRequirements)
                && rawRequirements is IEnumerable<object> requirements)
            {
                foreach (object rawRequirement in requirements)
                {
                    int prerequisiteId = Convert.ToInt32(rawRequirement, CultureInfo.InvariantCulture);
                    if (LegionMapSkillLevel(prerequisiteId) <= 0)
                        return Invalid("An official prerequisite guild skill has not been learned.");
                }
            }

            IReadOnlyDictionary<int, long> costs = LegionMapSkillUpgradeCost(skillClassId);
            if (costs.Count == 0) return Invalid("The next official guild skill level has no valid cost row.");
            Dictionary<int, long> required = costs.ToDictionary(value => value.Key, value => value.Value);
            return session.ApplyCharacterMutation(player =>
            {
                OfflineCharacterPropertyProgress properties = player.CharacterProperties
                    ?? (player.CharacterProperties = new OfflineCharacterPropertyProgress());
                OfflineCharacterPropertyProgress.Normalize(properties);
                if (properties.LegionLibraryLevel < Integer(definition, "UnlockLevel"))
                    throw new InvalidOperationException("The library level does not unlock this guild skill.");
                OfflineLegionMapSkill skill = properties.LegionMapSkills.FirstOrDefault(value =>
                    value.SkillClassId == skillClassId);
                int savedLevel = skill?.Level ?? 0;
                if (savedLevel != currentLevel)
                    throw new InvalidOperationException("The guild skill changed before the upgrade was applied.");
                if (libraryLevel < Integer(definition, "UnlockLevel"))
                    throw new InvalidOperationException("The library level does not unlock this guild skill.");
                foreach (KeyValuePair<int, long> cost in required)
                {
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == cost.Key);
                    if (stack == null || stack.Count < cost.Value)
                        throw new InvalidOperationException("The required guild skill materials are insufficient.");
                }
                if (skill == null)
                    properties.LegionMapSkills.Add(skill = new OfflineLegionMapSkill
                    {
                        SkillClassId = skillClassId
                    });
                skill.Level = currentLevel + 1;
                foreach (KeyValuePair<int, long> cost in required)
                {
                    OfflineItemStack stack = player.Inventory.First(value => value.ItemId == cost.Key);
                    stack.Count -= cost.Value;
                }
            }, eventId);
        }

        private bool TryNpcMeetRequirements(Dictionary<string, object> npc, OfflinePlayerProgress player,
            out Dictionary<int, long> requiredItems, bool requireConditionMet = true)
        {
            requiredItems = new Dictionary<int, long>();
            Dictionary<string, object> condition = Map(npc, "CanMeetCondition");
            if (condition == null || player == null) return false;
            var context = new OfficialConditionContext
            {
                Level = player.Level,
                ItemCounts = (player.Inventory ?? new List<OfflineItemStack>())
                    .Where(value => value != null && value.ItemId > 0)
                    .GroupBy(value => value.ItemId)
                    .ToDictionary(group => group.Key, group => group.Sum(value => value.Count))
            };
            if (requireConditionMet && !catalog.EvaluateCondition(condition, context)) return false;
            return CollectNpcMeetItems(condition, context, requiredItems, new HashSet<int>());
        }

        private bool CollectNpcMeetItems(Dictionary<string, object> condition, OfficialConditionContext context,
            Dictionary<int, long> requiredItems, HashSet<int> visiting)
        {
            if (condition == null || !condition.TryGetValue("_type", out object rawType)) return false;
            string type = Convert.ToString(rawType, CultureInfo.InvariantCulture);
            switch (type)
            {
                case "NoneCondition":
                case "LevelCondition":
                    return true;
                case "HasItemCondition":
                    int itemId = Integer(condition, "ClassId");
                    int count = Integer(condition, "Count");
                    if (itemId <= 0 || count <= 0) return false;
                    requiredItems.TryGetValue(itemId, out long existing);
                    requiredItems[itemId] = checked(existing + count);
                    return true;
                case "CompositeAndCondition":
                    if (!condition.TryGetValue("Ids", out object rawAndIds)
                        || !(rawAndIds is IEnumerable<object> andIds)) return false;
                    foreach (object rawId in andIds)
                    {
                        int id = Convert.ToInt32(rawId, CultureInfo.InvariantCulture);
                        if (!TryCollectNpcMeetConditionId(id, context, requiredItems, visiting)) return false;
                    }
                    return true;
                case "CompositeOrCondition":
                    if (!condition.TryGetValue("Ids", out object rawOrIds)
                        || !(rawOrIds is IEnumerable<object> orIds)) return false;
                    Dictionary<string, object> firstBranch = null;
                    foreach (object rawId in orIds)
                    {
                        int id = Convert.ToInt32(rawId, CultureInfo.InvariantCulture);
                        Dictionary<string, object> row = catalog.Find("conditions", id, "ClassId");
                        Dictionary<string, object> child = Map(row, "Condition");
                        if (firstBranch == null) firstBranch = child;
                        if (!catalog.EvaluateCondition(child, context)) continue;
                        var branchItems = new Dictionary<int, long>();
                        if (!TryCollectNpcMeetConditionId(id, context, branchItems, visiting)) continue;
                        foreach (KeyValuePair<int, long> item in branchItems)
                        {
                            requiredItems.TryGetValue(item.Key, out long prior);
                            requiredItems[item.Key] = checked(prior + item.Value);
                        }
                        return true;
                    }
                    return firstBranch != null
                        && CollectNpcMeetItems(firstBranch, context, requiredItems, visiting);
                default:
                    return catalog.EvaluateCondition(condition, context);
            }
        }

        private bool TryCollectNpcMeetConditionId(int conditionId, OfficialConditionContext context,
            Dictionary<int, long> requiredItems, HashSet<int> visiting)
        {
            if (conditionId <= 0 || !visiting.Add(conditionId)) return false;
            try
            {
                Dictionary<string, object> row = catalog.Find("conditions", conditionId, "ClassId");
                return CollectNpcMeetItems(Map(row, "Condition"), context, requiredItems, visiting);
            }
            finally
            {
                visiting.Remove(conditionId);
            }
        }

        public IReadOnlyList<int> NpcGiftCandidates(int npcClassId)
        {
            Dictionary<string, object> npc = catalog.Find("npc", npcClassId, "Id");
            if (npc == null) return Array.Empty<int>();
            Dictionary<string, object> preferences = Map(npc, "GiftPreferenceDict");
            bool starGod = Integer(npc, "NpcRoleType") == 2;
            return catalog.Rows("npc_gift").Select(row => Integer(row, "ClassId"))
                .Where(itemId => itemId > 0 && InventoryCount(Player, itemId) > 0
                    && CanSendNpcGift(itemId)
                    && (!starGod || preferences != null
                        && preferences.ContainsKey(itemId.ToString(CultureInfo.InvariantCulture))))
                .Distinct().OrderBy(itemId => itemId).ToArray();
        }

        public ProgressionResult GiveNpcGifts(int npcClassId, IReadOnlyDictionary<int, long> gifts,
            string eventId)
        {
            if (npcClassId <= 0 || gifts == null || gifts.Count == 0
                || string.IsNullOrWhiteSpace(eventId))
                return Invalid("Select NPC gifts and provide a valid event ID.");
            Dictionary<string, object> npc = catalog.Find("npc", npcClassId, "Id");
            if (npc == null || !TryNpcGiftFriendshipExperience(npc, gifts, out int addedExperience))
                return Invalid("The selected NPC gifts are unavailable or cannot be sent.");

            OfflineNpcFriendship current = (Player.CharacterProperties?.NpcFriendships
                ?? new List<OfflineNpcFriendship>()).FirstOrDefault(value => value != null
                    && value.NpcClassId == npcClassId);
            if (current == null) return Invalid("This NPC has not been met.");
            int friendshipLimit = session.MaximumFriendshipLevel(npcClassId, Player.SubRank);
            if (friendshipLimit <= 0 || current.Level >= friendshipLimit)
                return Invalid("This NPC friendship level is limited by the current sub-rank.");

            long currentExperience = current.FriendshipExp;
            if (currentExperience == 0 && current.Level > 0
                && session.TryGetFriendshipExperience(npcClassId, current.Level, out long levelExperience))
                currentExperience = levelExperience;
            long nextExperience;
            try
            {
                nextExperience = checked(currentExperience + addedExperience);
            }
            catch (OverflowException)
            {
                return Invalid("NPC friendship experience is exhausted.");
            }
            if (session.FriendshipLevel(npcClassId, nextExperience, Player.SubRank) >= friendshipLimit)
                return Invalid("The selected gifts would reach the current sub-rank friendship limit.");

            Dictionary<int, long> selected = gifts.ToDictionary(value => value.Key, value => value.Value);
            return session.ApplyCharacterMutation(player =>
            {
                OfflineCharacterPropertyProgress properties = player.CharacterProperties
                    ?? (player.CharacterProperties = new OfflineCharacterPropertyProgress());
                OfflineCharacterPropertyProgress.Normalize(properties);
                OfflineNpcFriendship friendship = properties.NpcFriendships.FirstOrDefault(value =>
                    value.NpcClassId == npcClassId);
                if (friendship == null)
                    throw new InvalidOperationException("This NPC has not been met.");
                foreach (KeyValuePair<int, long> gift in selected)
                {
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == gift.Key);
                    if (stack == null || stack.Count < gift.Value)
                        throw new InvalidOperationException("NPC gifts changed before the transaction was saved.");
                    stack.Count -= gift.Value;
                }
                long experience = friendship.FriendshipExp;
                if (experience == 0 && friendship.Level > 0
                    && session.TryGetFriendshipExperience(npcClassId, friendship.Level, out long threshold))
                    experience = threshold;
                friendship.FriendshipExp = checked(experience + addedExperience);
                friendship.Level = session.FriendshipLevel(npcClassId, friendship.FriendshipExp, player.SubRank);
            }, eventId);
        }

        public int NpcGiftFriendshipExperience(int npcClassId, IReadOnlyDictionary<int, long> gifts)
        {
            Dictionary<string, object> npc = catalog.Find("npc", npcClassId, "Id");
            return npc != null && TryNpcGiftFriendshipExperience(npc, gifts, out int experience)
                ? experience : 0;
        }

        private bool TryNpcGiftFriendshipExperience(Dictionary<string, object> npc,
            IReadOnlyDictionary<int, long> gifts, out int experience)
        {
            experience = 0;
            if (npc == null || gifts == null || gifts.Count == 0) return false;
            Dictionary<string, object> gameSettings = catalog.Rows("game_settings").FirstOrDefault();
            double defaultFactor = gameSettings != null
                && gameSettings.TryGetValue("NpcGiftDefaultPreferenceFactor", out object rawFactor)
                ? Convert.ToDouble(rawFactor, CultureInfo.InvariantCulture) : 1.0;
            Dictionary<string, object> preferences = Map(npc, "GiftPreferenceDict");
            bool starGod = Integer(npc, "NpcRoleType") == 2;
            long total = 0;
            foreach (KeyValuePair<int, long> gift in gifts)
            {
                if (gift.Key <= 0 || gift.Value <= 0 || catalog.FindItem(gift.Key) == null) return false;
                OfflineItemStack stack = Player.Inventory.FirstOrDefault(value => value.ItemId == gift.Key);
                Dictionary<string, object> giftInfo = catalog.Find("npc_gift", gift.Key);
                if (stack == null || stack.Count < gift.Value || giftInfo == null
                    || !CanSendNpcGift(gift.Key)
                    || starGod && (preferences == null
                        || !preferences.ContainsKey(gift.Key.ToString(CultureInfo.InvariantCulture)))) return false;
                long friendship = Long(giftInfo, "Friendship");
                if (friendship < 0) return false;
                double factor = preferences != null
                    && preferences.TryGetValue(gift.Key.ToString(CultureInfo.InvariantCulture), out object rawPreference)
                    ? Convert.ToDouble(rawPreference, CultureInfo.InvariantCulture) : defaultFactor;
                if (friendship > 0 && gift.Value > long.MaxValue / friendship) return false;
                double rounded = Math.Round((double)(friendship * gift.Value) * factor);
                if (double.IsNaN(rounded) || double.IsInfinity(rounded)
                    || rounded < 0 || rounded > int.MaxValue) return false;
                total = checked(total + (long)rounded);
                if (total > int.MaxValue) return false;
            }
            experience = (int)total;
            return experience > 0;
        }

        private bool CanSendNpcGift(int itemId)
        {
            Dictionary<string, object> unlock = catalog.Find("npc_unlock_item", itemId);
            return unlock == null || Boolean(unlock, "CanSendGift");
        }

        private void RecordEquipCabinetScore(OfflinePlayerProgress player)
        {
            OfflineCharacterPropertyProgress properties = player.CharacterProperties
                ?? (player.CharacterProperties = new OfflineCharacterPropertyProgress());
            OfflineCharacterPropertyProgress.Normalize(properties);
            int seasonId = CurrentSeasonId(player);
            if (seasonId <= 0) return;
            properties.SeasonId = seasonId;
            OfflineEquipCabinetSeasonScore current = properties.EquipCabinetScores.FirstOrDefault(value =>
                value.SeasonId == seasonId);
            if (current == null)
                properties.EquipCabinetScores.Add(current = new OfflineEquipCabinetSeasonScore { SeasonId = seasonId });
            current.Score = CalculateEquipCabinetScore(player);
        }

        private int CurrentSeasonId(OfflinePlayerProgress player)
        {
            IReadOnlyList<Dictionary<string, object>> seasons = catalog.Rows("astrological_season_config");
            Dictionary<string, object> season = seasons.FirstOrDefault(row => Integer(row, "Rank") == player.Rank);
            return season == null ? 0 : Integer(season, "Season");
        }

        private static bool SeasonConditionMet(Dictionary<string, object> season, int level)
        {
            Dictionary<string, object> condition = Map(season, "Condition");
            if (condition == null || !condition.TryGetValue("_type", out object rawType)
                || !string.Equals(Convert.ToString(rawType, CultureInfo.InvariantCulture),
                    "LevelCondition", StringComparison.Ordinal)) return false;
            int target = Integer(condition, "Level");
            string operation = condition.TryGetValue("Op", out object rawOperation)
                ? Convert.ToString(rawOperation, CultureInfo.InvariantCulture) : string.Empty;
            switch (operation)
            {
                case ">=": return level >= target;
                case ">": return level > target;
                case "<=": return level <= target;
                case "<": return level < target;
                case "==": return level == target;
                default: return false;
            }
        }

        private int CalculateEquipCabinetScore(OfflinePlayerProgress player)
        {
            long score = 0;
            if (player?.Equipment?.Equipped == null) return 0;
            List<OfflineEquipmentInstance> instances = player.Equipment.Instances
                ?? new List<OfflineEquipmentInstance>();
            foreach (OfflineEquippedSlot equipped in player.Equipment.Equipped)
            {
                if (equipped == null) continue;
                OfflineEquipmentInstance instance = instances.FirstOrDefault(value =>
                    value.InstanceId == equipped.InstanceId);
                if (instance == null) continue;
                Dictionary<string, object> item = catalog.FindItem(instance.ItemId);
                if (item == null) continue;
                score = checked(score + EquipmentCabinetScore(item, instance.ItemLevel + instance.BlessLevel));
                foreach (OfflineEquipmentGem gem in instance.Gems ?? new List<OfflineEquipmentGem>())
                {
                    if (gem == null || gem.ItemId <= 0) continue;
                    Dictionary<string, object> gemItem = catalog.FindItem(gem.ItemId);
                    if (gemItem != null)
                        score = checked(score + GemCabinetScore(gemItem));
                }
            }
            return checked((int)score);
        }

        private int EquipmentCabinetScore(Dictionary<string, object> item, int level)
        {
            List<Dictionary<string, object>> rows = catalog.Rows("equip_cabinet_score")
                .Where(row => Integer(row, "RankLimit") == Integer(item, "LimitRank"))
                .OrderBy(row => Integer(row, "Level")).ToList();
            Dictionary<string, object> row = rows.FirstOrDefault(value => Integer(value, "Level") >= level)
                ?? rows.LastOrDefault();
            Dictionary<string, object> scores = Map(row, "Score");
            return scores != null && scores.TryGetValue(Integer(item, "Quality")
                .ToString(CultureInfo.InvariantCulture), out object rawScore)
                ? Convert.ToInt32(rawScore, CultureInfo.InvariantCulture) : 0;
        }

        private int GemCabinetScore(Dictionary<string, object> item)
        {
            Dictionary<string, object> row = catalog.Rows("equip_cabinet_gem_score").FirstOrDefault(value =>
                Integer(value, "RankLimit") == Integer(item, "LimitRank")
                    && Integer(value, "Rank") == Integer(item, "Rank"));
            return row == null ? 0 : Integer(row, "Score");
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

        public IReadOnlyList<OfflineEquipmentInstance> InheritCandidates(long targetInstanceId)
        {
            if (EquipmentCatalog == null) return Array.Empty<OfflineEquipmentInstance>();
            OfflineEquipmentInstance target = Player.Equipment.Instances
                .FirstOrDefault(value => value.InstanceId == targetInstanceId);
            if (target == null) return Array.Empty<OfflineEquipmentInstance>();
            int type = catalog.ItemType(target.ItemId);
            return Player.Equipment.Instances.Where(value => value.InstanceId != targetInstanceId
                    && catalog.ItemType(value.ItemId) == type)
                .OrderByDescending(value => value.ItemLevel).ThenBy(value => value.InstanceId).ToArray();
        }

        /// <summary>Same-season full inheritance: the source instance is consumed and the target receives its terms and gems.</summary>
        public ProgressionResult InheritEquipment(long sourceInstanceId, long targetInstanceId, string eventId)
        {
            if (EquipmentCatalog == null || sourceInstanceId <= 0 || targetInstanceId <= 0
                || sourceInstanceId == targetInstanceId || string.IsNullOrWhiteSpace(eventId))
                return Invalid("Valid source and target equipment are required.");
            return session.ApplyCharacterMutation(player =>
            {
                EquipmentInheritanceResult result = OfflineEquipmentOperations.Inherit(player, EquipmentCatalog,
                    0, sourceInstanceId, targetInstanceId, null,
                    instance => catalog.ItemType(instance.ItemId) == catalog.ItemType(
                        player.Equipment.Instances.First(value => value.InstanceId == targetInstanceId).ItemId),
                    (itemId, terms) => null);
                if (result == null) throw new InvalidOperationException("The equipment inheritance is invalid.");
            }, eventId);
        }

        public IReadOnlyDictionary<int, long> PurifyCost(int lockedCount)
        {
            if (EquipmentCatalog == null) return new Dictionary<int, long>();
            Dictionary<string, object> row = EquipmentCatalog.Rows("equip_purify_cost")
                .FirstOrDefault(value => Convert.ToInt32(value["Count"], CultureInfo.InvariantCulture) == lockedCount);
            if (row == null || !(row.TryGetValue("Cost", out object raw)
                && raw is Dictionary<string, object> cost)) return new Dictionary<int, long>();
            var result = new Dictionary<int, long>();
            foreach (KeyValuePair<string, object> entry in cost)
                result[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                    = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
            return result;
        }

        /// <summary>Rerolls every unlocked term, keeping the indices in <paramref name="keepIndices"/>.</summary>
        public ProgressionResult PurifyEquipment(long instanceId, IReadOnlyCollection<int> keepIndices, string eventId)
        {
            if (EquipmentCatalog == null || instanceId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid equipment instance and event ID are required.");
            OfflineEquipmentInstance instance = Player.Equipment.Instances
                .FirstOrDefault(value => value.InstanceId == instanceId);
            if (instance == null || instance.Terms == null || instance.Terms.Count == 0)
                return Invalid("The equipment has no terms to purify.");
            var keep = new HashSet<int>(keepIndices ?? Array.Empty<int>());
            if (keep.Any(index => index < 0 || index >= instance.Terms.Count)
                || keep.Count >= instance.Terms.Count)
                return Invalid("Select fewer terms to keep before purifying.");
            IReadOnlyDictionary<int, long> cost = PurifyCost(keep.Count);
            if (cost.Count == 0) return Invalid("The official purify cost is unavailable.");
            foreach (KeyValuePair<int, long> entry in cost)
                if (Player.Inventory.Where(value => value.ItemId == entry.Key).Sum(value => value.Count) < entry.Value)
                    return Invalid("Not enough purify materials.");
            return session.ApplyCharacterMutation(player =>
            {
                foreach (KeyValuePair<int, long> entry in cost)
                {
                    OfflineItemStack stack = player.Inventory.First(value => value.ItemId == entry.Key);
                    stack.Count -= entry.Value;
                }
                OfflineEquipmentInstance target = player.Equipment.Instances
                    .First(value => value.InstanceId == instanceId);
                var random = new System.Random(BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0));
                for (int index = 0; index < target.Terms.Count; index++)
                {
                    if (keep.Contains(index)) continue;
                    OfflineEquipmentTerm generated = OfficialEquipmentGenerator.GenerateReplacement(
                        EquipmentCatalog, target.ItemId, target.Terms, () => (float)random.NextDouble());
                    if (generated != null) target.Terms[index] = generated;
                }
            }, eventId);
        }

        public IReadOnlyList<int> MergeableEquipmentPieces()
        {
            var result = new List<int>();
            foreach (Dictionary<string, object> row in catalog.Rows("equip_piece_merge"))
            {
                int pieceId = Convert.ToInt32(row["Id"], CultureInfo.InvariantCulture);
                int count = Convert.ToInt32(row["PieceMergeCount"], CultureInfo.InvariantCulture);
                if (count <= 0) continue;
                if (Player.Inventory.Where(value => value.ItemId == pieceId).Sum(value => value.Count) >= count)
                    result.Add(pieceId);
            }
            return result.OrderBy(value => value).ToArray();
        }

        public IReadOnlyList<int> EquipmentMergeCandidates(int pieceId)
        {
            Dictionary<string, object> row = catalog.Rows("equip_piece_merge")
                .FirstOrDefault(value => Convert.ToInt32(value["Id"], CultureInfo.InvariantCulture) == pieceId);
            if (row == null || !(row.TryGetValue("EquipIdList", out object raw)
                && raw is List<object> list)) return Array.Empty<int>();
            return list.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture))
                .Where(value => EquipmentCatalog?.Find("item", value) != null).ToArray();
        }

        public ProgressionResult MergeEquipmentPiece(int pieceId, int equipId)
        {
            if (EquipmentCatalog == null) return Invalid("Official equipment rules are unavailable.");
            Dictionary<string, object> row = catalog.Rows("equip_piece_merge")
                .FirstOrDefault(value => Convert.ToInt32(value["Id"], CultureInfo.InvariantCulture) == pieceId);
            if (row == null) return Invalid("The selected equipment piece cannot be merged.");
            int count = Convert.ToInt32(row["PieceMergeCount"], CultureInfo.InvariantCulture);
            if (Player.Inventory.Where(value => value.ItemId == pieceId).Sum(value => value.Count) < count)
                return Invalid("Not enough equipment pieces.");
            if (!EquipmentMergeCandidates(pieceId).Contains(equipId))
                return Invalid("The selected equipment is not part of this merge table.");
            return session.ApplyCharacterMutation(player =>
            {
                long instanceId = player.Equipment.NextInstanceId;
                OfflineEquipmentInstance generated = OfficialEquipmentGenerator.Generate(EquipmentCatalog, equipId,
                    instanceId, new EquipmentRollContext
                    {
                        DropLevel = Math.Max(1, player.Level),
                        PlayerLevel = player.Level,
                        SubRank = player.SubRank,
                        SourceOperate = 0
                    }, () => (float)new System.Random(BitConverter.ToInt32(
                        Guid.NewGuid().ToByteArray(), 0)).NextDouble());
                if (generated == null) throw new InvalidOperationException("The equipment could not be generated.");
                OfflineItemStack piece = player.Inventory.First(value => value.ItemId == pieceId);
                piece.Count -= count;
                player.Equipment.Instances.Add(generated);
                player.Equipment.NextInstanceId = instanceId + 1;
            }, "equip-piece-merge-" + pieceId.ToString(CultureInfo.InvariantCulture) + "-"
                + Guid.NewGuid().ToString("N"));
        }

        public IReadOnlyDictionary<int, long> InheritanceCost(int selectedTerms)
        {
            if (EquipmentCatalog == null) return new Dictionary<int, long>();
            Dictionary<string, object> row = EquipmentCatalog.Rows("equip_inherit_cost")
                .FirstOrDefault(value => Convert.ToInt32(value["Count"], CultureInfo.InvariantCulture) == selectedTerms);
            if (row == null || !(row.TryGetValue("Cost", out object raw)
                && raw is Dictionary<string, object> cost)) return new Dictionary<int, long>();
            var result = new Dictionary<int, long>();
            foreach (KeyValuePair<string, object> entry in cost)
                result[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                    = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
            return result;
        }

        /// <summary>Cross-season inheritance: only the selected source terms are copied, the rest are rerolled.</summary>
        public ProgressionResult InheritEquipmentSelected(long sourceInstanceId, long targetInstanceId,
            IReadOnlyCollection<int> selectedTerms, string eventId)
        {
            if (EquipmentCatalog == null || sourceInstanceId <= 0 || targetInstanceId <= 0
                || sourceInstanceId == targetInstanceId || string.IsNullOrWhiteSpace(eventId))
                return Invalid("Valid source and target equipment are required.");
            var selected = new HashSet<int>(selectedTerms ?? Array.Empty<int>());
            if (selected.Count == 0) return Invalid("Select at least one term to inherit.");
            return session.ApplyCharacterMutation(player =>
            {
                var random = new System.Random(BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0));
                EquipmentInheritanceResult result = OfflineEquipmentOperations.Inherit(player, EquipmentCatalog,
                    1, sourceInstanceId, targetInstanceId, selected,
                    instance => catalog.ItemType(instance.ItemId) == catalog.ItemType(
                        player.Equipment.Instances.First(value => value.InstanceId == targetInstanceId).ItemId),
                    (itemId, terms) => OfficialEquipmentGenerator.GenerateReplacement(
                        EquipmentCatalog, itemId, terms, () => (float)random.NextDouble()));
                if (result == null) throw new InvalidOperationException("The equipment inheritance is invalid.");
            }, eventId);
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
                RecordEquipCabinetScore(player);
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

        public bool IsWardrobeItemNew(int itemId)
        {
            return itemId > 0 && !Player.Wardrobe.SeenItems.Contains(itemId);
        }

        public ProgressionResult MarkWardrobeItemSeen(int itemId)
        {
            if (itemId <= 0 || Player.Wardrobe.SeenItems.Contains(itemId))
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            return session.ApplyCharacterMutation(player => player.Wardrobe.SeenItems.Add(itemId),
                "wardrobe-seen-" + itemId.ToString(CultureInfo.InvariantCulture) + "-"
                + Guid.NewGuid().ToString("N"));
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

        public int EquippedSkillSkinId(int skillId)
        {
            OfflineSkillAppearance equipped = Player.Wardrobe.SkillSkins
                .FirstOrDefault(value => value != null && value.SkillId == skillId);
            return equipped == null ? 0 : equipped.SkillSkinId;
        }

        public ProgressionResult WearSkillSkin(int itemId, string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A skill skin event ID is required.");
            Dictionary<string, object> skin = catalog.Find("skill_skin", itemId);
            if (skin == null || ItemType(itemId) != 425 || Integer(skin, "SkinType") == 0)
                return Invalid("The selected item is not an official skill appearance.");
            int skillId = Integer(skin, "SkillId");
            if (!IsOwned(Player, itemId) || OfflineSkillProgression.OwnedRank(Player.Skills, skillId) <= 0)
                return Invalid("The skill appearance or its original skill is not owned.");

            return session.ApplyCharacterMutation(player =>
            {
                OfflineSkillAppearance equipped = player.Wardrobe.SkillSkins
                    .FirstOrDefault(value => value.SkillId == skillId);
                if (equipped == null)
                {
                    equipped = new OfflineSkillAppearance { SkillId = skillId };
                    player.Wardrobe.SkillSkins.Add(equipped);
                }
                equipped.SkillSkinId = itemId;
            }, eventId);
        }

        public ProgressionResult TakeOffSkillSkin(int skillId, string eventId)
        {
            if (skillId <= 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid skill and skill skin event ID are required.");
            return session.ApplyCharacterMutation(player =>
            {
                OfflineSkillAppearance equipped = player.Wardrobe.SkillSkins
                    .FirstOrDefault(value => value.SkillId == skillId);
                if (equipped == null)
                    throw new InvalidOperationException("The selected skill has no equipped appearance.");
                player.Wardrobe.SkillSkins.Remove(equipped);
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

        /// <summary>Resolves an owned wardrobe item for visual preview without mutating the profile.</summary>
        public WardrobeResolution PreviewAppearance(int itemId, long nowUnixSeconds)
        {
            WardrobeAppearanceState state = ReadAppearance(Player.Wardrobe);
            if (itemId > 0)
            {
                var owned = Player.Inventory.ToDictionary(item => item.ItemId, item => item.Count);
                OfflineWardrobeRules.TryWear(state, itemId, owned, catalog.Appearances,
                    catalog.Skins, catalog.AllowedSkins, nowUnixSeconds);
            }
            return OfflineWardrobeRules.Resolve(state, catalog.Appearances, catalog.Skins, catalog.AllowedSkins);
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
            int seasonId = CurrentSeasonId(player);
            player.SeasonRank = seasonId;
            if (player.CharacterProperties == null)
                player.CharacterProperties = new OfflineCharacterPropertyProgress();
            OfflineCharacterPropertyProgress.Normalize(player.CharacterProperties);
            player.CharacterProperties.SeasonId = seasonId;
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

        private static Dictionary<string, object> Map(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                && value is Dictionary<string, object> result ? result : null;
        }

        private static int Integer(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static long Long(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : 0L;
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
