using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Skills;

namespace Zjcs.Offline.Progression
{
    public enum WishResultCode
    {
        Applied,
        Unchanged,
        InvalidArgument,
        InsufficientItems,
        DailyLimit,
        ConditionNotMet,
        NotReady,
        InvalidState
    }

    public sealed class WishResult
    {
        public WishResultCode Code { get; }
        public string Message { get; }
        public IReadOnlyList<OfflineWishAward> Awards { get; }
        public bool IsApplied => Code == WishResultCode.Applied;

        internal WishResult(WishResultCode code, string message = null, IEnumerable<OfflineWishAward> awards = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            Awards = (awards ?? Array.Empty<OfflineWishAward>()).ToArray();
        }
    }

    public sealed class OfflineWishService
    {
        private const int HistoryLimitPerPool = 1000;
        private const int HistoryDays = 180;
        private readonly OfflineMainlineProgressSession profile;
        private readonly WishRuntimeCatalog catalog;

        public OfflineWishService(OfflineMainlineProgressSession profile, WishRuntimeCatalog catalog)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public WishRuntimeCatalog Catalog => catalog;
        public OfflineWishProgress State => profile.Save.Player.Wishes;

        public bool GuideRewardClaimed => State.TutorialState > 0;

        public WishResult ClaimGuideReward(long nowUnixSeconds, string receiptId)
        {
            WishFixedReward reward = catalog.FixedReward(catalog.settings.SkillGachaGuideFixedRewardId);
            if (reward == null || reward.CanGetTimes <= 0 || string.IsNullOrWhiteSpace(receiptId)
                || nowUnixSeconds < 0)
                return Result(WishResultCode.InvalidState, "新人祈愿奖励配置无效");
            profile.Reload();
            if (State.TutorialState > 0) return Result(WishResultCode.Unchanged);
            var awards = (reward.Reward ?? Array.Empty<WishAmount>()).Select(amount => new OfflineWishAward
            {
                ItemId = amount.ItemId,
                Count = amount.Count,
                Quality = catalog.Item(amount.ItemId)?.Quality ?? 0,
                OriginItemId = amount.ItemId
            }).ToArray();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineWishProgress wish = save.Player.Wishes;
                if (wish.TutorialState > 0) throw new InvalidOperationException("新人祈愿奖励已领取");
                foreach (WishAmount amount in reward.Reward ?? Array.Empty<WishAmount>())
                    GrantConfiguredItem(save.Player, amount.ItemId, amount.Count, nowUnixSeconds);
                wish.TutorialState = 1;
            });
            return new WishResult(Map(transaction.Status), transaction.Message,
                transaction.IsApplied ? awards : null);
        }

        public int DailyCount(DateTime localNow)
        {
            int day = DayKey(localNow);
            int classType = catalog.gachas.Length == 0 ? 0 : catalog.gachas[0].GachaClassType;
            OfflineWishDailyCounter entry = State.DailyCounts.FirstOrDefault(value =>
                value.DayKey == day && value.ClassType == classType);
            return entry == null ? 0 : entry.Count;
        }

        public long PoolCount(int gachaType)
        {
            OfflineWishPoolCounter entry = State.PoolCounts.FirstOrDefault(value => value.GachaType == gachaType);
            return entry == null ? 0L : entry.Count;
        }

        public int SelectedProfession(int gachaType, int fallback)
        {
            int selected = gachaType == 0 ? State.SelectedRegularProfession
                : gachaType == 2 ? State.SelectedSeniorProfession : 0;
            if (selected > 0 && AvailableDrawProfessions(gachaType,
                profile.Save.Player.Wardrobe.Profession).Contains(selected)) return selected;
            return fallback;
        }

        public IReadOnlyList<int> AvailableDrawProfessions(int gachaType, int preferredProfession)
        {
            int maxRank = Math.Max(1, profile.Save.Player.MaxProfessionRank);
            WishProfession[] unlocked = catalog.professions.Where(value => value != null && value.Rank <= maxRank).ToArray();
            if (gachaType == 2)
                return unlocked.OrderBy(value => value.Profession == preferredProfession ? 0 : 1)
                    .ThenByDescending(value => value.Rank).ThenBy(value => value.Profession)
                    .Select(value => value.Profession).ToArray();
            if (gachaType != 0 || unlocked.Length == 0) return Array.Empty<int>();

            int highestRank = catalog.professions.Where(value => value != null).Max(value => value.Rank);
            var result = new List<int>();
            var seen = new HashSet<int>();
            foreach (WishProfession endpoint in catalog.professions.Where(value => value != null && value.Rank == highestRank))
            {
                WishProfession current = endpoint;
                for (int depth = 0; current != null && current.Rank > maxRank && depth < 32; depth++)
                {
                    if (current.PrePro <= 0 || current.PrePro == current.Profession)
                    {
                        current = null;
                        break;
                    }
                    current = catalog.Profession(current.PrePro);
                }
                if (current != null && current.Rank <= maxRank && seen.Add(current.Profession))
                    result.Add(current.Profession);
            }
            return result;
        }

        public WishResult SetDrawProfession(int gachaType, int profession, string receiptId)
        {
            WishProfession row = catalog.Profession(profession);
            if ((gachaType != 0 && gachaType != 2) || row == null || string.IsNullOrWhiteSpace(receiptId))
                return Result(WishResultCode.InvalidArgument, "请选择有效职业");
            profile.Reload();
            if (!AvailableDrawProfessions(gachaType, profile.Save.Player.Wardrobe.Profession).Contains(profession))
                return Result(WishResultCode.ConditionNotMet, "该职业尚未解锁");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                if (gachaType == 0) save.Player.Wishes.SelectedRegularProfession = profession;
                else save.Player.Wishes.SelectedSeniorProfession = profession;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public int QualityMissCount(int gachaType, int quality)
        {
            WishGacha gacha = catalog.Gacha(gachaType);
            WishChest chest = gacha == null ? null : catalog.Chest(gacha.ChestId);
            WishRewardRule rule = chest == null || chest.RewardRules.Length == 0
                ? null : catalog.RewardRule(chest.RewardRules[0]);
            int ruleId = SharedRuleId(rule);
            OfflineWishPityCounter counter = State.PityCounters.FirstOrDefault(value =>
                value.RuleId == ruleId && value.Quality == quality);
            return counter == null ? 0 : counter.MissCount;
        }

        public int ProfessionQualityMissCount(int gachaType, int profession, int quality)
        {
            OfflineWishProfessionPityCounter counter = State.ProfessionPityCounters.FirstOrDefault(value =>
                value.GachaType == gachaType && value.Profession == profession);
            return counter == null || counter.QualityMissCounts == null || quality < 0 || quality >= counter.QualityMissCounts.Length
                ? 0 : counter.QualityMissCounts[quality];
        }

        public WishResult Draw(int gachaType, int count, int profession, string receiptId, long nowUnixSeconds,
            bool confirmHundred = false)
        {
            WishGacha gacha = catalog.Gacha(gachaType);
            WishChest chest = gacha == null ? null : catalog.Chest(gacha.ChestId);
            if (gacha == null || chest == null || string.IsNullOrWhiteSpace(receiptId)
                || nowUnixSeconds < 0 || !chest.OpenCounts.Contains(count))
                return Result(WishResultCode.InvalidArgument, "祈愿次数或类型无效");
            DateTime localNow = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).ToLocalTime().DateTime;
            if (count == 100 && ShouldConfirmHundred(gachaType, localNow) && !confirmHundred)
                return Result(WishResultCode.ConditionNotMet, "百次祈愿需要再次确认");
            if (gachaType != 1 && (profession <= 0 || catalog.Profession(profession) == null))
                return Result(WishResultCode.InvalidArgument, "请选择有效职业");

            profile.Reload();
            if (gachaType == 1 ? profession != 0
                : !AvailableDrawProfessions(gachaType, profile.Save.Player.Wardrobe.Profession).Contains(profession))
                return Result(WishResultCode.ConditionNotMet, "该职业尚未解锁或不属于当前祈愿池");
            if (gachaType == 0)
            {
                int requiredRank = catalog.settings.SkillGachaCustomSelectionSettings.RequiredProfessionRank;
                int requiredCount = catalog.settings.SkillGachaCustomSelectionSettings.SelectionProfessionCount;
                OfflineWishProfessionSelection selection = State.ProfessionSelections.FirstOrDefault(value =>
                    value.Profession == profession && value.Completed && value.SelectedProfessions != null
                    && value.SelectedProfessions.Distinct().Count() == requiredCount);
                if (catalog.Profession(profession).Rank >= requiredRank && selection == null)
                    return Result(WishResultCode.ConditionNotMet, "请先完成该职业的四职业自选");
            }
            int day = DayKey(localNow);
            int maximum = catalog.settings.DailyGachaMaxCount.FirstOrDefault(value =>
                value.GachaClassType == gacha.GachaClassType)?.Count ?? 0;
            int used = DailyCount(localNow);
            if (maximum > 0 && used + count > maximum)
                return Result(WishResultCode.DailyLimit, "今日祈愿次数已达上限");

            if (chest.CostItems.Any(value => profile.InventoryCount(value.ItemId) < checked(value.Count * count)))
                return Result(WishResultCode.InsufficientItems, "祈愿材料不足");

            var resultAwards = new List<OfflineWishAward>();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineWishProgress wish = save.Player.Wishes;
                int transactionUsed = DailyCountIn(wish, day, gacha.GachaClassType);
                if (maximum > 0 && transactionUsed + count > maximum)
                    throw new InvalidOperationException("今日祈愿次数已达上限");
                foreach (WishAmount cost in chest.CostItems)
                    Consume(save.Player, cost.ItemId, checked(cost.Count * count), nowUnixSeconds);

                WishRewardRule rule = chest.RewardRules.Select(catalog.RewardRule).FirstOrDefault(value => value != null);
                if (rule == null) throw new InvalidOperationException("祈愿奖励规则不存在");
                WishRewardRule sharedRule = catalog.RewardRule(SharedRuleId(rule)) ?? rule;
                EnsureGuarantees(wish, gachaType, profession, sharedRule);
                EnsureRandomState(ref wish.RandomState, nowUnixSeconds, receiptId);
                List<OfflineWishDrawRecord> history = new List<OfflineWishDrawRecord>(count);

                for (int index = 0; index < count; index++)
                {
                    long sequence = checked(PoolSequence(wish, gachaType) + 1);
                    OfflineWishScheduledGuarantee scheduled = wish.ScheduledGuarantees
                        .Where(value => value.GachaType == gachaType && value.TargetSequence == sequence
                            && (value.Kind == 0 || value.Profession == profession))
                        .OrderBy(value => value.Kind).ThenBy(value => value.GuaranteeId).FirstOrDefault();
                    WishDrop drop = Roll(gachaType, profession, rule, sharedRule, wish, scheduled, ref wish.RandomState);
                    OfflineWishAward award = GrantDrop(save.Player, drop, nowUnixSeconds);
                    resultAwards.Add(award);

                    long scoreCount = GetChestScoreCount(chest);
                    if (scoreCount > 0) GrantItem(save.Player, gacha.RewardScoreId, scoreCount, nowUnixSeconds);
                    UpdatePoolAndAchievementProgress(wish, gachaType, 1, scoreCount);
                    if (gachaType != 1)
                        UpdatePity(wish, gachaType, profession, sharedRule, drop.Quality,
                            drop.IsSkill, drop.IsPiece);
                    wish.DrawSequence = checked(wish.DrawSequence + 1);
                    if (gachaType == 0 && wish.NewbieDrawCount < int.MaxValue) wish.NewbieDrawCount++;
                    history.Add(new OfflineWishDrawRecord
                    {
                        ReceiptId = receiptId,
                        DrawIndex = index,
                        GachaType = gachaType,
                        Profession = profession,
                        UnixSeconds = nowUnixSeconds,
                        Rewards = new List<OfflineWishAward> { CloneAward(award) }
                    });
                    RemoveResolvedGuarantees(wish, gachaType, profession, sequence, drop.Quality,
                        drop.IsSkill, scheduled);
                    EnsureGuarantees(wish, gachaType, profession, sharedRule);
                }

                OfflineWishDailyCounter daily = FindOrCreateDaily(wish, day, gacha.GachaClassType);
                daily.Count = checked(daily.Count + count);
                foreach (OfflineWishAchievementProgress progress in wish.AchievementProgress)
                    progress.Progress = GroupProgress(progress.Group, wish);
                wish.History.AddRange(history);
                TrimHistory(wish, nowUnixSeconds);
                EnsureGuarantees(wish, gachaType, profession, sharedRule);
            });

            if (transaction.Status == ProgressionResultStatus.Unchanged)
            {
                profile.Reload();
                resultAwards.Clear();
                resultAwards.AddRange(State.History.Where(value => value.ReceiptId == receiptId)
                    .OrderBy(value => value.DrawIndex).SelectMany(value => value.Rewards).Select(CloneAward));
                return new WishResult(WishResultCode.Unchanged, transaction.Message, resultAwards);
            }
            if (!transaction.IsApplied)
                return Result(Map(transaction.Status), transaction.Message);
            return new WishResult(WishResultCode.Applied, awards: resultAwards);
        }

        public bool ShouldConfirmHundred(int gachaType, DateTime localNow)
        {
            OfflineWishReminderState state = State.HundredReminders.FirstOrDefault(value => value.GachaType == gachaType);
            return state == null || state.DayKey != DayKey(localNow);
        }

        public WishResult SetHundredReminder(int gachaType, DateTime localNow, bool suppress, string receiptId)
        {
            if (catalog.Gacha(gachaType) == null || string.IsNullOrWhiteSpace(receiptId))
                return Result(WishResultCode.InvalidArgument);
            profile.Reload();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineWishReminderState state = save.Player.Wishes.HundredReminders.FirstOrDefault(value => value.GachaType == gachaType);
                if (suppress)
                {
                    if (state == null) save.Player.Wishes.HundredReminders.Add(state = new OfflineWishReminderState { GachaType = gachaType });
                    state.DayKey = DayKey(localNow);
                }
                else if (state != null) save.Player.Wishes.HundredReminders.Remove(state);
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public IReadOnlyList<OfflineWishDrawRecord> History(int gachaType, long nowUnixSeconds, int page, int pageSize)
        {
            if (page < 0 || pageSize <= 0 || pageSize > 100 || nowUnixSeconds < 0) return Array.Empty<OfflineWishDrawRecord>();
            long cutoff = Math.Max(0, nowUnixSeconds - HistoryDays * 86400L);
            return State.History.Where(value => value.GachaType == gachaType && value.UnixSeconds >= cutoff)
                .OrderByDescending(value => value.UnixSeconds).ThenByDescending(value => value.DrawIndex)
                .Skip(page * pageSize).Take(pageSize).ToArray();
        }

        public WishResult SetProfessionSelection(int profession, IEnumerable<int> selectedProfessions, string receiptId)
        {
            WishProfession source = catalog.Profession(profession);
            int requiredRank = catalog.settings.SkillGachaCustomSelectionSettings.RequiredProfessionRank;
            int requiredCount = catalog.settings.SkillGachaCustomSelectionSettings.SelectionProfessionCount;
            int[] selection = (selectedProfessions ?? Array.Empty<int>()).Distinct()
                .OrderByDescending(value => catalog.Profession(value)?.Rank ?? 0)
                .ThenBy(value => value).ToArray();
            var predecessors = new HashSet<int>(catalog.ProfessionChain(profession).Skip(1));
            if (source == null || source.Rank < requiredRank || selection.Length != requiredCount
                || selection.Any(value => value <= 0 || catalog.Profession(value) == null || value == profession)
                || selection.Any(value => !predecessors.Contains(value)) || string.IsNullOrWhiteSpace(receiptId))
                return Result(WishResultCode.InvalidArgument, "自选职业无效");
            profile.Reload();
            int maxProfessionRank = Math.Max(1, profile.Save.Player.MaxProfessionRank);
            if (source.Rank > maxProfessionRank || selection.Any(value => catalog.Profession(value).Rank > maxProfessionRank))
                return Result(WishResultCode.ConditionNotMet, "所选职业尚未解锁");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineWishProfessionSelection row = save.Player.Wishes.ProfessionSelections.FirstOrDefault(value => value.Profession == profession);
                if (row == null) save.Player.Wishes.ProfessionSelections.Add(row = new OfflineWishProfessionSelection { Profession = profession });
                row.SelectedProfessions = new List<int>(selection);
                row.Completed = true;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public WishResult ResetProfessionSelection(int profession, string receiptId)
        {
            if (catalog.Profession(profession) == null || string.IsNullOrWhiteSpace(receiptId))
                return Result(WishResultCode.InvalidArgument);
            profile.Reload();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
                save.Player.Wishes.ProfessionSelections.RemoveAll(value => value.Profession == profession));
            return Result(Map(transaction.Status), transaction.Message);
        }

        public IReadOnlyList<WishAchievementEntry> Achievements(int group, int page = 0, int pageSize = 5)
        {
            if (page < 0 || pageSize <= 0) return Array.Empty<WishAchievementEntry>();
            List<WishAchievementEntry> entries = BuildAchievements(group);
            return entries.Skip(page * pageSize).Take(pageSize).ToArray();
        }

        public WishResult ClaimAchievement(int group, int index, string receiptId, long nowUnixSeconds)
        {
            if (string.IsNullOrWhiteSpace(receiptId) || nowUnixSeconds < 0) return Result(WishResultCode.InvalidArgument);
            WishAchievementEntry entry = BuildAchievements(group).FirstOrDefault(value => value.Index == index);
            if (entry == null || !entry.IsReady || entry.IsClaimed || !IsFirstUnclaimedCycle(group, index, State))
                return Result(WishResultCode.NotReady, "奖励尚未達成或已领取");
            profile.Reload();
            entry = BuildAchievements(group).FirstOrDefault(value => value.Index == index);
            if (entry == null || !entry.IsReady || entry.IsClaimed || !IsFirstUnclaimedCycle(group, index, State))
                return Result(WishResultCode.NotReady, "奖励状态已变化");

            var awards = new List<OfflineWishAward>();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                WishAchievementEntry current = BuildAchievements(group, save.Player.Wishes).FirstOrDefault(value => value.Index == index);
                if (current == null || !current.IsReady || current.IsClaimed
                    || !IsFirstUnclaimedCycle(group, index, save.Player.Wishes))
                    throw new InvalidOperationException("奖励状态已变化");
                foreach (WishAmount reward in current.Rewards)
                    awards.Add(GrantConfiguredItem(save.Player, reward.ItemId, reward.Count, nowUnixSeconds));
                OfflineWishAchievementProgress progress = save.Player.Wishes.AchievementProgress.FirstOrDefault(value =>
                    value.Group == group && value.TemplateId == current.TemplateId && value.CycleIndex == current.CycleRecordIndex);
                if (progress == null)
                    save.Player.Wishes.AchievementProgress.Add(progress = new OfflineWishAchievementProgress
                    {
                        Group = group,
                        TemplateId = current.TemplateId,
                        CycleIndex = current.CycleRecordIndex
                    });
                progress.Progress = current.CurrentValue;
                progress.ClaimedCount++;
            });
            return new WishResult(Map(transaction.Status), transaction.Message,
                transaction.IsApplied ? awards : null);
        }

        public IReadOnlyList<WishShopStockView> ShopGoods(int shopId, int profession, DateTime localNow)
        {
            WishShop shop = catalog.Shop(shopId);
            if (shop == null) return Array.Empty<WishShopStockView>();
            EnsureShopDay(shopId, profession, localNow);
            if (shopId == 4)
            {
                EnsureFixedShopStocks(shop);
                var fixedGoods = new List<WishShopStockView>();
                for (int slot = 0; slot < (shop.FixedGoodsList ?? Array.Empty<int>()).Length; slot++)
                {
                    int goodsId = shop.FixedGoodsList[slot];
                    WishShopGood good = catalog.ShopGood(goodsId);
                    if (good == null || !ShopGoodAvailable(good, profession)) continue;
                    OfflineWishShopStock stock = State.ShopStocks.First(value =>
                        value.ShopId == shopId && value.Slot == slot && value.GoodsId == goodsId);
                    fixedGoods.Add(new WishShopStockView(shopId, slot, goodsId,
                        stock.RemainingInventory, stock.BoughtCount));
                }
                return fixedGoods;
            }
            return State.ShopStocks.Where(value => value.ShopId == shopId)
                .OrderBy(value => value.Slot)
                .Select(value => new WishShopStockView(value.ShopId, value.Slot, value.GoodsId,
                    value.RemainingInventory, value.BoughtCount)).ToArray();
        }

        public int ManualRefreshCost(int shopId, int count)
        {
            WishShop shop = catalog.Shop(shopId);
            if (shop == null || count < 0 || count >= shop.RefreshPriceBase.Length) return -1;
            double factor = catalog.LevelMultiplier(shop.RefreshPriceFactor, profile.Save.Player.SubRank);
            return (int)(shop.RefreshPriceBase[count] * factor);
        }

        public WishResult RefreshShop(int shopId, int profession, DateTime localNow, string receiptId)
        {
            WishShop shop = catalog.Shop(shopId);
            if (shop == null || shopId != 5 || string.IsNullOrWhiteSpace(receiptId))
                return Result(WishResultCode.InvalidArgument);
            profile.Reload();
            OfflineWishShopState state = State.Shops.FirstOrDefault(value => value.ShopId == shopId);
            int day = DayKey(localNow);
            if (state == null || state.DayKey != day) EnsureShopDay(shopId, profession, localNow);
            state = State.Shops.FirstOrDefault(value => value.ShopId == shopId);
            if (state == null || state.ManualRefreshCount >= shop.RefreshCountsLimit)
                return Result(WishResultCode.ConditionNotMet, "今日刷新次数已用完");
            int price = ManualRefreshCost(shopId, state.ManualRefreshCount);
            int currency = shop.RefreshCurrency == null || shop.RefreshCurrency.Length == 0 ? 0 : shop.RefreshCurrency[0];
            if (price <= 0 || currency <= 0) return Result(WishResultCode.InvalidState, "刷新配置无效");
            if (profile.InventoryCount(currency) < price) return Result(WishResultCode.InsufficientItems, "刷新材料不足");

            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineWishShopState changed = save.Player.Wishes.Shops.First(value => value.ShopId == shopId);
                if (changed.DayKey != day || changed.ManualRefreshCount >= shop.RefreshCountsLimit)
                    throw new InvalidOperationException("今日刷新次数已用完");
                Consume(save.Player, currency, price, new DateTimeOffset(localNow).ToUnixTimeSeconds());
                changed.ManualRefreshCount++;
                GenerateRandomShop(save.Player.Wishes, shop, profession, day, changed, localNow);
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public WishResult BuyShopGood(int shopId, int goodsId, int slot, int count, int profession,
            long nowUnixSeconds, string receiptId)
        {
            WishShop shop = catalog.Shop(shopId);
            WishShopGood good = catalog.ShopGood(goodsId);
            if (shop == null || good == null || count <= 0 || slot < 0 || nowUnixSeconds < 0
                || string.IsNullOrWhiteSpace(receiptId) || !ShopGoodAvailable(good, profession))
                return Result(WishResultCode.InvalidArgument);
            if (!good.SupportBuyMultiCount && count > 1)
                return Result(WishResultCode.InvalidArgument, "该商品不支持批量购买");
            if (shopId == 4) EnsureFixedShopStocks(shop);
            profile.Reload();
            OfflineWishShopStock stock = State.ShopStocks.FirstOrDefault(value =>
                value.ShopId == shopId && value.Slot == slot && value.GoodsId == goodsId);
            if (shopId == 5 && (stock == null || stock.RemainingInventory == 0))
                return Result(WishResultCode.ConditionNotMet, "该商品已售罄");
            if (good.Inventory >= 0 && count > good.Inventory || stock != null && stock.RemainingInventory >= 0
                && count > stock.RemainingInventory)
                return Result(WishResultCode.ConditionNotMet, "购买数量超出库存");
            if (good.TotalBuyCountLimit >= 0 && stock != null && count + stock.BoughtCount > good.TotalBuyCountLimit)
                return Result(WishResultCode.ConditionNotMet, "已达到购买上限");
            long price = checked((long)Math.Ceiling(good.OriginalPrice * count));
            if (profile.InventoryCount(good.Currency) < price) return Result(WishResultCode.InsufficientItems, "货币不足");

            var awards = new List<OfflineWishAward>();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineWishShopStock currentStock = save.Player.Wishes.ShopStocks.FirstOrDefault(value =>
                    value.ShopId == shopId && value.Slot == slot && value.GoodsId == goodsId);
                if (shopId == 5 && (currentStock == null || currentStock.RemainingInventory == 0))
                    throw new InvalidOperationException("该商品已售罄");
                if (currentStock != null && currentStock.RemainingInventory >= 0)
                {
                    if (currentStock.RemainingInventory < count) throw new InvalidOperationException("购买数量超出库存");
                    currentStock.RemainingInventory -= count;
                }
                if (good.TotalBuyCountLimit >= 0 && (currentStock?.BoughtCount ?? 0) + count > good.TotalBuyCountLimit)
                    throw new InvalidOperationException("已达到购买上限");
                if (currentStock != null) currentStock.BoughtCount = checked(currentStock.BoughtCount + count);
                Consume(save.Player, good.Currency, price, nowUnixSeconds);
                awards.Add(GrantConfiguredItem(save.Player, good.ItemId,
                    checked((long)good.Count * count), nowUnixSeconds));
            });
            return new WishResult(Map(transaction.Status), transaction.Message,
                transaction.IsApplied ? awards : null);
        }

        public bool CanQuickBuy(int itemId, long count)
        {
            WishQuickBuy rule = catalog.quickBuys == null
                ? null : catalog.quickBuys.FirstOrDefault(value => value.ItemId == itemId);
            return count > 0 && rule != null && rule.CurrencyId > 0 && rule.BaseCount > 0
                && rule.PriceList != null && rule.PriceList.Length == 1
                && rule.PriceBuyCount != null && rule.PriceBuyCount.Length == 1
                && (rule.PriceBuyCount[0] < 0 || count <= rule.PriceBuyCount[0])
                && QuickBuyConditionMet(rule.Condition);
        }

        public WishResult QuickBuy(int itemId, int count, long nowUnixSeconds, string receiptId)
        {
            if (count <= 0 || string.IsNullOrWhiteSpace(receiptId) || nowUnixSeconds < 0)
                return Result(WishResultCode.InvalidArgument);
            profile.Reload();
            WishQuickBuy rule = catalog.quickBuys == null ? null : catalog.quickBuys.FirstOrDefault(value => value.ItemId == itemId);
            if (!CanQuickBuy(itemId, count))
                return Result(WishResultCode.ConditionNotMet, "该材料没有快捷购买配置");
            long total = checked((long)rule.PriceList[0] * count);
            if (profile.InventoryCount(rule.CurrencyId) < total) return Result(WishResultCode.InsufficientItems, "货币不足");
            var awards = new List<OfflineWishAward>();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                if (!CanQuickBuy(itemId, count))
                    throw new InvalidOperationException("尚未满足快捷购买条件");
                Consume(save.Player, rule.CurrencyId, total, nowUnixSeconds);
                awards.Add(GrantConfiguredItem(save.Player, itemId,
                    checked((long)rule.BaseCount * count), nowUnixSeconds));
            });
            return new WishResult(Map(transaction.Status), transaction.Message,
                transaction.IsApplied ? awards : null);
        }

        public WishResult SetTutorialState(int state, string receiptId)
        {
            if (state < 0 || state > 3 || string.IsNullOrWhiteSpace(receiptId)) return Result(WishResultCode.InvalidArgument);
            profile.Reload();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
                save.Player.Wishes.TutorialState = Math.Max(save.Player.Wishes.TutorialState, state));
            return Result(Map(transaction.Status), transaction.Message);
        }

        public void EnsureShopDay(int shopId, int profession, DateTime localNow)
        {
            WishShop shop = catalog.Shop(shopId);
            if (shop == null || shopId != 5) return;
            int day = DayKey(localNow);
            OfflineWishShopState existing = State.Shops.FirstOrDefault(value => value.ShopId == shopId);
            if (existing != null && existing.DayKey == day) return;
            profile.Reload();
            existing = State.Shops.FirstOrDefault(value => value.ShopId == shopId);
            if (existing != null && existing.DayKey == day) return;
            string receipt = "wish:shop:auto:" + profile.Save.Player.CharacterName + ":" + shopId + ":" + day;
            profile.ApplyInventoryTransaction(receipt, save =>
            {
                OfflineWishShopState state = save.Player.Wishes.Shops.FirstOrDefault(value => value.ShopId == shopId);
                if (state == null) save.Player.Wishes.Shops.Add(state = new OfflineWishShopState { ShopId = shopId });
                state.DayKey = day;
                state.ManualRefreshCount = 0;
                state.RandomState = 0;
                GenerateRandomShop(save.Player.Wishes, shop, profession, day, state, localNow);
            });
        }

        private void EnsureFixedShopStocks(WishShop shop)
        {
            if (shop == null || shop.Id != 4) return;
            profile.Reload();
            bool hasMissing = (shop.FixedGoodsList ?? Array.Empty<int>()).Select((goodsId, slot) =>
                new { goodsId, slot }).Any(entry => catalog.ShopGood(entry.goodsId) != null
                    && !State.ShopStocks.Any(value => value.ShopId == shop.Id
                        && value.Slot == entry.slot && value.GoodsId == entry.goodsId));
            if (!hasMissing) return;
            ProgressionResult transaction = profile.ApplyInventoryTransaction("wish:fixed-shop:init:4", save =>
            {
                OfflineWishProgress wish = save.Player.Wishes;
                for (int slot = 0; slot < shop.FixedGoodsList.Length; slot++)
                {
                    int goodsId = shop.FixedGoodsList[slot];
                    WishShopGood good = catalog.ShopGood(goodsId);
                    if (good == null || wish.ShopStocks.Any(value => value.ShopId == shop.Id
                        && value.Slot == slot && value.GoodsId == goodsId)) continue;
                    wish.ShopStocks.Add(new OfflineWishShopStock
                    {
                        ShopId = shop.Id,
                        Slot = slot,
                        GoodsId = goodsId,
                        RemainingInventory = good.Inventory
                    });
                }
            });
            if (!transaction.IsApplied && transaction.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException("固定技能兑换货架初始化失败：" + transaction.Message);
        }

        private WishDrop Roll(int gachaType, int profession, WishRewardRule rule, WishRewardRule sharedRule,
            OfflineWishProgress wish, OfflineWishScheduledGuarantee scheduled, ref long randomState)
        {
            if (gachaType == 1) return RollWeightedRule(rule, wish, profession, ref randomState);
            WishRandomReward forced = null;
            if (scheduled != null)
            {
                if (scheduled.Kind == 0) forced = RewardForQuality(sharedRule, scheduled.GuaranteeId);
                else if (scheduled.Kind == 1) forced = RewardForQuality(sharedRule, scheduled.GuaranteeId);
                else if (scheduled.Kind == 2) forced = SkillReward(sharedRule);
            }
            if (forced == null)
            {
                int[] weights = sharedRule.WeightList;
                int selected = SelectWeighted(weights, ref randomState);
                if (selected < 0 || selected >= sharedRule.IdList.Length)
                    throw new InvalidOperationException("祈愿奖励池没有可抽取的项目");
                forced = catalog.RandomReward(sharedRule.IdList[selected]);
            }
            if (forced == null) throw new InvalidOperationException("祈愿随机奖励包不存在");
            int[] professionFilters = CustomSelectionProfessions(profession);
            List<WishItem> candidates = RandomItems(forced, profession, professionFilters, gachaType == 2);
            if (candidates.Count == 0) throw new InvalidOperationException("当前职业没有符合条件的祈愿奖励");
            WishItem item;
            int candidateIndex;
            int[] explicitWeights = forced.WeightOfIdList ?? Array.Empty<int>();
            if (forced.IdList != null && forced.IdList.Length > 0)
            {
                var explicitCandidates = forced.IdList.Select((id, index) => new { Item = catalog.Item(id), Index = index })
                    .Where(value => value.Item != null && candidates.Contains(value.Item)).ToArray();
                if (explicitCandidates.Length == 0) throw new InvalidOperationException("祈愿奖励包过滤后为空");
                int[] candidateWeights = explicitCandidates.Select(value => explicitWeights.Length > value.Index
                    ? explicitWeights[value.Index] : 100).ToArray();
                int selected = SelectWeighted(candidateWeights, ref randomState);
                item = explicitCandidates[selected].Item;
            }
            else
            {
                candidateIndex = NextInt(ref randomState, candidates.Count);
                item = candidates[candidateIndex];
            }
            int low = Math.Max(1, forced.MinCount);
            int high = Math.Max(low, forced.MaxCount);
            int amount = low == high ? low : low + NextInt(ref randomState, high - low + 1);
            if ((gachaType == 0 || gachaType == 2) && (item.ItemType == 111 || item.ItemType == 112))
                amount = 3;
            int quality = forced.Quality > 0 ? forced.Quality : item.Quality;
            return new WishDrop { Item = item, Count = amount, Quality = quality,
                IsSkill = item.ItemType == 100 || item.ItemType == 101,
                IsPiece = item.ItemType == 111 || item.ItemType == 112 };
        }

        private WishDrop RollWeightedRule(WishRewardRule rule, OfflineWishProgress wish, int profession, ref long randomState)
        {
            int selected = SelectWeighted(rule.WeightList, ref randomState);
            if (selected < 0 || selected >= rule.IdList.Length) throw new InvalidOperationException("友情祈愿奖励池为空");
            int itemId = rule.IdList[selected];
            WishItem item = catalog.Item(itemId);
            if (item == null) throw new InvalidOperationException("友情祈愿奖励物品不存在");
            float baseCount = rule.CountList != null && rule.CountList.Length > selected ? rule.CountList[selected] : 1f;
            double scaledCount = baseCount;
            int levelCountId = rule.LevelCountId != null && rule.LevelCountId.Length > selected
                ? rule.LevelCountId[selected] : 0;
            int subRankCountId = rule.SubRankCountId != null && rule.SubRankCountId.Length > selected
                ? rule.SubRankCountId[selected] : 0;
            scaledCount *= catalog.LevelMultiplier(levelCountId, profile.Save.Player.Level);
            scaledCount *= catalog.LevelMultiplier(subRankCountId, profile.Save.Player.SubRank);
            int count = (int)Math.Truncate(scaledCount);
            if (Math.Abs(baseCount - Math.Truncate(baseCount)) >= 0.00001f)
            {
                double fraction = scaledCount - Math.Truncate(scaledCount);
                if (fraction > 0 && NextRandom(ref randomState) / (double)ulong.MaxValue < fraction) count++;
            }
            if (item.ItemType == 110)
            {
                WishRandomReward random = catalog.RandomReward(itemId);
                if (random == null) throw new InvalidOperationException("友情祈愿随机奖励包不存在");
                List<WishItem> choices = RandomItems(random, 0, Array.Empty<int>());
                if (choices.Count == 0) throw new InvalidOperationException("友情祈愿随机奖励包为空");
                if (random.IdList != null && random.IdList.Length > 0)
                {
                    var weightedChoices = random.IdList.Select((id, index) => new
                    {
                        Item = catalog.Item(id),
                        Weight = random.WeightOfIdList != null && random.WeightOfIdList.Length > index
                            ? random.WeightOfIdList[index] : 100
                    }).Where(value => value.Item != null && choices.Contains(value.Item) && value.Weight > 0).ToArray();
                    if (weightedChoices.Length == 0) throw new InvalidOperationException("友情祈愿随机奖励权重为空");
                    int choice = SelectWeighted(weightedChoices.Select(value => value.Weight).ToArray(), ref randomState);
                    item = weightedChoices[choice].Item;
                }
                else item = choices[NextInt(ref randomState, choices.Count)];
                int low = Math.Max(1, random.MinCount);
                int high = Math.Max(low, random.MaxCount);
                count = checked(count * (low == high ? low : low + NextInt(ref randomState, high - low + 1)));
            }
            return new WishDrop { Item = item, Count = count, Quality = item.Quality,
                IsSkill = item.ItemType == 100 || item.ItemType == 101,
                IsPiece = item.ItemType == 111 || item.ItemType == 112 };
        }

        private WishRandomReward RewardForQuality(WishRewardRule rule, int quality)
        {
            foreach (int id in rule.IdList ?? Array.Empty<int>())
            {
                WishRandomReward reward = catalog.RandomReward(id);
                if (reward != null && reward.Quality == quality) return reward;
            }
            return null;
        }

        private WishRandomReward SkillReward(WishRewardRule rule)
        {
            return RewardForQuality(rule, 4) ?? RewardForQuality(rule, 3) ?? RewardForQuality(rule, 2);
        }

        private List<WishItem> RandomItems(WishRandomReward reward, int profession, int[] customProfessions,
            bool requireExactProfession = false)
        {
            IEnumerable<WishItem> items;
            if (reward.IdList != null && reward.IdList.Length > 0)
            {
                items = reward.IdList.Select(catalog.Item).Where(value => value != null);
            }
            else
            {
                items = catalog.items.Where(item => (reward.ItemTypes ?? Array.Empty<int>()).Contains(item.ItemType)
                    && (reward.Quality <= 0 || item.Quality == reward.Quality)
                    && (reward.Rank < 0 || item.Rank == reward.Rank)
                    && ((reward.ElementTypes ?? Array.Empty<int>()).Length == 0
                        || reward.ElementTypes.Contains(item.ElementType))
                    && !(reward.BlackList ?? Array.Empty<int>()).Contains(item.ClassId));
            }
            return items.Where(item =>
            {
                if (item.ItemType == 100 || item.ItemType == 101)
                {
                    if (requireExactProfession)
                        return catalog.Skill(item.ClassId)?.unlockProfession == profession;
                    if (catalog.SkillAvailable(item.ClassId, profession)) return true;
                    return customProfessions.Any(value => SkillAvailableSingle(item.ClassId, value));
                }
                if (item.ItemType == 111 || item.ItemType == 112)
                {
                    int skillId = item.ClassId - 2000;
                    if (requireExactProfession)
                        return catalog.Skill(skillId)?.unlockProfession == profession;
                    if (catalog.SkillAvailable(skillId, profession)) return true;
                    return customProfessions.Any(value => SkillAvailableSingle(skillId, value));
                }
                return true;
            }).OrderBy(value => value.ClassId).ToList();
        }

        private bool SkillAvailableSingle(int skillId, int profession)
        {
            return catalog.SkillAvailable(skillId, profession);
        }

        private int[] CustomSelectionProfessions(int profession)
        {
            OfflineWishProfessionSelection selection = State.ProfessionSelections.FirstOrDefault(value =>
                value.Profession == profession && value.Completed);
            return selection?.SelectedProfessions?.ToArray() ?? Array.Empty<int>();
        }

        private WishResult EnsureShopDayNoReload(int shopId, int profession, DateTime localNow)
        {
            EnsureShopDay(shopId, profession, localNow);
            return Result(WishResultCode.Applied);
        }

        private void GenerateRandomShop(OfflineWishProgress wish, WishShop shop, int profession, int day,
            OfflineWishShopState state, DateTime localNow)
        {
            wish.ShopStocks.RemoveAll(value => value.ShopId == shop.Id);
            EnsureRandomState(ref state.RandomState,
                new DateTimeOffset(DateTime.SpecifyKind(localNow.Date, DateTimeKind.Local)).ToUnixTimeSeconds(),
                "shop:" + shop.Id + ":" + day);
            int slot = 0;
            for (int groupIndex = 0; groupIndex < (shop.GroupIdList ?? Array.Empty<int>()).Length; groupIndex++)
            {
                int count = shop.GroupRandomCounts != null && shop.GroupRandomCounts.Length > groupIndex
                    ? shop.GroupRandomCounts[groupIndex] : 0;
                for (int itemIndex = 0; itemIndex < count; itemIndex++)
                {
                    WishShopGroup group = ChooseLeafGroup(shop.GroupIdList[groupIndex], profession, ref state.RandomState);
                    if (group == null) continue;
                    WishShopGood good = ChooseShopGood(group, profession, wish, ref state.RandomState);
                    if (good == null) continue;
                    AddShopStock(wish, shop.Id, slot++, good);
                }
            }
        }

        private WishShopGroup ChooseLeafGroup(int groupId, int profession, ref long randomState)
        {
            WishShopGroup group = catalog.ShopGroup(groupId);
            for (int depth = 0; group != null && depth < 16; depth++)
            {
                WishWeightedGroup[] children = (group.ComplexGroupWeights ?? Array.Empty<WishWeightedGroup>())
                    .Where(value => value.Weight > 0).ToArray();
                if (children.Length == 0) return group;
                int selected = SelectWeighted(children.Select(value => value.Weight).ToArray(), ref randomState);
                group = catalog.ShopGroup(children[selected].GroupId);
            }
            return null;
        }

        private WishShopGood ChooseShopGood(WishShopGroup group, int profession, OfflineWishProgress wish, ref long randomState)
        {
            WishShopGood[] goods = (group.ShopGoodsList ?? Array.Empty<int>()).Select(catalog.ShopGood)
                .Where(value => value != null && ShopGoodAvailable(value, profession)).ToArray();
            if (goods.Length == 0) return null;
            int[] weights = goods.Select(value => Math.Max(0, value.Weight)).ToArray();
            int selected = SelectWeighted(weights, ref randomState);
            return selected < 0 ? null : goods[selected];
        }

        private bool ShopGoodAvailable(WishShopGood good, int profession)
        {
            if (good.Condition == null || string.IsNullOrEmpty(good.Condition._type)
                || good.Condition._type == "NoneCondition") return true;
            if (good.Condition._type == "PlayerProfessionFactionCondition")
            {
                return profession > 0 && good.Condition.Profession > 0
                    && catalog.ProfessionChain(profession).Contains(good.Condition.Profession);
            }
            return false;
        }

        private bool QuickBuyConditionMet(WishQuickBuyCondition condition)
        {
            if (condition == null) return true;
            long actual;
            long expected;
            if (condition._type == "LevelCondition")
            {
                actual = profile.Save.Player.Level;
                expected = condition.Level;
            }
            else if (condition._type == "HasItemCondition")
            {
                if (condition.ClassId <= 0) return false;
                actual = profile.InventoryCount(condition.ClassId);
                expected = condition.Count;
            }
            else return false;

            switch (condition.Op)
            {
                case ">=": return actual >= expected;
                case ">": return actual > expected;
                case "==": return actual == expected;
                case "<=": return actual <= expected;
                case "<": return actual < expected;
                default: return false;
            }
        }

        private static void AddShopStock(OfflineWishProgress wish, int shopId, int slot, WishShopGood good)
        {
            wish.ShopStocks.Add(new OfflineWishShopStock
            {
                ShopId = shopId,
                Slot = slot,
                GoodsId = good.Id,
                RemainingInventory = good.Inventory,
                BoughtCount = 0
            });
        }

        private void EnsureGuarantees(OfflineWishProgress wish, int gachaType, int profession, WishRewardRule rule)
        {
            if (gachaType == 1) return;
            long poolSequence = PoolSequence(wish, gachaType);
            wish.ScheduledGuarantees.RemoveAll(value => value.GachaType == gachaType
                && (value.TargetSequence <= poolSequence
                    || value.Kind != 0 && value.Profession != profession));
            var pending = new List<PendingGuarantee>();
            int[] ids = rule.IdList ?? Array.Empty<int>();
            for (int index = 0; index < ids.Length; index++)
            {
                WishRandomReward reward = catalog.RandomReward(ids[index]);
                int quality = reward == null ? 0 : reward.Quality;
                int minimum = rule.IndexMininum != null && rule.IndexMininum.Length > index
                    ? rule.IndexMininum[index] : 0;
                if (quality <= 1 || minimum <= 0) continue;
                int ruleId = SharedRuleId(rule);
                OfflineWishPityCounter counter = FindPity(wish, ruleId, quality);
                pending.Add(new PendingGuarantee(gachaType, 0, quality, profession,
                    Math.Max(1, minimum - counter.MissCount), index));
            }

            if (gachaType == 0 && profession > 0)
            {
                WishSkillGachaSettings settings = catalog.SettingsForProfession(profession);
                OfflineWishProfessionPityCounter counter = FindProfessionPity(wish, gachaType, profession);
                foreach (WishQualityMiss config in settings.EnsureHighRank ?? Array.Empty<WishQualityMiss>())
                {
                    if (config.Quality <= 1 || config.Quality >= counter.QualityMissCounts.Length) continue;
                    pending.Add(new PendingGuarantee(gachaType, 1, config.Quality, profession,
                        Math.Max(1, config.MissCount - counter.QualityMissCounts[config.Quality] + 1), config.Quality));
                }
            }

            var plans = pending.Select(candidate =>
            {
                OfflineWishScheduledGuarantee existing = wish.ScheduledGuarantees.FirstOrDefault(value =>
                    value.GachaType == candidate.GachaType && value.Kind == candidate.Kind
                    && value.GuaranteeId == candidate.Quality
                    && (candidate.Kind == 0 || value.Profession == candidate.Profession)
                    && value.TargetSequence > poolSequence);
                long deadlineSequence = existing != null && existing.DeadlineSequence > poolSequence
                    ? existing.DeadlineSequence : checked(poolSequence + candidate.DueIn);
                return new GuaranteePlan(candidate, existing, deadlineSequence);
            }).ToArray();

            var existingPlans = new HashSet<OfflineWishScheduledGuarantee>(plans
                .Where(value => value.Existing != null).Select(value => value.Existing));
            var occupied = new HashSet<long>(wish.ScheduledGuarantees
                .Where(value => value.GachaType == gachaType && value.TargetSequence > poolSequence
                    && !existingPlans.Contains(value))
                .Select(value => value.TargetSequence));
            foreach (IGrouping<long, GuaranteePlan> deadline in plans.GroupBy(value => value.DeadlineSequence)
                .OrderBy(value => value.Key))
            {
                GuaranteePlan[] collisions = deadline.OrderBy(value => value.Guarantee.Kind)
                    .ThenBy(value => value.Guarantee.ConfigOrder).ThenBy(value => value.Guarantee.Quality).ToArray();
                GuaranteePlan[] unscheduled = collisions.Where(value => value.Existing == null).ToArray();
                var reserved = new HashSet<long>(occupied);
                foreach (GuaranteePlan plan in collisions.Where(value => value.Existing != null))
                    reserved.Add(plan.Existing.TargetSequence);

                long firstSequence = Math.Max(poolSequence + 1, deadline.Key - collisions.Length + 1);
                var targets = new List<long>(unscheduled.Length);
                for (long target = deadline.Key; target >= firstSequence && targets.Count < unscheduled.Length; target--)
                {
                    if (!reserved.Contains(target)) targets.Add(target);
                }

                long adjustedDeadline = deadline.Key;
                long nextSequence = checked(deadline.Key + 1);
                while (targets.Count < unscheduled.Length)
                {
                    if (!reserved.Contains(nextSequence)) targets.Add(nextSequence);
                    nextSequence = checked(nextSequence + 1);
                }
                if (targets.Count > 0) adjustedDeadline = Math.Max(adjustedDeadline, targets.Max());
                targets.Sort();
                for (int index = 0; index < unscheduled.Length; index++)
                {
                    GuaranteePlan plan = unscheduled[index];
                    long target = targets[index];
                    occupied.Add(target);
                    PendingGuarantee guarantee = plan.Guarantee;
                    wish.ScheduledGuarantees.Add(new OfflineWishScheduledGuarantee
                    {
                        TargetSequence = target,
                        DeadlineSequence = adjustedDeadline,
                        GachaType = guarantee.GachaType,
                        Kind = guarantee.Kind,
                        GuaranteeId = guarantee.Quality,
                        Profession = guarantee.Profession
                    });
                }
                if (adjustedDeadline > deadline.Key)
                    foreach (GuaranteePlan plan in collisions.Where(value => value.Existing != null))
                        plan.Existing.DeadlineSequence = adjustedDeadline;
                foreach (GuaranteePlan plan in collisions.Where(value => value.Existing != null))
                    occupied.Add(plan.Existing.TargetSequence);
            }
        }

        private static void RemoveResolvedGuarantees(OfflineWishProgress wish, int gachaType,
            int profession, long sequence, int quality, bool isSkill,
            OfflineWishScheduledGuarantee scheduled)
        {
            var collisionDeadlines = wish.ScheduledGuarantees.Where(value => value.GachaType == gachaType
                    && (value.Kind == 0 || value.Profession == profession))
                .GroupBy(value => value.DeadlineSequence)
                .Where(value => value.Count() > 1).Select(value => value.Key).ToHashSet();
            wish.ScheduledGuarantees.RemoveAll(value =>
            {
                if (scheduled != null && value.GachaType == scheduled.GachaType
                    && value.TargetSequence == sequence && value.Kind == scheduled.Kind
                    && value.GuaranteeId == scheduled.GuaranteeId && value.Profession == scheduled.Profession)
                    return true;
                if (value.GachaType != gachaType || collisionDeadlines.Contains(value.DeadlineSequence)
                    || scheduled != null && value.DeadlineSequence == scheduled.DeadlineSequence) return false;
                return value.Kind == 0 && value.GuaranteeId == quality
                    || value.Kind == 1 && value.Profession == profession
                        && value.GuaranteeId == quality && isSkill
                    || value.Kind == 2 && value.Profession == profession && isSkill;
            });
        }

        private void UpdatePity(OfflineWishProgress wish, int gachaType, int profession, WishRewardRule rule,
            int quality, bool isSkill, bool isPiece)
        {
            int ruleId = SharedRuleId(rule);
            var qualities = new HashSet<int>();
            foreach (int id in rule.IdList ?? Array.Empty<int>())
            {
                WishRandomReward reward = catalog.RandomReward(id);
                if (reward != null && reward.Quality > 1) qualities.Add(reward.Quality);
            }
            foreach (int trackedQuality in qualities)
            {
                OfflineWishPityCounter counter = FindPity(wish, ruleId, trackedQuality);
                if (quality == trackedQuality)
                {
                    counter.MissCount = 0;
                    counter.HitCount = checked(counter.HitCount + 1);
                }
                else counter.MissCount = checked(counter.MissCount + 1);
            }
            if (gachaType == 0 && profession > 0)
            {
                OfflineWishProfessionPityCounter counter = FindProfessionPity(wish, gachaType, profession);
                for (int trackedQuality = 2; trackedQuality < counter.QualityMissCounts.Length; trackedQuality++)
                    counter.QualityMissCounts[trackedQuality] = quality == trackedQuality
                        ? 0 : checked(counter.QualityMissCounts[trackedQuality] + 1);
                counter.SkillMissCount = isSkill ? 0 : checked(counter.SkillMissCount + 1);
                counter.PieceMissCount = isPiece ? checked(counter.PieceMissCount + 1) : 0;
            }
        }

        private OfflineWishAward GrantDrop(OfflinePlayerProgress player, WishDrop drop, long now)
        {
            var award = new OfflineWishAward
            {
                ItemId = drop.Item.ClassId,
                Count = drop.Count,
                Quality = drop.Quality,
                OriginItemId = drop.Item.ClassId
            };
            if (drop.IsSkill)
            {
                OfflineOwnedSkill owned = player.Skills.Skills.FirstOrDefault(value => value.ClassId == drop.Item.ClassId);
                if (owned == null)
                {
                    player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = drop.Item.ClassId, Rank = 1 });
                    return award;
                }
                if (drop.Item.AgioItemId <= 0 || drop.Item.AgioAmount <= 0)
                    throw new InvalidOperationException("重复技能缺少官方碎片转换规则");
                long pieceCount = checked(drop.Count * drop.Item.AgioAmount);
                award.ConvertedItemId = drop.Item.AgioItemId;
                award.ConvertedCount = pieceCount;
                WishSkill definition = catalog.Skill(drop.Item.ClassId);
                if (definition != null && owned.Rank >= definition.maxRank)
                    RecyclePiece(player, drop.Item.AgioItemId, pieceCount, now, award);
                else GrantSkillPiece(player, drop.Item.AgioItemId, pieceCount, now);
                return award;
            }
            if (drop.IsPiece)
            {
                WishSkill skill = catalog.Skill(drop.Item.ClassId - 2000);
                OfflineOwnedSkill owned = skill == null ? null : player.Skills.Skills.FirstOrDefault(value => value.ClassId == skill.id);
                if (skill != null && owned != null && owned.Rank >= skill.maxRank)
                    RecyclePiece(player, drop.Item.ClassId, drop.Count, now, award);
                else GrantSkillPiece(player, drop.Item.ClassId, drop.Count, now);
                return award;
            }
            GrantConfiguredItem(player, drop.Item.ClassId, drop.Count, now);
            return award;
        }

        private OfflineWishAward GrantConfiguredItem(OfflinePlayerProgress player, int itemId, long count, long now)
        {
            WishItem item = catalog.Item(itemId);
            if (item != null && (item.ItemType == 100 || item.ItemType == 101))
            {
                WishDrop drop = new WishDrop { Item = item, Count = count, Quality = item.Quality, IsSkill = true };
                return GrantDrop(player, drop, now);
            }
            if (item != null && (item.ItemType == 111 || item.ItemType == 112))
            {
                WishDrop drop = new WishDrop { Item = item, Count = count, Quality = item.Quality, IsPiece = true };
                return GrantDrop(player, drop, now);
            }
            GrantItem(player, itemId, count, now);
            return new OfflineWishAward
            {
                ItemId = itemId, Count = count, Quality = item?.Quality ?? 0, OriginItemId = itemId
            };
        }

        private void RecyclePiece(OfflinePlayerProgress player, int pieceId, long count, long now, OfflineWishAward award)
        {
            WishItem piece = catalog.Item(pieceId);
            int groupCount = piece == null ? 1 : Math.Max(1, piece.RecycleGroupCount);
            long groups = count / groupCount;
            long leftover = count % groupCount;
            if (leftover > 0) GrantSkillPiece(player, pieceId, leftover, now);
            if (groups <= 0 || piece?.RecycleAwards == null || piece.RecycleAwards.Length == 0)
            {
                if (groups > 0) GrantSkillPiece(player, pieceId, groups * groupCount, now);
                return;
            }
            foreach (WishAmount reward in piece.RecycleAwards)
                GrantItem(player, reward.ItemId, checked(reward.Count * groups), now);
            award.RecycledItemId = piece.RecycleAwards[0].ItemId;
            award.RecycledCount = checked(piece.RecycleAwards[0].Count * groups);
        }

        private static void GrantSkillPiece(OfflinePlayerProgress player, int itemId, long count, long now)
        {
            GrantItem(player, itemId, count, now);
            OfflineSkillItemCount item = player.Skills.Items.FirstOrDefault(value => value.ItemId == itemId);
            if (item == null) player.Skills.Items.Add(item = new OfflineSkillItemCount { ItemId = itemId });
            item.Count = checked(item.Count + count);
        }

        private static void GrantItem(OfflinePlayerProgress player, int itemId, long count, long now)
        {
            if (count <= 0) return;
            InventoryOperationResult result = OfflineInventoryOperations.Grant(player, itemId, count, now);
            if (result.Status != InventoryOperationStatus.Applied)
                throw new InvalidOperationException("无法发放物品 " + itemId + "：" + result.Status);
        }

        private static void Consume(OfflinePlayerProgress player, int itemId, long count, long now)
        {
            if (count <= 0) return;
            InventoryOperationResult result = OfflineInventoryOperations.Consume(player, itemId, count, now);
            if (result.Status != InventoryOperationStatus.Applied)
                throw new InvalidOperationException("物品不足：" + itemId);
            OfflineSkillItemCount skillItem = player.Skills.Items.FirstOrDefault(value => value.ItemId == itemId);
            if (skillItem != null) skillItem.Count = Math.Max(0, skillItem.Count - count);
        }

        private void UpdatePoolAndAchievementProgress(OfflineWishProgress wish, int gachaType,
            int count, long score)
        {
            int countGroup = gachaType == 0 ? 5 : gachaType == 1 ? 18 : 7;
            int scoreGroup = gachaType == 0 ? 6 : gachaType == 1 ? 19 : 8;
            OfflineWishPoolCounter pool = FindOrCreatePool(wish, gachaType);
            pool.Count = checked(pool.Count + count);
            OfflineWishScoreCounter scoreCounter = FindOrCreateScore(wish, gachaType);
            scoreCounter.Count = checked(scoreCounter.Count + score);
            foreach (WishAchievementProgressEntry entry in new[]
            {
                new WishAchievementProgressEntry(countGroup, pool.Count),
                new WishAchievementProgressEntry(scoreGroup, scoreCounter.Count)
            })
            {
                if (entry.Group <= 0) continue;
                foreach (OfflineWishAchievementProgress progress in wish.AchievementProgress.Where(value => value.Group == entry.Group))
                    progress.Progress = entry.Value;
            }
        }

        private List<WishAchievementEntry> BuildAchievements(int group, OfflineWishProgress state = null)
        {
            state = state ?? State;
            WishAchievement[] rows = catalog.achievements.Where(value => value.Group == group).OrderBy(value => value.Id).ToArray();
            if (rows.Length == 0) return new List<WishAchievementEntry>();
            WishAchievement[] independent = rows.Where(value => value.CycleCount == 0 && string.IsNullOrEmpty(value.CycleIncrementParams)).ToArray();
            WishAchievement[] cycle = rows.Where(value => value.CycleCount > 0 || !string.IsNullOrEmpty(value.CycleIncrementParams)).ToArray();
            int anchorIndex = Array.FindIndex(cycle, value => value.CycleCount > 0);
            if (anchorIndex >= 0 && anchorIndex != 0)
            {
                WishAchievement anchor = cycle[anchorIndex];
                cycle = new[] { anchor }.Concat(cycle.Where((_, index) => index != anchorIndex)).ToArray();
            }
            var result = new List<WishAchievementEntry>();
            long currentValue = GroupProgress(group, state);
            int templateCount = independent.Length;
            for (int index = 0; index < independent.Length; index++)
            {
                WishAchievement row = independent[index];
                OfflineWishAchievementProgress saved = state.AchievementProgress.FirstOrDefault(value =>
                    value.Group == group && value.TemplateId == row.Id && value.CycleIndex == 0);
                int target = ParseCount(row.CompleteParams);
                result.Add(new WishAchievementEntry
                {
                    Group = group, Index = index, TemplateId = row.Id, CycleRecordIndex = 0,
                    Target = target, CurrentValue = currentValue, ClaimedCount = saved?.ClaimedCount ?? 0,
                    Rewards = row.Reward ?? Array.Empty<WishAmount>()
                });
            }

            if (cycle.Length > 1)
            {
                WishAchievement anchor = cycle[0];
                int cycleRows = cycle.Length - 1;
                int maxCycleCount = Math.Max(0, anchor.CycleCount);
                int generatedCount = maxCycleCount * cycleRows + 1;
                int total = templateCount + generatedCount;
                int alreadyClaimed = cycle.Sum(row => state.AchievementProgress
                    .Where(value => value.Group == group && value.TemplateId == row.Id && value.CycleIndex == 1)
                    .Sum(value => value.ClaimedCount));
                int firstCycleIndex = Math.Min(alreadyClaimed, generatedCount - 1);
                int last = Math.Min(total, templateCount + firstCycleIndex + Math.Max(10, catalog.settings.AchievementPanelShowCount));
                for (int index = templateCount + firstCycleIndex; index < last; index++)
                {
                    int offset = index - templateCount;
                    WishAchievement template = offset == 0 ? anchor : cycle[(offset - 1) % cycleRows + 1];
                    int target = ParseCount(anchor.CompleteParams) + GenerateCycleValue(offset, cycle);
                    OfflineWishAchievementProgress saved = state.AchievementProgress.FirstOrDefault(value =>
                        value.Group == group && value.TemplateId == template.Id && value.CycleIndex == 1);
                    result.Add(new WishAchievementEntry
                    {
                        Group = group, Index = index, TemplateId = template.Id, CycleRecordIndex = 1,
                        Target = target, CurrentValue = currentValue, ClaimedCount = 0,
                        Rewards = template.Reward ?? Array.Empty<WishAmount>()
                    });
                }
            }
            return result;
        }

        private bool IsFirstUnclaimedCycle(int group, int index, OfflineWishProgress state)
        {
            WishAchievementEntry selected = BuildAchievements(group, state).FirstOrDefault(value => value.Index == index);
            if (selected == null) return false;
            if (selected.CycleRecordIndex == 0) return true;
            WishAchievementEntry first = BuildAchievements(group, state)
                .Where(value => value.CycleRecordIndex > 0 && !value.IsClaimed)
                .OrderBy(value => value.Index).FirstOrDefault();
            return first == null || first.Index == index;
        }

        private long GroupProgress(int group, OfflineWishProgress state)
        {
            int type = group == 5 || group == 6 ? 0 : group == 7 || group == 8 ? 2 : 1;
            bool score = group == 6 || group == 8 || group == 19;
            return score
                ? state.ScoreCounts.FirstOrDefault(value => value.GachaType == type)?.Count ?? 0L
                : state.PoolCounts.FirstOrDefault(value => value.GachaType == type)?.Count ?? 0L;
        }

        private static int GenerateCycleValue(int index, WishAchievement[] cycle)
        {
            if (index <= 0 || cycle.Length <= 1) return 0;
            int[] increments = cycle.Skip(1).Select(value => ParseCount(value.CycleIncrementParams)).ToArray();
            int num = increments.Length;
            int rounds = index / num;
            int remainder = index % num;
            long total = 0;
            for (int value = 0; value < num; value++)
                total += (long)increments[value] * (value < remainder ? rounds + 1 : rounds);
            return (int)Math.Min(int.MaxValue, total);
        }

        private static int ParseCount(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return 0;
            int colon = source.IndexOf(':');
            if (colon < 0) return 0;
            string digits = new string(source.Skip(colon + 1).TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static long GetChestScoreCount(WishChest chest)
        {
            return chest.Rewards?.Sum(value => value.Count) ?? 0L;
        }

        private static int DayKey(DateTime localNow) => localNow.Year * 10000 + localNow.Month * 100 + localNow.Day;
        private static int SharedRuleId(WishRewardRule rule)
        {
            return int.TryParse(rule?.RecordSharedId, out int shared) && shared > 0 ? shared : rule?.Id ?? 0;
        }
        private static OfflineWishDailyCounter FindOrCreateDaily(OfflineWishProgress state, int day, int classType)
        {
            OfflineWishDailyCounter counter = state.DailyCounts.FirstOrDefault(value => value.DayKey == day && value.ClassType == classType);
            if (counter == null) state.DailyCounts.Add(counter = new OfflineWishDailyCounter { DayKey = day, ClassType = classType });
            return counter;
        }
        private static OfflineWishPoolCounter FindOrCreatePool(OfflineWishProgress state, int type)
        {
            OfflineWishPoolCounter counter = state.PoolCounts.FirstOrDefault(value => value.GachaType == type);
            if (counter == null) state.PoolCounts.Add(counter = new OfflineWishPoolCounter { GachaType = type });
            return counter;
        }

        private static long PoolSequence(OfflineWishProgress state, int type)
        {
            return state.PoolCounts.FirstOrDefault(value => value.GachaType == type)?.Count ?? 0;
        }
        private static OfflineWishScoreCounter FindOrCreateScore(OfflineWishProgress state, int type)
        {
            OfflineWishScoreCounter counter = state.ScoreCounts.FirstOrDefault(value => value.GachaType == type);
            if (counter == null) state.ScoreCounts.Add(counter = new OfflineWishScoreCounter { GachaType = type });
            return counter;
        }
        private static int DailyCountIn(OfflineWishProgress state, int day, int classType)
        {
            return state.DailyCounts.FirstOrDefault(value => value.DayKey == day && value.ClassType == classType)?.Count ?? 0;
        }
        private static OfflineWishPityCounter FindPity(OfflineWishProgress state, int ruleId, int quality)
        {
            OfflineWishPityCounter counter = state.PityCounters.FirstOrDefault(value => value.RuleId == ruleId && value.Quality == quality);
            if (counter == null) state.PityCounters.Add(counter = new OfflineWishPityCounter { RuleId = ruleId, Quality = quality });
            return counter;
        }
        private static OfflineWishProfessionPityCounter FindProfessionPity(OfflineWishProgress state, int type, int profession)
        {
            OfflineWishProfessionPityCounter counter = state.ProfessionPityCounters.FirstOrDefault(value =>
                value.GachaType == type && value.Profession == profession);
            if (counter == null) state.ProfessionPityCounters.Add(counter = new OfflineWishProfessionPityCounter
            {
                GachaType = type, Profession = profession, QualityMissCounts = new int[5]
            });
            return counter;
        }
        private static int SelectWeighted(IReadOnlyList<int> weights, ref long state)
        {
            long total = weights.Where(value => value > 0).Sum(value => (long)value);
            if (total <= 0) return -1;
            long roll = NextLong(ref state, total);
            for (int index = 0; index < weights.Count; index++)
            {
                if (weights[index] <= 0) continue;
                if (roll < weights[index]) return index;
                roll -= weights[index];
            }
            return -1;
        }
        private static int NextInt(ref long state, int upperBound)
        {
            if (upperBound <= 0) throw new ArgumentOutOfRangeException(nameof(upperBound));
            return (int)NextLong(ref state, upperBound);
        }
        private static long NextLong(ref long state, long upperBound)
        {
            if (upperBound <= 0) throw new ArgumentOutOfRangeException(nameof(upperBound));
            ulong bound = (ulong)upperBound;
            ulong threshold = unchecked(0UL - bound) % bound;
            ulong value;
            do { value = NextRandom(ref state); } while (value < threshold);
            return (long)(value % bound);
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
        private static void EnsureRandomState(ref long state, long now, string identity)
        {
            if (state != 0) return;
            ulong hash = unchecked((ulong)now + 0x9E3779B97F4A7C15UL);
            foreach (char value in identity ?? string.Empty) hash = unchecked((hash ^ value) * 1099511628211UL);
            if (hash == 0) hash = 0x2545F4914F6CDD1DUL;
            state = unchecked((long)hash);
        }
        private static void TrimHistory(OfflineWishProgress wish, long now)
        {
            long cutoff = Math.Max(0, now - HistoryDays * 86400L);
            wish.History.RemoveAll(value => value.UnixSeconds < cutoff);
            foreach (int type in new[] { 0, 1, 2 })
            {
                OfflineWishDrawRecord[] excess = wish.History.Where(value => value.GachaType == type)
                    .OrderByDescending(value => value.UnixSeconds).ThenByDescending(value => value.DrawIndex)
                    .Skip(HistoryLimitPerPool).ToArray();
                foreach (OfflineWishDrawRecord record in excess) wish.History.Remove(record);
            }
        }
        private static OfflineWishAward CloneAward(OfflineWishAward value)
        {
            return new OfflineWishAward
            {
                ItemId = value.ItemId, Count = value.Count, Quality = value.Quality, OriginItemId = value.OriginItemId,
                ConvertedItemId = value.ConvertedItemId, ConvertedCount = value.ConvertedCount,
                RecycledItemId = value.RecycledItemId, RecycledCount = value.RecycledCount
            };
        }
        private static WishResultCode Map(ProgressionResultStatus status)
        {
            switch (status)
            {
                case ProgressionResultStatus.Applied: return WishResultCode.Applied;
                case ProgressionResultStatus.Unchanged: return WishResultCode.Unchanged;
                case ProgressionResultStatus.NotReady: return WishResultCode.NotReady;
                default: return WishResultCode.InvalidState;
            }
        }
        private static WishResult Result(WishResultCode code, string message = null) => new WishResult(code, message);

        private sealed class WishDrop
        {
            public WishItem Item;
            public long Count;
            public int Quality;
            public bool IsSkill, IsPiece;
        }
        private sealed class PendingGuarantee
        {
            public readonly int GachaType, Kind, Quality, Profession, DueIn, ConfigOrder;
            public PendingGuarantee(int type, int kind, int quality, int profession, int dueIn, int configOrder)
            {
                GachaType = type; Kind = kind; Quality = quality; Profession = profession; DueIn = dueIn; ConfigOrder = configOrder;
            }
        }
        private sealed class GuaranteePlan
        {
            public readonly PendingGuarantee Guarantee;
            public readonly OfflineWishScheduledGuarantee Existing;
            public readonly long DeadlineSequence;
            public GuaranteePlan(PendingGuarantee guarantee, OfflineWishScheduledGuarantee existing, long deadlineSequence)
            {
                Guarantee = guarantee; Existing = existing; DeadlineSequence = deadlineSequence;
            }
        }
        private sealed class WishAchievementProgressEntry
        {
            public readonly int Group;
            public readonly long Value;
            public WishAchievementProgressEntry(int group, long value) { Group = group; Value = value; }
        }
    }

    public sealed class WishAchievementEntry
    {
        public int Group, Index, TemplateId, CycleRecordIndex, Target, ClaimedCount;
        public long CurrentValue;
        public WishAmount[] Rewards;
        public bool IsReady => Target > 0 && CurrentValue >= Target;
        public bool IsClaimed => ClaimedCount > 0;
    }

    public sealed class WishShopStockView
    {
        public int ShopId { get; }
        public int Slot { get; }
        public int GoodsId { get; }
        public int RemainingInventory { get; }
        public int BoughtCount { get; }
        public WishShopStockView(int shopId, int slot, int goodsId, int remainingInventory, int boughtCount)
        {
            ShopId = shopId; Slot = slot; GoodsId = goodsId;
            RemainingInventory = remainingInventory; BoughtCount = boughtCount;
        }
    }

    [Serializable] public sealed class WishQuickBuy
    {
        public int ItemId, CurrencyId, BaseCount;
        public int[] PriceList, PriceBuyCount;
        public WishQuickBuyCondition Condition;
    }
    [Serializable] public sealed class WishQuickBuyCondition
    {
        public string _type, Op;
        public int ClassId, Count, Level;
    }
}
