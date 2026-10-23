using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public enum PetGachaResultCode
    {
        Applied, Unchanged, InvalidArgument, InsufficientItems, DailyLimit,
        ConditionNotMet, NotReady, ConfirmationRequired, InvalidState
    }

    public sealed class PetGachaResult
    {
        public PetGachaResultCode Code { get; }
        public string Message { get; }
        public IReadOnlyList<OfflinePetGachaAward> Awards { get; }
        public long ScoreAdded { get; }
        public bool IsApplied => Code == PetGachaResultCode.Applied;

        internal PetGachaResult(PetGachaResultCode code, string message = null,
            IEnumerable<OfflinePetGachaAward> awards = null, long scoreAdded = 0)
        {
            Code = code;
            Message = message ?? string.Empty;
            Awards = (awards ?? Array.Empty<OfflinePetGachaAward>()).Select(CloneAward).ToArray();
            ScoreAdded = scoreAdded;
        }

        private static OfflinePetGachaAward CloneAward(OfflinePetGachaAward value) => value == null ? null
            : new OfflinePetGachaAward
            {
                ItemId = value.ItemId, Count = value.Count, Quality = value.Quality, IsPet = value.IsPet,
                IsNew = value.IsNew, ConvertedItemId = value.ConvertedItemId, ConvertedCount = value.ConvertedCount
            };
    }

    public sealed class PetGachaAchievementEntry
    {
        public int Group, Index, TemplateId, CycleIndex;
        public long CurrentValue, Target;
        public bool IsClaimed;
        public bool IsReady => CurrentValue >= Target;
        public PetGachaAmount[] Rewards;
    }

    public sealed class PetShopGoodsEntry
    {
        public int GoodsId, ItemId, ItemCount, CurrencyItemId, Price, Remaining;
        public string Name, IconFile;
        public int Quality;
        public bool RequiresConfirmation;
    }

    /// <summary>Applies the pinned pet gacha and pet shop rules to the local character save.</summary>
    public sealed class OfflinePetGachaService
    {
        private const int EpicQuality = 3;
        private const int LegendaryQuality = 4;
        private readonly OfflineMainlineProgressSession profile;
        private readonly PetGachaRuntimeCatalog catalog;

        public OfflinePetGachaService(OfflineMainlineProgressSession profile, PetGachaRuntimeCatalog catalog)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public PetGachaRuntimeCatalog Catalog => catalog;
        public OfflinePetGachaProgress State => profile.Save.Player.PetGacha;

        public int DailyCount(DateTime localNow)
        {
            int day = Math.Max(DayKey(localNow), State.LastObservedLocalDayKey);
            return day > State.LastObservedLocalDayKey ? 0 : State.DailyDrawCount;
        }

        public bool ShouldConfirmHundred(DateTime localNow)
        {
            int day = Math.Max(DayKey(localNow), State.LastObservedLocalDayKey);
            return day > 0 && State.HundredReminderSuppressedDayKey < day;
        }

        public int PityMissCount(int rewardId) =>
            State.PityCounters.FirstOrDefault(value => value.RewardId == rewardId)?.MissCount ?? 0;

        public PetGachaResult Draw(int count, string receiptId, long nowUnixSeconds,
            bool confirmHundred = false, bool suppressHundredReminder = false, bool confirmQuickBuy = false)
        {
            if (string.IsNullOrWhiteSpace(receiptId) || receiptId.Length > 160 || nowUnixSeconds < 0
                || !catalog.Chest.OpenCounts.Contains(count))
                return Result(PetGachaResultCode.InvalidArgument, "抽取次数无效");
            DateTime localNow = LocalDateTime(nowUnixSeconds);
            int day = DayKey(localNow);
            profile.Reload();
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(receiptId))
                return PreviousResult(receiptId);
            if (count == 100 && ShouldConfirmHundred(localNow) && !confirmHundred)
                return Result(PetGachaResultCode.ConfirmationRequired, "百次抽取需要确认");

            int effectiveDay = Math.Max(day, State.LastObservedLocalDayKey);
            int dailyCount = effectiveDay > State.LastObservedLocalDayKey ? 0 : State.DailyDrawCount;
            if ((long)dailyCount + count > catalog.Settings.DailyLimit)
                return Result(PetGachaResultCode.DailyLimit, "今日抽取次数已达上限");

            long requiredMaterial = count;
            long missing = Math.Max(0, requiredMaterial - profile.InventoryCount(catalog.Gacha.CostItemId));
            long quickBuyCost = 0;
            if (missing > 0)
            {
                if (!confirmQuickBuy || profile.Save.Player.Level < 41)
                    return Result(PetGachaResultCode.InsufficientItems, "结缘材料不足");
                quickBuyCost = checked(missing * catalog.Settings.QuickBuyPrice);
                if (profile.InventoryCount(catalog.Settings.QuickBuyCurrencyItemId) < quickBuyCost)
                    return Result(PetGachaResultCode.InsufficientItems, "晨星不足，无法补足结缘材料");
            }

            var awards = new List<OfflinePetGachaAward>(count);
            long scoreAdded = 0;
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflinePlayerProgress player = save.Player;
                OfflinePetGachaProgress state = player.PetGacha;
                int observedDay = Math.Max(day, state.LastObservedLocalDayKey);
                if (observedDay > state.LastObservedLocalDayKey)
                {
                    state.LastObservedLocalDayKey = observedDay;
                    state.DailyDrawCount = 0;
                }
                if ((long)state.DailyDrawCount + count > catalog.Settings.DailyLimit)
                    throw new InvalidOperationException("今日抽取次数已达上限");
                if (missing > 0)
                {
                    if (player.Level < 41 || !confirmQuickBuy)
                        throw new InvalidOperationException("结缘材料不足");
                    Consume(player, catalog.Settings.QuickBuyCurrencyItemId, quickBuyCost);
                    Grant(player, catalog.Settings.QuickBuyItemId, missing);
                }
                Consume(player, catalog.Gacha.CostItemId, requiredMaterial);
                EnsureRandomState(state, nowUnixSeconds, receiptId, save.CreatedAtUnixSeconds);

                for (int index = 0; index < count; index++)
                {
                    PetGachaRewardRule rule = RuleFor(state.TotalDrawCount);
                    PetDrop drop = Roll(rule, state, ref state.RandomState);
                    OfflinePetGachaAward award = GrantDrop(player, state, drop);
                    awards.Add(award);
                    UpdatePity(state, rule, drop.RuleItemId, drop.IsGuaranteed);
                    state.DrawSequence = checked(state.DrawSequence + 1);
                    state.TotalDrawCount = checked(state.TotalDrawCount + 1);
                    state.DailyDrawCount = checked(state.DailyDrawCount + 1);
                    state.Score = checked(state.Score + catalog.Chest.Rewards[0].Count);
                    scoreAdded = checked(scoreAdded + catalog.Chest.Rewards[0].Count);
                    state.History.Add(new OfflinePetGachaDrawRecord
                    {
                        ReceiptId = receiptId, DrawIndex = index, UnixSeconds = nowUnixSeconds,
                        Awards = new List<OfflinePetGachaAward> { CloneAward(award) }
                    });
                }
                if (count == 100 && suppressHundredReminder)
                    state.HundredReminderSuppressedDayKey = Math.Max(state.HundredReminderSuppressedDayKey, observedDay);
                TrimHistory(state, nowUnixSeconds);
            });

            if (transaction.Status == ProgressionResultStatus.Unchanged)
            {
                profile.Reload();
                return PreviousResult(receiptId);
            }
            if (!transaction.IsApplied) return Result(Map(transaction.Status), transaction.Message);
            return new PetGachaResult(PetGachaResultCode.Applied, awards: awards, scoreAdded: scoreAdded);
        }

        public PetGachaResult SuppressHundredReminder(DateTime localNow, string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId) || receiptId.Length > 160 || localNow == default)
                return Result(PetGachaResultCode.InvalidArgument);
            profile.Reload();
            int day = Math.Max(DayKey(localNow), State.LastObservedLocalDayKey);
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
                save.Player.PetGacha.HundredReminderSuppressedDayKey = Math.Max(
                    save.Player.PetGacha.HundredReminderSuppressedDayKey, day));
            return Result(Map(transaction.Status), transaction.Message);
        }

        public IReadOnlyList<OfflinePetGachaDrawRecord> History(long nowUnixSeconds, int page = 0, int pageSize = 10)
        {
            if (nowUnixSeconds < 0 || page < 0 || page > 100 || pageSize <= 0 || pageSize > 100)
                return Array.Empty<OfflinePetGachaDrawRecord>();
            long cutoff = Math.Max(0, nowUnixSeconds - catalog.Settings.HistoryDays * 86400L);
            return State.History.Where(value => value.UnixSeconds >= cutoff)
                .OrderByDescending(value => value.UnixSeconds).ThenByDescending(value => value.DrawIndex)
                .Skip(page * pageSize).Take(pageSize).Select(CloneRecord).ToArray();
        }

        public int AchievementCount(int group)
        {
            if (group != 15 && group != 16) return 0;
            PetGachaAchievement[] rows = catalog.AchievementsFor(group);
            int independent = rows.Count(value => value.CompleteCount > 0 && value.CycleIncrementCount <= 0);
            int cycleItems = rows.Count(value => value.CycleIncrementCount > 0);
            int cycles = rows.Select(value => value.CycleCount).DefaultIfEmpty(0).Max();
            return checked(independent + cycleItems * cycles);
        }

        public IReadOnlyList<PetGachaAchievementEntry> Achievements(int group, int page = 0, int pageSize = 5)
        {
            if ((group != 15 && group != 16) || page < 0 || page > 100000 || pageSize <= 0 || pageSize > 100)
                return Array.Empty<PetGachaAchievementEntry>();
            int start = checked(page * pageSize);
            var result = new List<PetGachaAchievementEntry>(pageSize);
            for (int index = start; index < Math.Min(AchievementCount(group), start + pageSize); index++)
            {
                PetGachaAchievementEntry row = AchievementAt(group, index);
                if (row != null) result.Add(row);
            }
            return result;
        }

        public PetGachaResult ClaimAchievement(int group, int index, string receiptId, long nowUnixSeconds)
        {
            if ((group != 15 && group != 16) || index < 0 || string.IsNullOrWhiteSpace(receiptId)
                || receiptId.Length > 160 || nowUnixSeconds < 0)
                return Result(PetGachaResultCode.InvalidArgument);
            profile.Reload();
            PetGachaAchievementEntry entry = AchievementAt(group, index, State);
            if (entry == null || !entry.IsReady || entry.IsClaimed)
                return Result(PetGachaResultCode.NotReady, "奖励尚未达成或已领取");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                PetGachaAchievementEntry current = AchievementAt(group, index, save.Player.PetGacha);
                if (current == null || !current.IsReady || current.IsClaimed)
                    throw new InvalidOperationException("结缘奖励状态已变化");
                foreach (PetGachaAmount amount in current.Rewards ?? Array.Empty<PetGachaAmount>())
                    Grant(save.Player, amount.ItemId, amount.Count);
                save.Player.PetGacha.AchievementClaims.Add(new OfflinePetGachaAchievementClaim { Group = group, Index = index });
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public PetGachaResult RefreshShop(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) return Result(PetGachaResultCode.InvalidArgument);
            profile.Reload();
            DateTime localNow = LocalDateTime(nowUnixSeconds);
            int week = Math.Max(WeekKey(localNow), State.ShopWeekKey);
            int profession = profile.Save.Player.Wardrobe.Profession;
            if (State.ShopWeekKey == week && State.ShopProfession == profession && State.ShopGoods.Count > 0)
                return Result(PetGachaResultCode.Unchanged);
            int nextSequence = checked(State.ShopRefreshSequence + 1);
            string receipt = "pet-shop:refresh:" + week.ToString(CultureInfo.InvariantCulture) + ":"
                + profession.ToString(CultureInfo.InvariantCulture) + ":" + nextSequence.ToString(CultureInfo.InvariantCulture);
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receipt, save =>
            {
                OfflinePetGachaProgress state = save.Player.PetGacha;
                int effectiveWeek = Math.Max(week, state.ShopWeekKey);
                int effectiveProfession = save.Player.Wardrobe.Profession;
                if (state.ShopWeekKey == effectiveWeek && state.ShopProfession == effectiveProfession
                    && state.ShopGoods.Count > 0) return;
                state.ShopWeekKey = effectiveWeek;
                state.ShopProfession = effectiveProfession;
                state.ShopGoods = SelectShopGoods(effectiveWeek, effectiveProfession);
                state.ShopRefreshSequence = checked(state.ShopRefreshSequence + 1);
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public IReadOnlyList<PetShopGoodsEntry> ShopGoods(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) return Array.Empty<PetShopGoodsEntry>();
            RefreshShop(nowUnixSeconds);
            profile.Reload();
            return State.ShopGoods.Select(stock =>
            {
                PetGachaGoods goods = catalog.Goods(stock.GoodsId);
                PetGachaItem item = goods == null ? null : catalog.Item(goods.ItemId);
                if (goods == null || item == null) return null;
                return new PetShopGoodsEntry
                {
                    GoodsId = goods.Id, ItemId = goods.ItemId, ItemCount = goods.Count,
                    CurrencyItemId = goods.CurrencyItemId, Price = goods.Price, Remaining = stock.Remaining,
                    Name = item.Name, IconFile = item.IconFile, Quality = item.Quality,
                    RequiresConfirmation = goods.PopConfirmDialog
                };
            }).Where(value => value != null).OrderByDescending(value => value.Quality)
                .ThenBy(value => value.GoodsId).ToArray();
        }

        public PetGachaResult BuyShopGoods(int goodsId, int quantity, string receiptId,
            long nowUnixSeconds, bool confirmed)
        {
            if (goodsId <= 0 || quantity <= 0 || quantity > 99 || string.IsNullOrWhiteSpace(receiptId)
                || receiptId.Length > 160 || nowUnixSeconds < 0)
                return Result(PetGachaResultCode.InvalidArgument);
            if (!confirmed) return Result(PetGachaResultCode.ConfirmationRequired, "购买需要确认");
            RefreshShop(nowUnixSeconds);
            profile.Reload();
            PetGachaGoods goods = catalog.Goods(goodsId);
            OfflinePetGachaGoodsStock stock = State.ShopGoods.FirstOrDefault(value => value.GoodsId == goodsId);
            if (goods == null || stock == null || stock.Remaining == 0)
                return Result(PetGachaResultCode.ConditionNotMet, "该商品当前不可购买");
            if (stock.Remaining > 0 && stock.Remaining < quantity)
                return Result(PetGachaResultCode.ConditionNotMet, "商品库存不足");
            long price = checked((long)goods.Price * quantity);
            if (profile.InventoryCount(goods.CurrencyItemId) < price)
                return Result(PetGachaResultCode.InsufficientItems, "兑换货币不足");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflinePetGachaGoodsStock current = save.Player.PetGacha.ShopGoods
                    .FirstOrDefault(value => value.GoodsId == goodsId);
                if (current == null || current.Remaining == 0
                    || current.Remaining > 0 && current.Remaining < quantity)
                    throw new InvalidOperationException("商品库存不足");
                Consume(save.Player, goods.CurrencyItemId, price);
                Grant(save.Player, goods.ItemId, checked((long)goods.Count * quantity));
                if (current.Remaining > 0) current.Remaining -= quantity;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        private PetGachaAchievementEntry AchievementAt(int group, int index) => AchievementAt(group, index, State);

        private PetGachaAchievementEntry AchievementAt(int group, int index, OfflinePetGachaProgress progress)
        {
            if ((group != 15 && group != 16) || index < 0 || index >= AchievementCount(group)) return null;
            PetGachaAchievement[] rows = catalog.AchievementsFor(group);
            PetGachaAchievement[] independent = rows.Where(value => value.CompleteCount > 0
                && value.CycleIncrementCount <= 0).ToArray();
            long current = group == 15 ? progress.TotalDrawCount : progress.Score;
            if (index < independent.Length)
                return AchievementEntry(group, index, independent[index], 0,
                    independent[index].CompleteCount, current, progress);
            int cyclePosition = index - independent.Length;
            PetGachaAchievement[] increments = rows.Where(value => value.CycleIncrementCount > 0).ToArray();
            if (increments.Length == 0) return null;
            PetGachaAchievement anchor = rows.FirstOrDefault(value => value.CycleCount > 0);
            if (anchor == null) return null;
            long target = checked(anchor.CompleteCount + GenerateCycleValue(cyclePosition + 1, increments));
            int stepIndex = cyclePosition % increments.Length;
            int cycleIndex = cyclePosition / increments.Length + 1;
            return AchievementEntry(group, index, increments[stepIndex], cycleIndex, target, current, progress);
        }

        private PetGachaAchievementEntry AchievementEntry(int group, int index,
            PetGachaAchievement row, int cycleIndex, long target, long current, OfflinePetGachaProgress progress)
        {
            bool claimed = progress.AchievementClaims.Any(value => value.Group == group && value.Index == index);
            return new PetGachaAchievementEntry
            {
                Group = group, Index = index, TemplateId = row.Id, CycleIndex = cycleIndex,
                CurrentValue = current, Target = target, IsClaimed = claimed, Rewards = row.Reward
            };
        }

        private static long GenerateCycleValue(int index, PetGachaAchievement[] increments)
        {
            int length = increments.Length;
            int completeCycles = index / length;
            int partialCycle = index % length;
            long total = 0;
            for (int value = 1; value <= length; value++)
                total = checked(total + (long)increments[value - 1].CycleIncrementCount
                    * (value <= partialCycle ? completeCycles + 1 : completeCycles));
            return total;
        }

        private List<OfflinePetGachaGoodsStock> SelectShopGoods(int weekKey, int profession)
        {
            long random = Math.Abs((long)unchecked(weekKey * 397 ^ profession * 7919));
            if (random == 0) random = 1;
            var selected = new List<OfflinePetGachaGoodsStock>();
            foreach (int goodsId in catalog.Shop.FixedGoodsIds ?? Array.Empty<int>()) AddShopStock(selected, goodsId);
            for (int groupIndex = 0; groupIndex < catalog.Shop.GroupIds.Length; groupIndex++)
            {
                int groupId = catalog.Shop.GroupIds[groupIndex];
                PetGachaGoodsGroup group = catalog.Shop.GoodsGroups.FirstOrDefault(value => value.GroupId == groupId);
                if (group == null || group.ConditionType != "NoneCondition") continue;
                int count = catalog.Shop.GroupRandomCounts[groupIndex];
                var candidates = (group.GoodsIds ?? Array.Empty<int>()).Where(id => catalog.Goods(id) != null)
                    .Distinct().ToList();
                int selectedCount = Math.Min(Math.Max(0, count), candidates.Count);
                for (int selection = 0; selection < selectedCount; selection++)
                {
                    int choice = NextInt(ref random, candidates.Count);
                    int goodsId = candidates[choice];
                    candidates.RemoveAt(choice);
                    AddShopStock(selected, goodsId);
                }
            }
            return selected;
        }

        private void AddShopStock(List<OfflinePetGachaGoodsStock> selected, int goodsId)
        {
            PetGachaGoods goods = catalog.Goods(goodsId);
            if (goods == null || selected.Any(value => value.GoodsId == goodsId)) return;
            selected.Add(new OfflinePetGachaGoodsStock { GoodsId = goodsId, Remaining = goods.Inventory });
        }

        private PetGachaRewardRule RuleFor(long totalDraws)
        {
            int[] ids = totalDraws < catalog.Chest.FirstRewardCounts[0]
                ? catalog.Chest.FirstRewardRuleIds : catalog.Chest.RewardRules;
            PetGachaRewardRule rule = ids.Select(catalog.Rule).FirstOrDefault(value => value != null);
            if (rule == null) throw new InvalidOperationException("官方结缘奖池规则缺失");
            return rule;
        }

        private PetDrop Roll(PetGachaRewardRule rule, OfflinePetGachaProgress state, ref long randomState)
        {
            bool isGuaranteed = TryHardGuaranteedOne(rule, state, out int selectedIndex);
            if (!isGuaranteed) selectedIndex = SelectWeighted(rule.Weights, ref randomState);
            int rewardId = rule.ItemIds[selectedIndex];
            PetGachaRandomReward package = catalog.RandomReward(rewardId);
            PetGachaItem item;
            int count;
            if (package == null)
            {
                item = catalog.Item(rewardId);
                count = Math.Max(1, (int)Math.Floor(rule.Counts[selectedIndex]));
            }
            else
            {
                int candidate = SelectWeighted(package.Weights, ref randomState);
                item = catalog.Item(package.ItemIds[candidate]);
                int minimum = Math.Max(1, package.MinCount);
                int maximum = Math.Max(minimum, package.MaxCount);
                count = minimum + (maximum == minimum ? 0 : NextInt(ref randomState, maximum - minimum + 1));
            }
            if (item == null) throw new InvalidOperationException("结缘随机奖励引用了未知道具");
            return new PetDrop
            {
                RuleItemId = rewardId, Item = item, Count = count, Quality = item.Quality,
                IsGuaranteed = isGuaranteed
            };
        }

        private static bool TryHardGuaranteedOne(PetGachaRewardRule rule,
            OfflinePetGachaProgress state, out int selectedIndex)
        {
            int[] candidates = Enumerable.Range(0, rule.ItemIds.Length)
                .Where(index => rule.GuaranteedCounts[index] > 0)
                .OrderByDescending(index => rule.GuaranteedCounts[index])
                .ThenBy(index => rule.Weights[index])
                .ThenBy(index => index)
                .ToArray();
            selectedIndex = -1;
            for (int distance = 1; distance <= candidates.Length; distance++)
            {
                int earliest = int.MaxValue;
                int matching = 0;
                foreach (int index in candidates)
                {
                    int left = rule.GuaranteedCounts[index] - FindPity(state, rule.ItemIds[index]).MissCount;
                    if (left > distance) continue;
                    if (left < earliest)
                    {
                        earliest = left;
                        selectedIndex = index;
                    }
                    matching++;
                    if (matching >= distance) return true;
                }
            }
            selectedIndex = -1;
            return false;
        }

        private static OfflinePetGachaAward GrantDrop(OfflinePlayerProgress player,
            OfflinePetGachaProgress state, PetDrop drop)
        {
            var award = new OfflinePetGachaAward
            { ItemId = drop.Item.ClassId, Count = drop.Count, Quality = drop.Quality, IsPet = drop.Item.ItemType == 400 };
            if (drop.Item.ItemType == 400 && !drop.Item.CanRepeat)
            {
                bool alreadyOwned = state.OwnedPetIds.Contains(drop.Item.ClassId)
                    || InventoryCount(player, drop.Item.ClassId) > 0;
                for (int copy = 0; copy < drop.Count; copy++)
                {
                    if (alreadyOwned)
                    {
                        int shardCount = drop.Item.Quality == EpicQuality ? 30
                            : drop.Item.Quality == LegendaryQuality ? 60 : 0;
                        if (drop.Item.AgioItemId <= 0 || shardCount <= 0)
                            throw new InvalidOperationException("重复幻兽缺少已确认的碎片兑换规则");
                        Grant(player, drop.Item.AgioItemId, shardCount);
                        award.ConvertedItemId = drop.Item.AgioItemId;
                        award.ConvertedCount = checked(award.ConvertedCount + shardCount);
                    }
                    else
                    {
                        Grant(player, drop.Item.ClassId, 1);
                        state.OwnedPetIds.Add(drop.Item.ClassId);
                        award.IsNew = true;
                        alreadyOwned = true;
                    }
                }
                return award;
            }
            Grant(player, drop.Item.ClassId, drop.Count);
            return award;
        }

        private static void UpdatePity(OfflinePetGachaProgress state, PetGachaRewardRule rule,
            int hitId, bool isGuaranteed)
        {
            for (int index = 0; index < rule.ItemIds.Length; index++)
            {
                if (rule.GuaranteedCounts[index] <= 0) continue;
                OfflinePetGachaPityCounter counter = FindPity(state, rule.ItemIds[index]);
                counter.DrawCount = checked(counter.DrawCount + 1);
                if (rule.ItemIds[index] == hitId)
                {
                    counter.HitCount = checked(counter.HitCount + 1);
                    counter.MissCount = Math.Max(counter.MissCount - rule.GuaranteedCounts[index] + 1, 0);
                    if (isGuaranteed)
                        counter.FireGuaranteedCount = checked(counter.FireGuaranteedCount + 1);
                }
                else counter.MissCount = checked(counter.MissCount + 1);
            }
        }

        private static OfflinePetGachaPityCounter FindPity(OfflinePetGachaProgress state, int rewardId)
        {
            OfflinePetGachaPityCounter value = state.PityCounters.FirstOrDefault(item => item.RewardId == rewardId);
            if (value == null) state.PityCounters.Add(value = new OfflinePetGachaPityCounter { RewardId = rewardId });
            return value;
        }

        private static void EnsureRandomState(OfflinePetGachaProgress state, long now, string receipt, long createdAt)
        {
            if (state.RandomState > 0) return;
            ulong hash = 14695981039346656037UL;
            string seed = receipt + ":" + now.ToString(CultureInfo.InvariantCulture) + ":"
                + createdAt.ToString(CultureInfo.InvariantCulture);
            foreach (char character in seed)
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
            state.RandomState = (long)(hash % 2147483646UL) + 1;
        }

        private static int SelectWeighted(int[] weights, ref long randomState)
        {
            long total = 0;
            foreach (int weight in weights) total = checked(total + weight);
            if (total <= 0) throw new InvalidOperationException("官方权重总和无效");
            long target = NextLong(ref randomState) % total;
            for (int index = 0; index < weights.Length; index++)
            {
                if (target < weights[index]) return index;
                target -= weights[index];
            }
            return weights.Length - 1;
        }

        private static int NextInt(ref long randomState, int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return (int)(NextLong(ref randomState) % exclusiveMax);
        }

        private static long NextLong(ref long randomState)
        {
            randomState = randomState * 48271L % 2147483647L;
            if (randomState <= 0) randomState = 1;
            return randomState;
        }

        private int DayKey(DateTime localNow)
        {
            DateTime day = localNow.Date.AddHours(-catalog.Settings.DailyResetHour)
                .AddMinutes(-catalog.Settings.DailyResetMinute);
            return int.Parse(day.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private int WeekKey(DateTime localNow)
        {
            DateTime resetAdjusted = localNow.AddHours(-catalog.Settings.DailyResetHour)
                .AddMinutes(-catalog.Settings.DailyResetMinute);
            int offset = ((int)resetAdjusted.DayOfWeek - catalog.Settings.WeeklyStartDayOfWeek + 7) % 7;
            DateTime start = resetAdjusted.Date.AddDays(-offset);
            return int.Parse(start.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static DateTime LocalDateTime(long unixSeconds) =>
            DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().DateTime;

        private static long InventoryCount(OfflinePlayerProgress player, int itemId) =>
            player.Inventory.FirstOrDefault(value => value.ItemId == itemId)?.Count ?? 0;

        private static void Consume(OfflinePlayerProgress player, int itemId, long count)
        {
            if (count <= 0) throw new InvalidOperationException("消耗数量无效");
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            if (stack == null || stack.Count < count) throw new InvalidOperationException("道具数量不足");
            stack.Count -= count;
            if (stack.Count == 0 && stack.UnitRecords.Count == 0) player.Inventory.Remove(stack);
        }

        private static void Grant(OfflinePlayerProgress player, int itemId, long count)
        {
            if (itemId <= 0 || count <= 0) throw new InvalidOperationException("奖励配置无效");
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            if (stack == null)
            {
                stack = new OfflineItemStack { ItemId = itemId };
                player.Inventory.Add(stack);
            }
            stack.Count = checked(stack.Count + count);
        }

        private void TrimHistory(OfflinePetGachaProgress state, long nowUnixSeconds)
        {
            long cutoff = Math.Max(0, nowUnixSeconds - catalog.Settings.HistoryDays * 86400L);
            state.History.RemoveAll(value => value.UnixSeconds < cutoff);
            foreach (OfflinePetGachaDrawRecord record in state.History.OrderByDescending(value => value.UnixSeconds)
                .ThenByDescending(value => value.DrawIndex).Skip(catalog.Settings.HistoryLimit).ToArray())
                state.History.Remove(record);
        }

        private PetGachaResult PreviousResult(string receiptId)
        {
            OfflinePetGachaAward[] previous = State.History.Where(value => value.ReceiptId == receiptId)
                .OrderBy(value => value.DrawIndex).SelectMany(value => value.Awards).Select(CloneAward).ToArray();
            return new PetGachaResult(PetGachaResultCode.Unchanged, awards: previous);
        }

        private static OfflinePetGachaDrawRecord CloneRecord(OfflinePetGachaDrawRecord value) => new OfflinePetGachaDrawRecord
        {
            ReceiptId = value.ReceiptId, DrawIndex = value.DrawIndex, UnixSeconds = value.UnixSeconds,
            Awards = value.Awards.Select(CloneAward).ToList()
        };

        private static OfflinePetGachaAward CloneAward(OfflinePetGachaAward value) => new OfflinePetGachaAward
        {
            ItemId = value.ItemId, Count = value.Count, Quality = value.Quality, IsPet = value.IsPet,
            IsNew = value.IsNew, ConvertedItemId = value.ConvertedItemId, ConvertedCount = value.ConvertedCount
        };

        private static PetGachaResultCode Map(ProgressionResultStatus status) => status switch
        {
            ProgressionResultStatus.Applied => PetGachaResultCode.Applied,
            ProgressionResultStatus.Unchanged => PetGachaResultCode.Unchanged,
            ProgressionResultStatus.NotReady => PetGachaResultCode.NotReady,
            _ => PetGachaResultCode.InvalidState
        };

        private static PetGachaResult Result(PetGachaResultCode code, string message = null) =>
            new PetGachaResult(code, message);

        private sealed class PetDrop
        {
            public int RuleItemId;
            public PetGachaItem Item;
            public int Count, Quality;
            public bool IsGuaranteed;
        }
    }
}
