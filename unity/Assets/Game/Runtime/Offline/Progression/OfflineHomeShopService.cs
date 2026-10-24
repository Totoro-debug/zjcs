using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfficialHomeShopRuntimeData
    {
        public string version;
        public int shop_id;
        public string currency_policy;
        public OfficialHomeShopRuleHashes source_rule_tables;
        public List<OfficialHomeShopGoods> fixed_goods = new List<OfficialHomeShopGoods>();
        public bool grocery_unique;
        public List<int> grocery_group_ids = new List<int>();
        public List<int> grocery_group_draw_counts = new List<int>();
        public int grocery_random_count;
        public List<OfficialHomeShopGroup> grocery_groups = new List<OfficialHomeShopGroup>();
        public bool grocery_user_refresh;
        public List<int> grocery_refresh_currency_ids = new List<int>();
        public List<int> grocery_refresh_prices = new List<int>();
        public int grocery_refresh_price_factor;
        public int grocery_refresh_counts_limit;
        public int treasury_shop_id;
        public bool treasury_unique;
        public List<int> treasury_group_ids = new List<int>();
        public List<int> treasury_group_draw_counts = new List<int>();
        public int treasury_random_count;
        public List<OfficialHomeShopGroup> treasury_groups = new List<OfficialHomeShopGroup>();
        public int shop_score_currency_id;
        public float shop_score_multiplier;
        public int registered_days_reset_hour;
        public int registered_days_reset_minute;
        public int registered_days_reset_second;
        public int weekly_start_day_of_week;
        public OfficialHomeShopAchievementRules shop_score_achievements;
    }

    [Serializable]
    public sealed class OfficialHomeShopRuleHashes
    {
        public string shop;
        public string shop_goods;
        public string item;
        public string shop_goods_group;
        public string achievement;
        public string game_settings;
        public string shop_goods_price_off;
    }

    [Serializable]
    public sealed class OfficialHomeShopGoods
    {
        public int goods_id;
        public int group_id;
        public int weight;
        public string condition_type;
        public int condition_day;
        public string condition_op;
        public int condition_from_sub_rank;
        public int condition_to_sub_rank;
        public List<OfficialHomeShopPriceOff> price_off_options = new List<OfficialHomeShopPriceOff>();
        public int item_id;
        public int item_count;
        public int currency_id;
        public long original_price;
        public int inventory;
        public int total_buy_count_limit;
        public bool support_buy_multi_count;
        public bool show_name;
        public int award_dialog_type;
        public int refresh_cycle_type;
        public int map_unlock_id;
        public string item_name;
        public int item_quality;
        public string icon_file;
        public string currency_icon_file;
    }

    [Serializable]
    public sealed class OfficialHomeShopPriceOff
    {
        public int sub_rank;
        public List<float> values = new List<float>();
    }

    [Serializable]
    public sealed class OfficialHomeShopGroup
    {
        public int group_id;
        public int draw_count;
        public int order;
        public string condition_type;
        public int condition_day;
        public string condition_op;
        public bool refresh_when_condition_changed;
        public int refresh_filter;
        public List<int> complex_group_ids = new List<int>();
        public List<int> complex_group_weights = new List<int>();
        public List<OfficialHomeShopGoods> goods = new List<OfficialHomeShopGoods>();
    }

    [Serializable]
    public sealed class OfficialHomeShopAchievementRules
    {
        public int achievement_type;
        public int achievement_group;
        public int cycle_count;
        public List<OfficialHomeShopMilestone> independent = new List<OfficialHomeShopMilestone>();
        public OfficialHomeShopMilestone cycle_anchor;
        public List<OfficialHomeShopMilestone> cycle_templates = new List<OfficialHomeShopMilestone>();
    }

    [Serializable]
    public sealed class OfficialHomeShopMilestone
    {
        public int raw_id;
        public int target_score;
        public int cycle_increment;
        public List<OfficialHomeShopReward> rewards = new List<OfficialHomeShopReward>();
    }

    [Serializable]
    public sealed class OfficialHomeShopReward
    {
        public int item_id;
        public int count;
        public string item_name;
        public int item_quality;
        public string icon_file;
    }

    [Serializable]
    public sealed class OfflineHomeShopProgress
    {
        public bool TreasuryInitialized;
        public bool GroceryInitialized;
        public int GroceryRefreshedCount;
        public long RandomState;
        public long ShopScore;
        public List<OfflineHomeShopStock> GroceryStock = new List<OfflineHomeShopStock>();
        public List<OfflineHomeShopStock> TreasuryStock = new List<OfflineHomeShopStock>();
        public List<int> ClaimedGiftAchievementIds = new List<int>();

        public static void Normalize(OfflineHomeShopProgress progress)
        {
            if (progress.TreasuryStock == null) progress.TreasuryStock = new List<OfflineHomeShopStock>();
            if (progress.GroceryStock == null) progress.GroceryStock = new List<OfflineHomeShopStock>();
            if (progress.ClaimedGiftAchievementIds == null) progress.ClaimedGiftAchievementIds = new List<int>();
        }

        public static bool IsValid(OfflineHomeShopProgress progress)
        {
            if (progress == null || progress.ShopScore < 0 || progress.GroceryRefreshedCount < 0
                || progress.GroceryRefreshedCount > 10 || progress.TreasuryStock == null
                || progress.TreasuryStock.Count > 64 || progress.GroceryStock == null
                || progress.GroceryStock.Count > 16 || progress.ClaimedGiftAchievementIds == null
                || progress.ClaimedGiftAchievementIds.Count > 400040) return false;
            var slots = new HashSet<int>();
            foreach (OfflineHomeShopStock stock in progress.TreasuryStock)
                if (stock == null || stock.SlotIndex < 0 || stock.GoodsId <= 0 || stock.RemainingInventory < -1
                    || stock.BoughtCount < 0 || stock.PriceOff < 0f || stock.PriceOff > 1f
                    || !slots.Add(stock.SlotIndex)) return false;
            slots.Clear();
            foreach (OfflineHomeShopStock stock in progress.GroceryStock)
                if (stock == null || stock.SlotIndex < 0 || stock.GoodsId <= 0 || stock.RemainingInventory < -1
                    || stock.BoughtCount < 0 || stock.PriceOff < 0f || stock.PriceOff > 1f
                    || !slots.Add(stock.SlotIndex)) return false;
            var claimed = new HashSet<int>();
            foreach (int id in progress.ClaimedGiftAchievementIds)
                if (id <= 0 || (id & 0xFFF) != 4 || !claimed.Add(id)) return false;
            return (!progress.TreasuryInitialized || progress.TreasuryStock.Count <= 12)
                && (!progress.GroceryInitialized || progress.GroceryStock.Count <= 9);
        }
    }

    [Serializable]
    public sealed class OfflineHomeShopStock
    {
        public int SlotIndex;
        public int GoodsId;
        public int RemainingInventory;
        public long BoughtCount;
        public float PriceOff = 1f;
        public long RefreshCycleKey;
    }

    public sealed class OfflineHomeShopShelfItem
    {
        public int SlotIndex;
        public OfficialHomeShopGoods Goods;
        public int RemainingInventory;
        public long BoughtCount;
        public float PriceOff = 1f;
        public bool IsRandom;
    }

    public sealed class OfflineHomeShopGiftEntry
    {
        public int AchievementId;
        public int Index;
        public int TargetScore;
        public long CurrentScore;
        public bool CanClaim;
        public OfficialHomeShopMilestone Milestone;
    }

    public sealed class OfficialHomeShopCatalog
    {
        private OfficialHomeShopCatalog(OfficialHomeShopRuntimeData data)
        {
            Data = data;
        }

        public OfficialHomeShopRuntimeData Data { get; }

        public static OfficialHomeShopCatalog Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A HomeShop rules path is required.", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("The official offline HomeShop rules are missing.", path);
            OfficialHomeShopRuntimeData data = JsonUtility.FromJson<OfficialHomeShopRuntimeData>(File.ReadAllText(path));
            if (data == null || data.version != "854_156383" || data.shop_id != 1
                || data.source_rule_tables == null
                || data.source_rule_tables.shop != "784ca23dac58f9e9f1eb9db07e9850662dcde60a5e8e565b7dccc8047861f852"
                || data.source_rule_tables.shop_goods != "2376414c79ae91f8e923110a435cf46036ca2f5c7ec0958c1a55e61633cbccca"
                || data.source_rule_tables.shop_goods_price_off != "4df46569b425c270767709d5ab938426424ecc21306cc979002d47dc662e9654"
                || data.source_rule_tables.item != "a317a104405d25b63d1ec8c6f63397001f8cc6f2329a04034c2a49be4a18934d"
                || data.source_rule_tables.shop_goods_group != "52bfd4289a791df97e80726d09d669d5c18e1b6bfab50d435bec3bcd1fcb0b8f"
                || data.source_rule_tables.achievement != "319e4e07413ae5ccbebf724eb7e2454b841b072e48fe2ef71da8512cbd4978a8"
                || data.source_rule_tables.game_settings != "8832aa02061ada1c2dad9cc190fbe6fe54a25be1e30596513b9f8c1a1ef22b27"
                || data.fixed_goods == null || data.fixed_goods.Count != 8
                || data.grocery_group_ids == null || data.grocery_group_draw_counts == null
                || data.grocery_groups == null || data.grocery_group_ids.Count != data.grocery_group_draw_counts.Count
                || data.grocery_group_draw_counts.Sum() != 9 || data.grocery_random_count != 9
                || data.grocery_groups.Count < data.grocery_group_ids.Count
                || !data.grocery_user_refresh || data.grocery_refresh_counts_limit != 10
                || data.grocery_refresh_price_factor != 0
                || data.grocery_refresh_currency_ids == null || data.grocery_refresh_prices == null
                || data.grocery_refresh_currency_ids.Count != 10 || data.grocery_refresh_prices.Count != 10
                || data.grocery_refresh_currency_ids.Any(currency => currency <= 0)
                || data.grocery_refresh_prices.Any(price => price <= 0)
                || data.treasury_shop_id != 2 || !data.treasury_unique || data.treasury_random_count != 12
                || data.treasury_group_ids == null || data.treasury_group_draw_counts == null
                || data.treasury_groups == null || data.treasury_group_ids.Count != data.treasury_groups.Count
                || data.treasury_group_draw_counts.Count != data.treasury_groups.Count
                || data.treasury_group_draw_counts.Sum() != data.treasury_random_count
                || data.shop_score_currency_id != 2 || data.shop_score_multiplier <= 0f
                || data.registered_days_reset_hour < 0 || data.registered_days_reset_hour > 23
                || data.registered_days_reset_minute < 0 || data.registered_days_reset_minute > 59
                || data.registered_days_reset_second < 0 || data.registered_days_reset_second > 59
                || data.weekly_start_day_of_week < 0 || data.weekly_start_day_of_week > 6
                || data.shop_score_achievements == null || data.shop_score_achievements.achievement_type != 12
                || data.shop_score_achievements.achievement_group != 4
                || data.shop_score_achievements.cycle_count != 10000
                || data.shop_score_achievements.independent == null
                || data.shop_score_achievements.independent.Count != 39
                || data.shop_score_achievements.cycle_anchor == null
                || data.shop_score_achievements.cycle_anchor.target_score != 200000
                || data.shop_score_achievements.cycle_templates == null
                || data.shop_score_achievements.cycle_templates.Count != 40)
                throw new InvalidDataException("The official offline HomeShop rules are invalid.");

            var goodsIds = new HashSet<int>();
            foreach (OfficialHomeShopGoods goods in data.fixed_goods)
            {
                if (!ValidGoods(goods) || goods.inventory != -1
                    || goods.total_buy_count_limit != -1 || goods.map_unlock_id < 0
                    || string.IsNullOrWhiteSpace(goods.item_name) || string.IsNullOrWhiteSpace(goods.icon_file)
                    || string.IsNullOrWhiteSpace(goods.currency_icon_file)
                    || !ValidPriceOff(goods) || !goodsIds.Add(goods.goods_id))
                    throw new InvalidDataException("The official fixed HomeShop goods contain invalid or duplicate rows.");
            }
            var groceryGroupMap = new Dictionary<int, OfficialHomeShopGroup>();
            var groceryGroupIds = new HashSet<int>();
            foreach (OfficialHomeShopGroup group in data.grocery_groups)
            {
                if (group == null || group.group_id <= 0 || !groceryGroupIds.Add(group.group_id)
                    || group.condition_type != "NoneCondition" || group.goods == null
                    || group.complex_group_ids == null || group.complex_group_weights == null
                    || group.complex_group_ids.Count != group.complex_group_weights.Count
                    || group.goods.Count == 0 && group.complex_group_ids.Count == 0)
                    throw new InvalidDataException("The official grocery group is invalid.");
                groceryGroupMap.Add(group.group_id, group);
            }
            foreach (OfficialHomeShopGroup group in groceryGroupMap.Values)
            {
                foreach (int childId in group.complex_group_ids)
                    if (!groceryGroupMap.ContainsKey(childId)) throw new InvalidDataException("A nested grocery group is missing.");
                foreach (int childWeight in group.complex_group_weights)
                    if (childWeight <= 0) throw new InvalidDataException("A nested grocery weight is invalid.");
                foreach (OfficialHomeShopGoods goods in group.goods)
                    if (!ValidGoods(goods) || goods.group_id != group.group_id || goods.weight <= 0
                        || goods.inventory < -1 || goods.total_buy_count_limit < -1
                        || !ValidCondition(goods) || !ValidPriceOff(goods) || string.IsNullOrWhiteSpace(goods.icon_file)
                        || string.IsNullOrWhiteSpace(goods.currency_icon_file))
                        throw new InvalidDataException("The official grocery goods contain invalid rows.");
            }
            for (int i = 0; i < data.grocery_group_ids.Count; i++)
                if (data.grocery_group_draw_counts[i] <= 0 || !groceryGroupMap.ContainsKey(data.grocery_group_ids[i]))
                    throw new InvalidDataException("The official grocery group draw counts are invalid.");
            for (int i = 0; i < data.treasury_groups.Count; i++)
            {
                OfficialHomeShopGroup group = data.treasury_groups[i];
                if (group == null || group.group_id != data.treasury_group_ids[i]
                    || group.draw_count != data.treasury_group_draw_counts[i] || group.draw_count <= 0
                    || group.condition_type != "NoneCondition" || group.goods == null || group.goods.Count == 0)
                    throw new InvalidDataException("The official HomeShop treasure group is invalid.");
                foreach (OfficialHomeShopGoods goods in group.goods)
                    if (!ValidGoods(goods) || goods.group_id != group.group_id || goods.weight <= 0
                        || goods.inventory < -1 || goods.total_buy_count_limit != -1
                        || !ValidCondition(goods) || !ValidPriceOff(goods) || string.IsNullOrWhiteSpace(goods.icon_file)
                        || string.IsNullOrWhiteSpace(goods.currency_icon_file))
                        throw new InvalidDataException("The official HomeShop treasure goods contain invalid rows.");
            }
            foreach (OfficialHomeShopMilestone milestone in data.shop_score_achievements.independent)
                if (!ValidMilestone(milestone) || milestone.target_score <= 0) throw new InvalidDataException("The official HomeShop gift milestones are invalid.");
            if (!ValidMilestone(data.shop_score_achievements.cycle_anchor))
                throw new InvalidDataException("The official HomeShop gift cycle anchor is invalid.");
            foreach (OfficialHomeShopMilestone milestone in data.shop_score_achievements.cycle_templates)
                if (!ValidMilestone(milestone) || milestone.cycle_increment != 5000)
                    throw new InvalidDataException("The official HomeShop gift cycle templates are invalid.");
            return new OfficialHomeShopCatalog(data);
        }

        private static bool ValidGoods(OfficialHomeShopGoods goods)
        {
            return goods != null && goods.goods_id > 0 && goods.item_id > 0 && goods.item_count > 0
                && goods.currency_id > 0 && goods.original_price >= 0 && goods.total_buy_count_limit == -1
                && goods.map_unlock_id >= 0 && !string.IsNullOrWhiteSpace(goods.item_name);
        }

        private static bool ValidCondition(OfficialHomeShopGoods goods)
        {
            if (goods.condition_type == "NoneCondition") return goods.condition_day == 0;
            if (goods.condition_type == "RegisteredDaysCondition")
                return goods.condition_day >= 0
                    && (goods.condition_op == ">=" || goods.condition_op == ">" || goods.condition_op == "=="
                        || goods.condition_op == "<=" || goods.condition_op == "<");
            return goods.condition_type == "SubRankBetweenCondition"
                && goods.condition_from_sub_rank >= 0 && goods.condition_to_sub_rank >= goods.condition_from_sub_rank;
        }

        private static bool ValidPriceOff(OfficialHomeShopGoods goods)
        {
            return goods.price_off_options != null && goods.price_off_options.All(option => option != null
                && option.sub_rank >= 0 && option.values != null && option.values.Count > 0
                && option.values.All(value => value >= 0f && value <= 1f));
        }

        private static bool ValidMilestone(OfficialHomeShopMilestone milestone)
        {
            return milestone != null && milestone.raw_id > 0 && milestone.rewards != null
                && milestone.rewards.Count > 0 && milestone.rewards.All(reward => reward != null
                    && reward.item_id > 0 && reward.count > 0 && !string.IsNullOrWhiteSpace(reward.icon_file));
        }
    }

    public enum HomeShopPurchaseStatus
    {
        Applied,
        Duplicate,
        Locked,
        InsufficientCurrency,
        OutOfStock,
        RefreshLimitReached,
        Unsupported,
        InvalidRequest,
        SaveRejected
    }

    public sealed class HomeShopPurchaseResult
    {
        public HomeShopPurchaseStatus Status { get; }
        public int GoodsId { get; }
        public long Quantity { get; }
        public string Message { get; }
        public bool IsApplied => Status == HomeShopPurchaseStatus.Applied;

        internal HomeShopPurchaseResult(HomeShopPurchaseStatus status, int goodsId, long quantity, string message = null)
        {
            Status = status;
            GoodsId = goodsId;
            Quantity = quantity;
            Message = message;
        }
    }

    /// <summary>Runs the official home-shop rules against the selected local profile.</summary>
    public sealed class OfflineHomeShopService
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly OfficialHomeShopCatalog catalog;

        public OfflineHomeShopService(OfflineMainlineProgressSession profile, OfficialHomeShopCatalog catalog)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public IReadOnlyList<OfficialHomeShopGoods> GetVisibleFixedGoods()
        {
            return catalog.Data.fixed_goods
                .Where(goods => goods.map_unlock_id == 0 || profile.Save.Player.UnlockedMapClassIds.Contains(goods.map_unlock_id))
                .ToArray();
        }

        public long ShopScore => profile.Save.Player.HomeShop.ShopScore;

        public bool TryGetGroceryRefreshCost(out int currencyId, out int cost, out int remaining)
        {
            int refreshed = profile.Save.Player.HomeShop.GroceryRefreshedCount;
            remaining = Math.Max(0, catalog.Data.grocery_refresh_counts_limit - refreshed);
            if (!catalog.Data.grocery_user_refresh || remaining == 0)
            {
                currencyId = 0;
                cost = 0;
                return false;
            }
            int index = Math.Min(refreshed, catalog.Data.grocery_refresh_prices.Count - 1);
            currencyId = catalog.Data.grocery_refresh_currency_ids[index];
            cost = catalog.Data.grocery_refresh_prices[index];
            return true;
        }

        public HomeShopPurchaseResult RefreshGrocery(string requestId)
        {
            EnsureGroceryShelf();
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 120)
                return Result(HomeShopPurchaseStatus.InvalidRequest, 0, 1, "The refresh request is invalid.");
            if (!TryGetGroceryRefreshCost(out int currencyId, out int cost, out _))
                return Result(HomeShopPurchaseStatus.RefreshLimitReached, 0, 1, "The grocery refresh limit has been reached.");
            string eventId = "home-shop:grocery-refresh:" + profile.Save.Player.HomeShop.GroceryRefreshedCount + ":" + requestId;
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(eventId))
                return Result(HomeShopPurchaseStatus.Duplicate, 0, 1);
            if (profile.InventoryCount(currencyId) < cost)
                return Result(HomeShopPurchaseStatus.InsufficientCurrency, 0, 1, "Not enough currency to refresh the grocery shelf.");

            bool insufficientDuringCommit = false;
            bool limitReachedDuringCommit = false;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            ProgressionResult applied = profile.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineHomeShopProgress progress = save.Player.HomeShop;
                if (progress.GroceryRefreshedCount >= catalog.Data.grocery_refresh_counts_limit)
                {
                    limitReachedDuringCommit = true;
                    throw new InvalidOperationException("The grocery refresh limit has been reached.");
                }
                int refreshIndex = Math.Min(progress.GroceryRefreshedCount, catalog.Data.grocery_refresh_prices.Count - 1);
                int refreshCurrency = catalog.Data.grocery_refresh_currency_ids[refreshIndex];
                int refreshCost = catalog.Data.grocery_refresh_prices[refreshIndex];
                if (InventoryCount(save.Player, refreshCurrency) < refreshCost)
                {
                    insufficientDuringCommit = true;
                    throw new InvalidOperationException("Not enough currency to refresh the grocery shelf.");
                }
                InventoryOperationResult consumed = OfflineInventoryOperations.Consume(save.Player, refreshCurrency,
                    refreshCost, now);
                if (!consumed.IsApplied) throw new InvalidOperationException("The refresh currency could not be consumed.");
                progress.GroceryStock = GenerateGroceryStock(save.Player.SubRank, now, ref progress.RandomState);
                progress.GroceryRefreshedCount++;
            });
            if (applied.Status == ProgressionResultStatus.Applied)
                return Result(HomeShopPurchaseStatus.Applied, 0, 1);
            if (applied.Status == ProgressionResultStatus.Unchanged)
                return Result(HomeShopPurchaseStatus.Duplicate, 0, 1);
            if (insufficientDuringCommit)
                return Result(HomeShopPurchaseStatus.InsufficientCurrency, 0, 1, "Not enough currency to refresh the grocery shelf.");
            if (limitReachedDuringCommit)
                return Result(HomeShopPurchaseStatus.RefreshLimitReached, 0, 1, "The grocery refresh limit has been reached.");
            return Result(HomeShopPurchaseStatus.SaveRejected, 0, 1, applied.Message);
        }

        public void EnsureGroceryShelf()
        {
            if (profile.Save.Player.HomeShop.GroceryInitialized) return;
            long seed = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            if (seed == 0) seed = 0x4D595DF4D0F33173L;
            string eventId = "home-shop:grocery-init:" + Guid.NewGuid().ToString("N");
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            profile.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineHomeShopProgress progress = save.Player.HomeShop;
                if (progress.GroceryInitialized) return;
                progress.RandomState = seed;
                progress.GroceryStock = GenerateGroceryStock(save.Player.SubRank, now, ref progress.RandomState);
                progress.GroceryInitialized = true;
            });
        }

        public IReadOnlyList<OfflineHomeShopShelfItem> GetGroceryShelf()
        {
            EnsureGroceryShelf();
            RefreshWeeklyStock(true);
            var result = new List<OfflineHomeShopShelfItem>();
            foreach (OfflineHomeShopStock stock in profile.Save.Player.HomeShop.GroceryStock.OrderBy(value => value.SlotIndex))
            {
                OfficialHomeShopGoods goods = FindGoods(catalog.Data.grocery_groups, stock.GoodsId);
                if (goods == null) continue;
                result.Add(new OfflineHomeShopShelfItem
                {
                    SlotIndex = stock.SlotIndex,
                    Goods = goods,
                    RemainingInventory = stock.RemainingInventory,
                    BoughtCount = stock.BoughtCount,
                    PriceOff = stock.PriceOff,
                    IsRandom = true
                });
            }
            IReadOnlyList<OfficialHomeShopGoods> fixedGoods = GetVisibleFixedGoods();
            for (int i = 0; i < fixedGoods.Count; i++)
                result.Add(new OfflineHomeShopShelfItem
                {
                    SlotIndex = catalog.Data.grocery_random_count + i,
                    Goods = fixedGoods[i],
                    RemainingInventory = fixedGoods[i].inventory,
                    PriceOff = 1f,
                    IsRandom = false
                });
            return result;
        }

        public void EnsureTreasuryShelf()
        {
            if (profile.Save.Player.HomeShop.TreasuryInitialized) return;
            long seed = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            if (seed == 0) seed = 0x4D595DF4D0F33173L;
            string eventId = "home-shop:treasury-init:" + Guid.NewGuid().ToString("N");
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            profile.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineHomeShopProgress progress = save.Player.HomeShop;
                if (progress.TreasuryInitialized) return;
                progress.RandomState = seed;
                progress.TreasuryStock = GenerateTreasuryStock(save.CreatedAtUnixSeconds, now, ref progress.RandomState);
                progress.TreasuryInitialized = true;
            });
        }

        public IReadOnlyList<OfflineHomeShopShelfItem> GetTreasuryShelf()
        {
            EnsureTreasuryShelf();
            RefreshWeeklyStock(false);
            var result = new List<OfflineHomeShopShelfItem>();
            foreach (OfflineHomeShopStock stock in profile.Save.Player.HomeShop.TreasuryStock.OrderBy(value => value.SlotIndex))
            {
                OfficialHomeShopGoods goods = FindTreasuryGoods(stock.GoodsId);
                if (goods == null) continue;
                result.Add(new OfflineHomeShopShelfItem
                {
                    SlotIndex = stock.SlotIndex,
                    Goods = goods,
                    RemainingInventory = stock.RemainingInventory,
                    BoughtCount = stock.BoughtCount,
                    PriceOff = stock.PriceOff,
                    IsRandom = true
                });
            }
            return result;
        }

        public HomeShopPurchaseResult PurchaseTreasury(int slotIndex, long quantity, string requestId)
        {
            EnsureTreasuryShelf();
            RefreshWeeklyStock(false);
            return PurchaseRandomShelf(false, slotIndex, quantity, requestId);
        }

        public HomeShopPurchaseResult PurchaseGroceryRandom(int slotIndex, long quantity, string requestId)
        {
            EnsureGroceryShelf();
            RefreshWeeklyStock(true);
            return PurchaseRandomShelf(true, slotIndex, quantity, requestId);
        }

        private HomeShopPurchaseResult PurchaseRandomShelf(bool grocery, int slotIndex, long quantity, string requestId)
        {
            List<OfflineHomeShopStock> shelf = grocery
                ? profile.Save.Player.HomeShop.GroceryStock : profile.Save.Player.HomeShop.TreasuryStock;
            OfflineHomeShopStock currentStock = shelf.FirstOrDefault(value => value.SlotIndex == slotIndex);
            IReadOnlyList<OfficialHomeShopGroup> groups = grocery ? catalog.Data.grocery_groups : catalog.Data.treasury_groups;
            OfficialHomeShopGoods goods = currentStock == null ? null : FindGoods(groups, currentStock.GoodsId);
            if (goods == null) return Result(HomeShopPurchaseStatus.Unsupported, 0, quantity, "This shop slot is not available.");
            if (quantity <= 0 || string.IsNullOrWhiteSpace(requestId) || requestId.Length > 120
                || quantity > 1 && !goods.support_buy_multi_count)
                return Result(HomeShopPurchaseStatus.InvalidRequest, goods.goods_id, quantity, "The requested purchase quantity is invalid.");
            if (currentStock.RemainingInventory >= 0 && quantity > currentStock.RemainingInventory)
                return Result(HomeShopPurchaseStatus.OutOfStock, goods.goods_id, quantity, "The shop item is sold out.");

            long totalPrice;
            long rewardCount;
            long scoreGain;
            try
            {
                long unitPrice = checked((long)(goods.original_price * currentStock.PriceOff));
                totalPrice = checked(unitPrice * quantity);
                rewardCount = checked((long)goods.item_count * quantity);
                scoreGain = goods.currency_id == catalog.Data.shop_score_currency_id
                    ? checked((long)(totalPrice * catalog.Data.shop_score_multiplier)) : 0;
            }
            catch (OverflowException)
            {
                return Result(HomeShopPurchaseStatus.InvalidRequest, goods.goods_id, quantity, "The purchase total is too large.");
            }
            string eventId = "home-shop:" + (grocery ? "grocery" : "treasury") + "-buy:" + slotIndex + ":" + requestId;
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(eventId))
                return Result(HomeShopPurchaseStatus.Duplicate, goods.goods_id, quantity);
            if (profile.InventoryCount(goods.currency_id) < totalPrice)
                return Result(HomeShopPurchaseStatus.InsufficientCurrency, goods.goods_id, quantity, "Not enough shop currency.");

            bool insufficientDuringCommit = false;
            bool soldOutDuringCommit = false;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            ProgressionResult applied = profile.ApplyInventoryTransaction(eventId, save =>
            {
                List<OfflineHomeShopStock> currentShelf = grocery
                    ? save.Player.HomeShop.GroceryStock : save.Player.HomeShop.TreasuryStock;
                OfflineHomeShopStock stock = currentShelf.FirstOrDefault(value =>
                    value.SlotIndex == slotIndex && value.GoodsId == goods.goods_id);
                if (stock == null || stock.RemainingInventory >= 0 && quantity > stock.RemainingInventory)
                {
                    soldOutDuringCommit = true;
                    throw new InvalidOperationException("The shop item is sold out.");
                }
                if (goods.total_buy_count_limit >= 0 && quantity > goods.total_buy_count_limit - stock.BoughtCount)
                {
                    soldOutDuringCommit = true;
                    throw new InvalidOperationException("The shop purchase limit has been reached.");
                }
                if (InventoryCount(save.Player, goods.currency_id) < totalPrice)
                {
                    insufficientDuringCommit = true;
                    throw new InvalidOperationException("Not enough shop currency.");
                }
                InventoryOperationResult consumed = OfflineInventoryOperations.Consume(save.Player, goods.currency_id,
                    totalPrice, now);
                if (!consumed.IsApplied) throw new InvalidOperationException("The shop currency could not be consumed.");
                InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player, goods.item_id,
                    rewardCount, now);
                if (!granted.IsApplied) throw new InvalidOperationException("The purchased item could not be added to inventory.");
                if (stock.RemainingInventory >= 0) stock.RemainingInventory -= checked((int)quantity);
                stock.BoughtCount = checked(stock.BoughtCount + quantity);
                save.Player.HomeShop.ShopScore = checked(save.Player.HomeShop.ShopScore + scoreGain);
            });

            if (applied.Status == ProgressionResultStatus.Applied)
                return Result(HomeShopPurchaseStatus.Applied, goods.goods_id, quantity);
            if (applied.Status == ProgressionResultStatus.Unchanged)
                return Result(HomeShopPurchaseStatus.Duplicate, goods.goods_id, quantity);
            if (insufficientDuringCommit)
                return Result(HomeShopPurchaseStatus.InsufficientCurrency, goods.goods_id, quantity);
            if (soldOutDuringCommit)
                return Result(HomeShopPurchaseStatus.OutOfStock, goods.goods_id, quantity);
            return Result(HomeShopPurchaseStatus.SaveRejected, goods.goods_id, quantity, applied.Message);
        }

        public IReadOnlyList<OfflineHomeShopGiftEntry> GetGiftEntries(int maximumCount = 5)
        {
            if (maximumCount <= 0) return Array.Empty<OfflineHomeShopGiftEntry>();
            OfflineHomeShopProgress progress = profile.Save.Player.HomeShop;
            OfficialHomeShopAchievementRules rules = catalog.Data.shop_score_achievements;
            int totalCount = rules.independent.Count + 1 + rules.cycle_count * rules.cycle_templates.Count;
            var claimed = new HashSet<int>(progress.ClaimedGiftAchievementIds);
            var result = new List<OfflineHomeShopGiftEntry>(maximumCount);
            int achievedThrough = LastMilestoneIndexAtOrBelow(rules, progress.ShopScore, totalCount);
            for (int index = 0; index <= achievedThrough && result.Count < maximumCount; index++)
            {
                int achievementId = GiftAchievementId(index, rules.achievement_group);
                if (!claimed.Contains(achievementId)) result.Add(GiftEntry(rules, index, progress.ShopScore));
            }
            for (int index = achievedThrough + 1; index < totalCount && result.Count < maximumCount; index++)
            {
                int achievementId = GiftAchievementId(index, rules.achievement_group);
                if (!claimed.Contains(achievementId)) result.Add(GiftEntry(rules, index, progress.ShopScore));
            }
            return result;
        }

        public HomeShopPurchaseResult ClaimGift(int achievementId, string requestId)
        {
            OfficialHomeShopAchievementRules rules = catalog.Data.shop_score_achievements;
            int index = achievementId >> 12;
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 120
                || (achievementId & 0xFFF) != rules.achievement_group || index < 0
                || index >= rules.independent.Count + 1 + rules.cycle_count * rules.cycle_templates.Count)
                return Result(HomeShopPurchaseStatus.InvalidRequest, achievementId, 1, "The gift request is invalid.");
            OfflineHomeShopGiftEntry entry = GiftEntry(rules, index, profile.Save.Player.HomeShop.ShopScore);
            if (profile.Save.Player.HomeShop.ClaimedGiftAchievementIds.Contains(achievementId))
                return Result(HomeShopPurchaseStatus.Duplicate, achievementId, 1);
            if (!entry.CanClaim) return Result(HomeShopPurchaseStatus.Locked, achievementId, 1, "The shop score requirement is not met.");

            string eventId = "home-shop:gift:" + achievementId + ":" + requestId;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ProgressionResult applied = profile.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineHomeShopProgress progress = save.Player.HomeShop;
                if (progress.ClaimedGiftAchievementIds.Contains(achievementId))
                    throw new InvalidOperationException("This HomeShop gift has already been claimed.");
                if (progress.ShopScore < entry.TargetScore)
                    throw new InvalidOperationException("The shop score requirement is not met.");
                foreach (OfficialHomeShopReward reward in entry.Milestone.rewards)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player, reward.item_id,
                        reward.count, now);
                    if (!granted.IsApplied) throw new InvalidOperationException("The HomeShop gift could not be added to inventory.");
                }
                progress.ClaimedGiftAchievementIds.Add(achievementId);
            });

            if (applied.Status == ProgressionResultStatus.Applied)
                return Result(HomeShopPurchaseStatus.Applied, achievementId, 1);
            if (applied.Status == ProgressionResultStatus.Unchanged
                || profile.Save.Player.HomeShop.ClaimedGiftAchievementIds.Contains(achievementId))
                return Result(HomeShopPurchaseStatus.Duplicate, achievementId, 1);
            return Result(HomeShopPurchaseStatus.SaveRejected, achievementId, 1, applied.Message);
        }

        public HomeShopPurchaseResult Purchase(int goodsId, long quantity, string requestId)
        {
            OfficialHomeShopGoods goods = catalog.Data.fixed_goods.FirstOrDefault(value => value.goods_id == goodsId);
            if (goods == null)
                return Result(HomeShopPurchaseStatus.Unsupported, goodsId, quantity, "This shelf is not available offline.");
            if (quantity <= 0 || string.IsNullOrWhiteSpace(requestId) || requestId.Length > 120
                || quantity > 1 && !goods.support_buy_multi_count)
                return Result(HomeShopPurchaseStatus.InvalidRequest, goodsId, quantity, "The requested purchase quantity is invalid.");
            if (goods.map_unlock_id > 0 && !profile.Save.Player.UnlockedMapClassIds.Contains(goods.map_unlock_id))
                return Result(HomeShopPurchaseStatus.Locked, goodsId, quantity, "The official map unlock condition is not met.");

            long totalPrice;
            long rewardCount;
            try
            {
                totalPrice = checked(goods.original_price * quantity);
                rewardCount = checked((long)goods.item_count * quantity);
            }
            catch (OverflowException)
            {
                return Result(HomeShopPurchaseStatus.InvalidRequest, goodsId, quantity, "The purchase total is too large.");
            }
            string eventId = "home-shop:buy:" + goodsId + ":" + requestId;
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(eventId))
                return Result(HomeShopPurchaseStatus.Duplicate, goodsId, quantity);
            if (profile.InventoryCount(goods.currency_id) < totalPrice)
                return Result(HomeShopPurchaseStatus.InsufficientCurrency, goodsId, quantity, "Not enough official shop currency.");

            bool insufficientDuringCommit = false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ProgressionResult applied = profile.ApplyInventoryTransaction(eventId, save =>
            {
                if (InventoryCount(save.Player, goods.currency_id) < totalPrice)
                {
                    insufficientDuringCommit = true;
                    throw new InvalidOperationException("Not enough official shop currency.");
                }
                InventoryOperationResult consumed = OfflineInventoryOperations.Consume(save.Player, goods.currency_id,
                    totalPrice, now);
                if (!consumed.IsApplied)
                    throw new InvalidOperationException("The shop currency could not be consumed.");
                InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player, goods.item_id,
                    rewardCount, now);
                if (!granted.IsApplied)
                    throw new InvalidOperationException("The purchased item could not be added to inventory.");
            });

            if (applied.Status == ProgressionResultStatus.Applied)
                return Result(HomeShopPurchaseStatus.Applied, goodsId, quantity);
            if (applied.Status == ProgressionResultStatus.Unchanged)
                return Result(HomeShopPurchaseStatus.Duplicate, goodsId, quantity);
            if (insufficientDuringCommit)
                return Result(HomeShopPurchaseStatus.InsufficientCurrency, goodsId, quantity);
            return Result(HomeShopPurchaseStatus.SaveRejected, goodsId, quantity, applied.Message);
        }

        private List<OfflineHomeShopStock> GenerateTreasuryStock(long createdAt, long now, ref long randomState)
        {
            if (randomState == 0) randomState = 0x4D595DF4D0F33173L;
            int registeredDays = RegisteredDays(createdAt, now);
            var groups = catalog.Data.treasury_groups.ToDictionary(group => group.group_id);
            var result = new List<OfflineHomeShopStock>(catalog.Data.treasury_random_count);
            var selectedGoods = new HashSet<int>();
            long weeklyKey = WeekCycleKey(now);
            int slot = 0;
            for (int groupIndex = 0; groupIndex < catalog.Data.treasury_group_ids.Count; groupIndex++)
            {
                OfficialHomeShopGroup group = groups[catalog.Data.treasury_group_ids[groupIndex]];
                int drawCount = catalog.Data.treasury_group_draw_counts[groupIndex];
                var availableGoods = group.goods.Where(goods => IsConditionMet(goods.condition_type,
                    goods.condition_day, goods.condition_op, registeredDays, profile.Save.Player.SubRank,
                    goods.condition_from_sub_rank, goods.condition_to_sub_rank)
                    && (!catalog.Data.treasury_unique || !selectedGoods.Contains(goods.goods_id))).ToList();
                for (int draw = 0; draw < drawCount; draw++)
                {
                    if (availableGoods.Count == 0) { slot++; continue; }
                    OfficialHomeShopGoods goods = SelectWeighted(availableGoods, value => value.weight, ref randomState);
                    result.Add(NewStock(slot, goods, profile.Save.Player.SubRank, weeklyKey, ref randomState));
                    if (catalog.Data.treasury_unique)
                    {
                        selectedGoods.Add(goods.goods_id);
                        availableGoods.Remove(goods);
                    }
                    slot++;
                }
            }
            return result;
        }

        private List<OfflineHomeShopStock> GenerateGroceryStock(int subRank, long now, ref long randomState)
        {
            var groups = catalog.Data.grocery_groups.ToDictionary(group => group.group_id);
            var result = new List<OfflineHomeShopStock>(catalog.Data.grocery_random_count);
            var selectedGoods = new HashSet<int>();
            long weeklyKey = WeekCycleKey(now);
            int slot = 0;
            for (int rootIndex = 0; rootIndex < catalog.Data.grocery_group_ids.Count; rootIndex++)
            {
                OfficialHomeShopGroup root = groups[catalog.Data.grocery_group_ids[rootIndex]];
                int drawCount = catalog.Data.grocery_group_draw_counts[rootIndex];
                for (int draw = 0; draw < drawCount; draw++)
                {
                    if (!GroupHasEligibleGoods(root, groups, subRank, selectedGoods, catalog.Data.grocery_unique))
                        continue;
                    OfficialHomeShopGoods selected = SelectGroceryGoods(root, groups, subRank, selectedGoods,
                        catalog.Data.grocery_unique, ref randomState);
                    result.Add(NewStock(slot++, selected, subRank, weeklyKey, ref randomState));
                    if (catalog.Data.grocery_unique) selectedGoods.Add(selected.goods_id);
                }
            }
            return result;
        }

        private static OfficialHomeShopGoods SelectGroceryGoods(OfficialHomeShopGroup group,
            IReadOnlyDictionary<int, OfficialHomeShopGroup> groups, int subRank,
            HashSet<int> selectedGoods, bool unique, ref long randomState)
        {
            if (group.complex_group_ids.Count > 0)
            {
                var eligibleChildren = group.complex_group_ids.Select((id, index) => new
                {
                    Group = groups[id],
                    Weight = group.complex_group_weights[index]
                }).Where(value => GroupHasEligibleGoods(value.Group, groups, subRank, selectedGoods, unique)).ToList();
                if (eligibleChildren.Count == 0)
                    throw new InvalidDataException("An official grocery group has no eligible nested group.");
                OfficialHomeShopGroup child = SelectWeighted(eligibleChildren, value => value.Weight, ref randomState).Group;
                return SelectGroceryGoods(child, groups, subRank, selectedGoods, unique, ref randomState);
            }

            List<OfficialHomeShopGoods> eligible = group.goods.Where(goods => IsConditionMet(goods.condition_type,
                goods.condition_day, goods.condition_op, 0, subRank,
                goods.condition_from_sub_rank, goods.condition_to_sub_rank)
                && (!unique || !selectedGoods.Contains(goods.goods_id))).ToList();
            if (eligible.Count == 0) throw new InvalidDataException("An official grocery group has no eligible good.");
            return SelectWeighted(eligible, goods => goods.weight, ref randomState);
        }

        private static bool GroupHasEligibleGoods(OfficialHomeShopGroup group,
            IReadOnlyDictionary<int, OfficialHomeShopGroup> groups, int subRank,
            HashSet<int> selectedGoods, bool unique)
        {
            if (group.goods.Any(goods => IsConditionMet(goods.condition_type, goods.condition_day,
                goods.condition_op, 0, subRank, goods.condition_from_sub_rank, goods.condition_to_sub_rank)
                && (!unique || !selectedGoods.Contains(goods.goods_id))))
                return true;
            return group.complex_group_ids.Any(id => GroupHasEligibleGoods(groups[id], groups, subRank,
                selectedGoods, unique));
        }

        private static OfflineHomeShopStock NewStock(int slot, OfficialHomeShopGoods goods, int subRank,
            long weeklyKey, ref long randomState)
        {
            return new OfflineHomeShopStock
            {
                SlotIndex = slot,
                GoodsId = goods.goods_id,
                RemainingInventory = goods.inventory,
                BoughtCount = 0,
                PriceOff = SelectPriceOff(goods, subRank, ref randomState),
                RefreshCycleKey = weeklyKey
            };
        }

        private void RefreshWeeklyStock(bool grocery)
        {
            List<OfflineHomeShopStock> current = grocery
                ? profile.Save.Player.HomeShop.GroceryStock : profile.Save.Player.HomeShop.TreasuryStock;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            long cycleKey = WeekCycleKey(now);
            IReadOnlyList<OfficialHomeShopGroup> groups = grocery ? catalog.Data.grocery_groups : catalog.Data.treasury_groups;
            bool needsRefresh = current.Any(stock =>
            {
                OfficialHomeShopGoods goods = FindGoods(groups, stock.GoodsId);
                return goods != null && goods.refresh_cycle_type == 2 && stock.RefreshCycleKey != cycleKey;
            });
            if (!needsRefresh) return;

            string eventId = "home-shop:weekly-reset:" + (grocery ? "grocery:" : "treasury:") + cycleKey;
            profile.ApplyInventoryTransaction(eventId, save =>
            {
                List<OfflineHomeShopStock> stockList = grocery
                    ? save.Player.HomeShop.GroceryStock : save.Player.HomeShop.TreasuryStock;
                foreach (OfflineHomeShopStock stock in stockList)
                {
                    OfficialHomeShopGoods goods = FindGoods(groups, stock.GoodsId);
                    if (goods == null || goods.refresh_cycle_type != 2 || stock.RefreshCycleKey == cycleKey) continue;
                    stock.RemainingInventory = goods.inventory;
                    stock.BoughtCount = 0;
                    stock.RefreshCycleKey = cycleKey;
                }
            });
        }

        private long WeekCycleKey(long nowUnixSeconds)
        {
            DateTime now = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).LocalDateTime;
            TimeSpan reset = new TimeSpan(catalog.Data.registered_days_reset_hour,
                catalog.Data.registered_days_reset_minute, catalog.Data.registered_days_reset_second);
            if (now.TimeOfDay < reset) now = now.AddDays(-1);
            int daysFromWeekStart = ((int)now.DayOfWeek - catalog.Data.weekly_start_day_of_week + 7) % 7;
            DateTime weekStart = now.Date.Add(reset).AddDays(-daysFromWeekStart);
            return new DateTimeOffset(weekStart).ToUnixTimeSeconds();
        }

        private static float SelectPriceOff(OfficialHomeShopGoods goods, int subRank, ref long randomState)
        {
            OfficialHomeShopPriceOff row = goods.price_off_options
                .Where(value => value.sub_rank <= subRank).OrderBy(value => value.sub_rank).LastOrDefault();
            if (row == null || row.values == null || row.values.Count == 0) return 1f;
            int index = (int)(NextRandom(ref randomState) % (ulong)row.values.Count);
            return row.values[index];
        }

        private static T SelectWeighted<T>(IReadOnlyList<T> values, Func<T, int> weight, ref long randomState)
        {
            long total = 0;
            foreach (T value in values) total = checked(total + weight(value));
            if (total <= 0) throw new InvalidDataException("An official HomeShop weighted selector has no positive entries.");
            long selected = (long)(NextRandom(ref randomState) % (ulong)total);
            foreach (T value in values)
            {
                selected -= weight(value);
                if (selected < 0) return value;
            }
            throw new InvalidDataException("An official HomeShop weighted selector exceeded its weight range.");
        }

        private static ulong NextRandom(ref long state)
        {
            ulong value = unchecked((ulong)state);
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            state = unchecked((long)value);
            return value * 2685821657736338717UL;
        }

        private int RegisteredDays(long createdAt, long now)
        {
            if (createdAt <= 0) return 0;
            DateTime start = DateTimeOffset.FromUnixTimeSeconds(createdAt).LocalDateTime;
            DateTime end = DateTimeOffset.FromUnixTimeSeconds(now).LocalDateTime;
            TimeSpan resetTime = new TimeSpan(catalog.Data.registered_days_reset_hour,
                catalog.Data.registered_days_reset_minute, catalog.Data.registered_days_reset_second);
            int days = (int)(end.Date.Add(resetTime) - start.Date.Add(resetTime)).TotalDays;
            if (start.TimeOfDay < resetTime) days++;
            if (end.TimeOfDay < resetTime) days--;
            return days;
        }

        private static bool IsConditionMet(string type, int day, string op, int registeredDays,
            int subRank, int fromSubRank, int toSubRank)
        {
            if (type == "NoneCondition") return true;
            if (type == "SubRankBetweenCondition") return subRank >= fromSubRank && subRank <= toSubRank;
            if (type != "RegisteredDaysCondition") return false;
            switch (op)
            {
                case ">=": return registeredDays >= day;
                case ">": return registeredDays > day;
                case "==": return registeredDays == day;
                case "<=": return registeredDays <= day;
                case "<": return registeredDays < day;
                default: return false;
            }
        }

        private OfficialHomeShopGoods FindTreasuryGoods(int goodsId)
        {
            return FindGoods(catalog.Data.treasury_groups, goodsId);
        }

        private static OfficialHomeShopGoods FindGoods(IReadOnlyList<OfficialHomeShopGroup> groups, int goodsId)
        {
            foreach (OfficialHomeShopGroup group in groups)
            {
                OfficialHomeShopGoods goods = group.goods.FirstOrDefault(value => value.goods_id == goodsId);
                if (goods != null) return goods;
            }
            return null;
        }

        private static int LastMilestoneIndexAtOrBelow(OfficialHomeShopAchievementRules rules, long score, int totalCount)
        {
            int low = 0;
            int high = totalCount;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (MilestoneTarget(rules, middle) <= score) low = middle + 1;
                else high = middle;
            }
            return low - 1;
        }

        private static OfflineHomeShopGiftEntry GiftEntry(OfficialHomeShopAchievementRules rules, int index, long score)
        {
            int target = MilestoneTarget(rules, index);
            return new OfflineHomeShopGiftEntry
            {
                AchievementId = GiftAchievementId(index, rules.achievement_group),
                Index = index,
                TargetScore = target,
                CurrentScore = score,
                CanClaim = score >= target,
                Milestone = MilestoneAt(rules, index)
            };
        }

        private static int MilestoneTarget(OfficialHomeShopAchievementRules rules, int index)
        {
            int independentCount = rules.independent.Count;
            if (index < independentCount) return rules.independent[index].target_score;
            int anchorIndex = independentCount;
            if (index == anchorIndex) return rules.cycle_anchor.target_score;
            long steps = index - anchorIndex;
            long cycles = steps / rules.cycle_templates.Count;
            int remainder = (int)(steps % rules.cycle_templates.Count);
            long increase = 0;
            for (int i = 0; i < rules.cycle_templates.Count; i++)
            {
                int repeats = checked((int)cycles + (i < remainder ? 1 : 0));
                increase = checked(increase + (long)rules.cycle_templates[i].cycle_increment * repeats);
            }
            return checked(rules.cycle_anchor.target_score + (int)increase);
        }

        private static OfficialHomeShopMilestone MilestoneAt(OfficialHomeShopAchievementRules rules, int index)
        {
            if (index < rules.independent.Count) return rules.independent[index];
            if (index == rules.independent.Count) return rules.cycle_anchor;
            int templateIndex = (index - rules.independent.Count - 1) % rules.cycle_templates.Count;
            return rules.cycle_templates[templateIndex];
        }

        private static int GiftAchievementId(int index, int groupId)
        {
            return checked((index << 12) | (groupId & 0xFFF));
        }

        private static HomeShopPurchaseResult Result(HomeShopPurchaseStatus status, int goodsId, long quantity,
            string message = null) => new HomeShopPurchaseResult(status, goodsId, quantity, message);

        private static long InventoryCount(OfflinePlayerProgress player, int itemId)
        {
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            return stack == null ? 0 : stack.Count;
        }
    }
}
