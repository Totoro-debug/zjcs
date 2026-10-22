using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    public sealed class EquipmentUpgradeQuote
    {
        public bool CanUpgrade;
        public bool IsBlessing;
        public int Position;
        public int CurrentLevel;
        public int NextLevel;
        public string LimitKey;
        public int LimitValue;
        public IReadOnlyDictionary<int, long> Cost;
    }

    public sealed class EquipmentInheritanceResult
    {
        public long ResultInstanceId;
        public int ResultItemId;
        public IReadOnlyDictionary<int, long> ReturnedGems;
    }

    public static class OfflineEquipmentOperations
    {
        private static readonly int[] Positions = { 10, 11, 12, 13, 14 };

        public static void Initialize(OfflineEquipmentProfile profile)
        {
            Require(profile);
            foreach (int position in Positions)
                if (profile.SlotProgress.All(value => value.Position != position))
                    profile.SlotProgress.Add(new OfflineEquipmentSlotProgress { Position = position });
        }

        public static int PositionForItem(OfficialEquipmentCatalog catalog, int itemId)
        {
            Dictionary<string, object> item = catalog.Find("item", itemId);
            if (item == null) return 0;
            int itemType = Int(item, "ItemType");
            Dictionary<string, object> type = catalog.Rows("equip_type")
                .FirstOrDefault(value => Int(value, "ItemType") == itemType);
            return type == null ? 0 : Int(type, "EquipPos");
        }

        public static bool CanWear(OfficialEquipmentCatalog catalog, int itemId, int professionId)
        {
            Dictionary<string, object> item = catalog.Find("item", itemId);
            if (item == null) return false;
            int itemType = Int(item, "ItemType");
            Dictionary<string, object> type = catalog.Rows("equip_type")
                .FirstOrDefault(value => Int(value, "ItemType") == itemType);
            return type != null && Ints(type, "Professions").Contains(professionId);
        }

        public static bool Wear(OfflineEquipmentProfile profile, OfficialEquipmentCatalog catalog,
            long instanceId, int professionId)
        {
            Require(profile);
            OfflineEquipmentInstance instance = Find(profile, instanceId);
            int position = instance == null ? 0 : PositionForItem(catalog, instance.ItemId);
            if (instance == null || !Positions.Contains(position) || !CanWear(catalog, instance.ItemId, professionId)) return false;
            OfflineEquippedSlot slot = profile.Equipped.FirstOrDefault(value => value.Position == position);
            if (slot == null) profile.Equipped.Add(new OfflineEquippedSlot { Position = position, InstanceId = instanceId });
            else slot.InstanceId = instanceId;
            return true;
        }

        public static bool TakeOff(OfflineEquipmentProfile profile, int position)
        {
            Require(profile);
            return profile.Equipped.RemoveAll(value => value.Position == position) > 0;
        }

        public static bool SetLocked(OfflineEquipmentProfile profile, long instanceId, bool locked)
        {
            Require(profile);
            OfflineEquipmentInstance item = Find(profile, instanceId);
            if (item == null || item.SystemLocked && !locked || item.Locked == locked) return false;
            item.Locked = locked;
            return true;
        }

        public static IReadOnlyDictionary<int, long> Decompose(OfflinePlayerProgress player,
            OfficialEquipmentCatalog catalog, long instanceId)
        {
            OfflineEquipmentInstance instance = Find(player?.Equipment, instanceId);
            if (instance == null || instance.Locked || instance.SystemLocked
                || player.Equipment.Equipped.Any(value => value.InstanceId == instanceId)) return null;
            Dictionary<string, object> item = catalog.Find("item", instance.ItemId);
            if (item == null || !item.TryGetValue("RecycleReward", out object rewardValue)
                || !(rewardValue is Dictionary<string, object> rewardRows)) return null;
            int groupCount = Math.Max(1, Int(item, "RecycleGroupCount"));
            if (groupCount != 1) return null;
            Dictionary<int, long> rewards = ToCosts(rewardRows);
            foreach (OfflineEquipmentGem gem in instance.Gems.Where(value => value.ItemId > 0))
                rewards[gem.ItemId] = rewards.TryGetValue(gem.ItemId, out long count) ? checked(count + 1) : 1;

            player.Equipment.Instances.Remove(instance);
            foreach (KeyValuePair<int, long> reward in rewards) ChangeStack(player.Inventory, reward.Key, reward.Value);
            return rewards;
        }

        public static EquipmentUpgradeQuote QuoteUpgrade(OfflinePlayerProgress player, OfficialEquipmentCatalog catalog,
            int position, int seasonId, int playerBlessLevel, int playerTotalLevel, int professionRank)
        {
            if (player == null || player.Equipment == null || !Positions.Contains(position))
                return InvalidQuote(position);
            OfflineEquipmentProfile profile = player.Equipment;
            OfflineEquipmentSlotProgress slot = profile.SlotProgress.FirstOrDefault(value => value.Position == position);
            if (slot == null) return InvalidQuote(position);
            int bless = BlessLevel(slot, seasonId);
            int current = slot.BaseLevel + bless;
            Dictionary<string, object> next = catalog.Rows("system_level_limit")
                .FirstOrDefault(row => player.Level <= Int(row, "PlayerLevel")
                    && Int(row, "EquipLevel") + Int(row, "EquipBlessLevel") > current);
            if (next == null) return new EquipmentUpgradeQuote { Position = position, CurrentLevel = current, NextLevel = current, LimitKey = "PlayerUseItemContent.Max" };

            bool blessing = Int(next, "PlayerBlessLevel") > 0;
            int target = current + 1;
            Dictionary<int, long> cost = UpgradeCost(catalog, position, target, blessing,
                player.SubRank, Math.Max(0, current - Int(next, "EquipLevel") + 1));
            var quote = new EquipmentUpgradeQuote
            {
                Position = position,
                CurrentLevel = current,
                NextLevel = target,
                IsBlessing = blessing,
                Cost = cost
            };
            if (profile.Equipped.All(value => value.Position != position))
                quote.LimitKey = "ui_equip_not_equipped";
            else if (current >= MaxBaseLevel(catalog, player.SubRank)
                && Int(next, "SubRank") > player.SubRank)
            {
                quote.LimitKey = "function_limit_subrank";
                quote.LimitValue = Int(next, "SubRank");
            }
            else if (current >= MaxBaseLevel(catalog, player.SubRank)
                && Int(next, "PlayerLevel") + Int(next, "PlayerBlessLevel") > playerTotalLevel)
            {
                quote.LimitKey = blessing ? "function_limit_bless_level" : "function_limit_level";
                quote.LimitValue = Int(next, "PlayerLevel") + Int(next, "PlayerBlessLevel");
            }
            else if (blessing && !CanBless(player, catalog, position, seasonId, playerBlessLevel, playerTotalLevel, professionRank,
                out bool otherSlotTooLow, out int gap))
            {
                quote.LimitKey = otherSlotTooLow ? "EquipPromotePanel.MaxSubRankLevel" : "EquipPromotePanel.LimitGapLevel";
                quote.LimitValue = gap;
            }
            else if (cost.Count == 0) quote.LimitKey = "PlayerUseItemContent.Max";
            else quote.CanUpgrade = true;
            return quote;
        }

        public static bool Upgrade(OfflinePlayerProgress player, OfficialEquipmentCatalog catalog,
            int position, int seasonId, int playerBlessLevel, int playerTotalLevel, int professionRank)
        {
            EquipmentUpgradeQuote quote = QuoteUpgrade(player, catalog, position, seasonId, playerBlessLevel,
                playerTotalLevel, professionRank);
            if (!quote.CanUpgrade || !HasItems(player.Inventory, quote.Cost)) return false;
            foreach (KeyValuePair<int, long> item in quote.Cost) ChangeStack(player.Inventory, item.Key, -item.Value);
            OfflineEquipmentSlotProgress slot = player.Equipment.SlotProgress.First(value => value.Position == position);
            if (quote.IsBlessing) SetBlessLevel(slot, seasonId, BlessLevel(slot, seasonId) + 1);
            else slot.BaseLevel = quote.NextLevel;
            return true;
        }

        public static IReadOnlyList<int> GemCandidates(OfflinePlayerProgress player, OfficialEquipmentCatalog catalog,
            long instanceId)
        {
            OfflineEquipmentInstance item = Find(player?.Equipment, instanceId);
            if (item == null) return Array.Empty<int>();
            int position = PositionForItem(catalog, item.ItemId);
            return catalog.Rows("gem").Where(row => Ints(row, "InlayEquipPos").Contains(position)
                    && Count(player.Inventory, Int(row, "ClassId")) > 0)
                .OrderByDescending(row => Int(catalog.Find("item", Int(row, "ClassId")), "Rank"))
                .ThenBy(row => Int(row, "ClassId"))
                .Select(row => Int(row, "ClassId")).ToArray();
        }

        public static bool InlayGem(OfflinePlayerProgress player, OfficialEquipmentCatalog catalog,
            long instanceId, int gemId, Func<Dictionary<string, object>, bool> condition)
        {
            OfflineEquipmentInstance equip = Find(player?.Equipment, instanceId);
            Dictionary<string, object> gem = catalog.Find("gem", gemId);
            if (equip == null || gem == null || condition == null) return false;
            int position = PositionForItem(catalog, equip.ItemId);
            if (!Ints(gem, "InlayEquipPos").Contains(position) || Count(player.Inventory, gemId) < 1) return false;
            int slot = FirstUnlockedGemSlot(equip, catalog, position, condition);
            if (slot < 0) return false;
            ChangeStack(player.Inventory, gemId, -1);
            equip.Gems.RemoveAll(value => value.Slot == slot);
            equip.Gems.Add(new OfflineEquipmentGem { Slot = slot, ItemId = gemId });
            return true;
        }

        public static bool TakeOffGem(OfflinePlayerProgress player, long instanceId, int gemSlot)
        {
            OfflineEquipmentInstance equip = Find(player?.Equipment, instanceId);
            OfflineEquipmentGem gem = equip?.Gems.FirstOrDefault(value => value.Slot == gemSlot && value.ItemId > 0);
            if (gem == null) return false;
            equip.Gems.Remove(gem);
            ChangeStack(player.Inventory, gem.ItemId, 1);
            return true;
        }

        public static IReadOnlyList<int> PieceCandidates(OfflinePlayerProgress player, OfficialEquipmentCatalog catalog,
            int pieceId, int professionId)
        {
            Dictionary<string, object> piece = catalog.Find("equip_piece_merge", pieceId);
            if (piece == null) return Array.Empty<int>();
            Dictionary<string, object> settings = catalog.GameSettings as Dictionary<string, object>;
            List<int> priority = settings != null && settings.TryGetValue("EquipPieceMergeItemSortPriority", out object value)
                && value is List<object> list ? list.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToList()
                : new List<int>();
            return Ints(piece, "EquipIdList").Where(id => CanWear(catalog, id, professionId))
                .OrderBy(id => priority.IndexOf(PositionForItem(catalog, id)) < 0 ? int.MaxValue : priority.IndexOf(PositionForItem(catalog, id)))
                .ToArray();
        }

        public static bool MergePiece(OfflinePlayerProgress player, OfficialEquipmentCatalog catalog,
            int pieceId, int equipId, int professionId,
            Func<int, long, OfflineEquipmentInstance> generateInstance)
        {
            Dictionary<string, object> piece = catalog.Find("equip_piece_merge", pieceId);
            int count = piece == null ? 0 : Int(piece, "PieceMergeCount");
            if (player?.Equipment == null || count <= 0 || generateInstance == null
                || !PieceCandidates(player, catalog, pieceId, professionId).Contains(equipId)
                || Count(player.Inventory, pieceId) < count) return false;
            long instanceId = player.Equipment.NextInstanceId;
            OfflineEquipmentInstance generated = generateInstance(equipId, instanceId);
            if (generated == null || generated.ItemId != equipId || generated.InstanceId != instanceId) return false;
            ChangeStack(player.Inventory, pieceId, -count);
            player.Equipment.Instances.Add(generated);
            player.Equipment.NextInstanceId++;
            return true;
        }

        public static EquipmentInheritanceResult Inherit(OfflinePlayerProgress player, OfficialEquipmentCatalog catalog,
            int mode, long sourceInstanceId, long targetInstanceId, IReadOnlyCollection<int> selectedTermIndices,
            Func<OfflineEquipmentInstance, bool> isEligible,
            Func<int, IReadOnlyList<OfflineEquipmentTerm>, OfflineEquipmentTerm> generateReplacement)
        {
            if (player?.Equipment == null || catalog == null || isEligible == null || generateReplacement == null
                || (mode != 0 && mode != 1) || sourceInstanceId == targetInstanceId) return null;
            OfflineEquipmentProfile profile = player.Equipment;
            OfflineEquipmentInstance source = Find(profile, sourceInstanceId);
            OfflineEquipmentInstance target = Find(profile, targetInstanceId);
            if (source == null || target == null || source.Locked || target.Locked
                || source.SystemLocked || target.SystemLocked
                || !isEligible(source) || !isEligible(target)) return null;
            Dictionary<string, object> sourceItem = catalog.Find("item", source.ItemId);
            Dictionary<string, object> targetItem = catalog.Find("item", target.ItemId);
            if (sourceItem == null || targetItem == null || Int(sourceItem, "ItemType") != Int(targetItem, "ItemType")) return null;

            var selected = new HashSet<int>();
            if (selectedTermIndices != null)
                foreach (int index in selectedTermIndices)
                    if (index < 0 || index >= source.Terms.Count || !selected.Add(index)) return null;
            if ((mode == 0 && selected.Count != 0) || (mode == 1 && selected.Count == 0)) return null;
            Dictionary<int, long> cost = mode == 0
                ? new Dictionary<int, long>() : InheritCost(catalog, selected.Count);
            if (mode == 1 && (cost.Count == 0 || !HasItems(player.Inventory, cost))) return null;

            var resultingTerms = new List<OfflineEquipmentTerm>(source.Terms.Count);
            for (int index = 0; index < source.Terms.Count; index++)
                resultingTerms.Add(mode == 0 || selected.Contains(index) ? CloneTerm(source.Terms[index]) : null);
            for (int index = 0; index < resultingTerms.Count; index++)
                if (resultingTerms[index] == null)
                {
                    OfflineEquipmentTerm generated = generateReplacement(target.ItemId, resultingTerms);
                    if (generated == null) return null;
                    resultingTerms[index] = CloneTerm(generated);
                }

            bool targetHasGems = target.Gems.Any(value => value.ItemId > 0);
            var returned = new Dictionary<int, long>();
            if (targetHasGems)
                foreach (OfflineEquipmentGem gem in source.Gems.Where(value => value.ItemId > 0))
                    returned[gem.ItemId] = returned.TryGetValue(gem.ItemId, out long count) ? count + 1 : 1;
            bool transferWear = profile.Equipped.Any(value => value.InstanceId == sourceInstanceId
                || value.InstanceId == targetInstanceId);

            foreach (KeyValuePair<int, long> item in cost) ChangeStack(player.Inventory, item.Key, -item.Value);
            foreach (KeyValuePair<int, long> item in returned) ChangeStack(player.Inventory, item.Key, item.Value);
            target.Terms = resultingTerms;
            if (!targetHasGems) target.Gems = source.Gems.Select(CloneGem).ToList();
            profile.Equipped.RemoveAll(value => value.InstanceId == sourceInstanceId || value.InstanceId == targetInstanceId);
            profile.Instances.Remove(source);
            target.Locked = true;
            if (transferWear)
            {
                int position = PositionForItem(catalog, target.ItemId);
                if (Positions.Contains(position)) profile.Equipped.Add(new OfflineEquippedSlot
                {
                    Position = position,
                    InstanceId = target.InstanceId
                });
            }
            return new EquipmentInheritanceResult
            {
                ResultInstanceId = target.InstanceId,
                ResultItemId = target.ItemId,
                ReturnedGems = returned
            };
        }

        private static bool CanBless(OfflinePlayerProgress player, OfficialEquipmentCatalog catalog, int position,
            int seasonId, int playerBlessLevel, int playerTotalLevel, int professionRank,
            out bool otherSlotTooLow, out int gap)
        {
            otherSlotTooLow = false;
            gap = 0;
            Dictionary<string, object> next = catalog.Rows("system_level_limit").FirstOrDefault(row =>
                player.Level <= Int(row, "PlayerLevel") && Int(row, "EquipLevel") + Int(row, "EquipBlessLevel")
                    > SlotLevel(player.Equipment, position, seasonId));
            if (next == null || Int(next, "SubRank") > player.SubRank
                || Int(next, "PlayerLevel") + Int(next, "PlayerBlessLevel") > playerTotalLevel) return false;
            int maximumBase = MaxBaseLevel(catalog, player.SubRank);
            int minimum = int.MaxValue;
            foreach (int other in Positions)
            {
                if (other == 14 && professionRank <= 1) continue;
                OfflineEquipmentSlotProgress slot = player.Equipment.SlotProgress.FirstOrDefault(value => value.Position == other);
                if (slot == null || slot.BaseLevel < maximumBase)
                {
                    otherSlotTooLow = true;
                    return false;
                }
                minimum = Math.Min(minimum, SlotLevel(player.Equipment, other, seasonId));
            }
            int nextLevel = SlotLevel(player.Equipment, position, seasonId) + 1;
            List<Dictionary<string, object>> gapRows = catalog.Rows("system_level_gap_limit")
                .Where(value => Int(value, "DevelopType") == 0 && Int(value, "SeasonId") == seasonId).ToList();
            int gapLimit = 0;
            for (int index = 0; index < gapRows.Count; index++)
            {
                Dictionary<string, object> row = gapRows[index];
                if (nextLevel <= Int(row, "Level")) break;
                if (index == gapRows.Count - 1 || nextLevel <= Int(gapRows[index + 1], "Level"))
                {
                    if (Bool(row, "IsOpen") && minimum < Int(row, "LimitLevel"))
                        gapLimit = Int(row, "LimitLevel");
                    break;
                }
            }
            if (gapLimit > 0)
            {
                gap = gapLimit;
                return false;
            }
            return GetBlessLevel(player.Equipment, position, seasonId) + 1 <= MaxBless(catalog, player, playerBlessLevel);
        }

        private static Dictionary<int, long> UpgradeCost(OfficialEquipmentCatalog catalog, int position,
            int target, bool blessing, int subRank, int nextBless)
        {
            string table = blessing ? "equip_upgrade_bless" : "equip_upgrade_material";
            Dictionary<string, object> row = catalog.Rows(table).FirstOrDefault(value =>
                (blessing ? Int(value, "Rank") == Rank(subRank) && Int(value, "BlessLevel") == nextBless
                    : Int(value, "UpgradeLevel") == target) && Ints(value, "EquipPoses").Contains(position));
            return ToCosts(row == null ? null : row.TryGetValue("Items", out object items) ? items : null);
        }

        private static Dictionary<int, long> InheritCost(OfficialEquipmentCatalog catalog, int count)
        {
            Dictionary<string, object> row = catalog.Rows("equip_inherit_cost")
                .FirstOrDefault(value => Int(value, "Count") == count);
            return ToCosts(row == null ? null : row.TryGetValue("Cost", out object cost) ? cost : null);
        }

        private static int MaxBaseLevel(OfficialEquipmentCatalog catalog, int subRank)
        {
            return catalog.Rows("system_level_limit").Where(row => Rank(Int(row, "SubRank")) == Rank(subRank)
                && Int(row, "EquipBlessLevel") == 0).Select(row => Int(row, "EquipLevel")).DefaultIfEmpty(0).Max();
        }

        private static int MaxBless(OfficialEquipmentCatalog catalog, OfflinePlayerProgress player, int blessLevel)
        {
            Dictionary<string, object> row = catalog.Rows("system_level_limit").FirstOrDefault(value =>
                Int(value, "SubRank") == player.SubRank && Int(value, "PlayerLevel") == player.Level
                    && Int(value, "PlayerBlessLevel") == blessLevel);
            return row == null ? 0 : Int(row, "EquipBlessLevel");
        }

        private static int FirstUnlockedGemSlot(OfflineEquipmentInstance item, OfficialEquipmentCatalog catalog,
            int position, Func<Dictionary<string, object>, bool> condition)
        {
            if (!catalog.Rows("function_limit").Any(row => Int(row, "Type") == 20 && Int(row, "Key0") == 0
                && Int(row, "Key1") == 0 && condition(ConditionValue(row)))) return -1;
            foreach (Dictionary<string, object> setting in catalog.Rows("equip_inlay_site")
                .Where(row => Int(row, "EquipPos") == position).OrderBy(row => Int(row, "Index")))
            {
                int index = Int(setting, "Index");
                if (!item.Gems.Any(value => value.Slot == index && value.ItemId > 0)
                    && condition(ConditionById(catalog, Int(setting, "CondtionId")))) return index;
            }
            return -1;
        }

        private static int SlotLevel(OfflineEquipmentProfile profile, int position, int seasonId)
        {
            OfflineEquipmentSlotProgress slot = profile.SlotProgress.FirstOrDefault(value => value.Position == position);
            return slot == null ? 0 : slot.BaseLevel + BlessLevel(slot, seasonId);
        }

        private static int BlessLevel(OfflineEquipmentSlotProgress slot, int seasonId)
        {
            return GetBlessLevelFromSlot(slot, seasonId);
        }

        private static int GetBlessLevel(OfflineEquipmentProfile profile, int position, int seasonId)
        {
            OfflineEquipmentSlotProgress slot = profile.SlotProgress.FirstOrDefault(value => value.Position == position);
            return slot == null ? 0 : BlessLevel(slot, seasonId);
        }

        private static int GetBlessLevelFromSlot(OfflineEquipmentSlotProgress slot, int seasonId)
        {
            OfflineEquipmentBlessProgress value = slot.BlessLevels.FirstOrDefault(entry => entry.SeasonId == seasonId);
            return value == null ? 0 : value.Level;
        }

        private static OfflineEquipmentTerm CloneTerm(OfflineEquipmentTerm value)
        {
            return value == null ? null : new OfflineEquipmentTerm
            {
                Type = value.Type,
                PropertyId = value.PropertyId,
                PropertyType = value.PropertyType,
                Factor = value.Factor,
                Group = value.Group,
                SkillQuality = value.SkillQuality,
                Indices = value.Indices == null ? new List<int>() : new List<int>(value.Indices)
            };
        }

        private static OfflineEquipmentGem CloneGem(OfflineEquipmentGem value)
        {
            return new OfflineEquipmentGem { Slot = value.Slot, ItemId = value.ItemId };
        }

        private static void SetBlessLevel(OfflineEquipmentSlotProgress slot, int seasonId, int level)
        {
            OfflineEquipmentBlessProgress value = slot.BlessLevels.FirstOrDefault(entry => entry.SeasonId == seasonId);
            if (value == null)
            {
                value = new OfflineEquipmentBlessProgress { SeasonId = seasonId };
                slot.BlessLevels.Add(value);
            }
            value.Level = level;
        }

        private static int Rank(int subRank) => subRank < 2 ? 1 : 2 + (subRank - 2) / 3;

        private static OfflineEquipmentInstance Find(OfflineEquipmentProfile profile, long instanceId) =>
            profile?.Instances.FirstOrDefault(value => value.InstanceId == instanceId);

        private static bool HasItems(List<OfflineItemStack> inventory, IReadOnlyDictionary<int, long> costs) =>
            costs != null && costs.All(value => value.Value > 0 && Count(inventory, value.Key) >= value.Value);

        private static long Count(List<OfflineItemStack> inventory, int id) =>
            inventory.FirstOrDefault(value => value.ItemId == id)?.Count ?? 0;

        private static void ChangeStack(List<OfflineItemStack> inventory, int id, long delta)
        {
            OfflineItemStack stack = inventory.FirstOrDefault(value => value.ItemId == id);
            if (stack == null)
            {
                if (delta < 0) throw new InvalidOperationException("Inventory count is insufficient.");
                stack = new OfflineItemStack { ItemId = id };
                inventory.Add(stack);
            }
            long next = checked(stack.Count + delta);
            if (next < 0) throw new InvalidOperationException("Inventory count is insufficient.");
            stack.Count = next;
            if (next == 0) inventory.Remove(stack);
        }

        private static Dictionary<int, long> ToCosts(object value)
        {
            var result = new Dictionary<int, long>();
            if (!(value is Dictionary<string, object> costs)) return result;
            foreach (KeyValuePair<string, object> entry in costs)
            {
                int id = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                long count = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
                if (id <= 0 || count <= 0) return new Dictionary<int, long>();
                result.Add(id, count);
            }
            return result;
        }

        private static Dictionary<string, object> ConditionValue(Dictionary<string, object> row)
        {
            return row != null && row.TryGetValue("Condition", out object value)
                ? value as Dictionary<string, object> : null;
        }

        private static Dictionary<string, object> ConditionById(OfficialEquipmentCatalog catalog, int id)
        {
            return ConditionValue(catalog.Find("conditions", id));
        }

        private static IEnumerable<int> Ints(Dictionary<string, object> value, string key) =>
            value != null && value.TryGetValue(key, out object result) && result is List<object> list
                ? list.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)) : Enumerable.Empty<int>();

        private static int Int(Dictionary<string, object> value, string key) =>
            value != null && value.TryGetValue(key, out object result)
                ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : 0;

        private static bool Bool(Dictionary<string, object> value, string key) =>
            value != null && value.TryGetValue(key, out object result) && Convert.ToBoolean(result, CultureInfo.InvariantCulture);

        private static EquipmentUpgradeQuote InvalidQuote(int position) => new EquipmentUpgradeQuote
        {
            Position = position,
            Cost = new Dictionary<int, long>(),
            LimitKey = "InvalidState"
        };

        private static void Require(OfflineEquipmentProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Instances == null || profile.Equipped == null || profile.SlotProgress == null)
                throw new InvalidOperationException("Equipment profile collections are not initialized.");
        }
    }
}
