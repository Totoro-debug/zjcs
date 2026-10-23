using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public enum TreasureGachaResultCode
    {
        Applied,
        Unchanged,
        InvalidArgument,
        InsufficientItems,
        DailyLimit,
        ConditionNotMet,
        NotReady,
        InvalidState,
        ConfirmationRequired
    }

    public sealed class TreasureGachaResult
    {
        public TreasureGachaResultCode Code { get; }
        public string Message { get; }
        public IReadOnlyList<OfflineTreasureAward> Awards { get; }
        public long ScoreAdded { get; }
        public bool IsApplied => Code == TreasureGachaResultCode.Applied;

        internal TreasureGachaResult(TreasureGachaResultCode code, string message = null,
            IEnumerable<OfflineTreasureAward> awards = null, long scoreAdded = 0)
        {
            Code = code;
            Message = message ?? string.Empty;
            Awards = (awards ?? Array.Empty<OfflineTreasureAward>()).ToArray();
            ScoreAdded = scoreAdded;
        }
    }

    public sealed class TreasureGachaSeriesEntry
    {
        public int SeriesId;
        public string MainTitle;
        public string Subtitle;
        public string EntryBgSpriteFile;
        public string EntryIconSpriteFile;
        public string IconSpriteFile;
        public bool IsOpen;
        public bool IsPreview;
        public bool IsNew;
        public DateTime OpensAt;
        public int CollectionOwned;
        public int CollectionTotal;
    }

    public sealed class TreasureGachaSourceNavigation
    {
        public int SourceItemId { get; internal set; }
        public int ResolvedTreasureItemId { get; internal set; }
        public int SelectedSeriesId { get; internal set; }
        public int SelectedElementGachaType { get; internal set; }
    }

    public sealed class TreasureGachaAchievementEntry
    {
        public int Group;
        public int Index;
        public int TemplateId;
        public int CycleIndex;
        public long CurrentValue;
        public long Target;
        public int ClaimedCount;
        public bool CanClaim;
        public bool IsReady => CurrentValue >= Target;
        public bool IsClaimed => ClaimedCount > 0;
        public IReadOnlyList<TreasureAmount> Rewards;
    }

    public sealed class TreasureGachaProbabilityEntry
    {
        public int ItemId;
        public int Quality;
        public int GroupType;
        public int MinCount;
        public int MaxCount;
        public double Probability;
    }

    /// <summary>Applies the pinned treasure gacha rules to the local character save.</summary>
    public sealed class OfflineTreasureGachaService
    {
        private const int ElementChestCountAchievementType = 8;
        private const int LightElementId = 103;
        private const int PremiumCurrencyItemId = 2;
        private const int QuickBuyItemId = 301;
        private const int QuickBuyCurrencyPerItem = 8;
        private readonly OfflineMainlineProgressSession profile;
        private readonly TreasureGachaRuntimeCatalog catalog;

        public OfflineTreasureGachaService(OfflineMainlineProgressSession profile,
            TreasureGachaRuntimeCatalog catalog)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public TreasureGachaRuntimeCatalog Catalog => catalog;
        public OfflineTreasureGachaProgress State => profile.Save.Player.TreasureGacha;

        public int DailyCount(DateTime localNow)
        {
            OfflineTreasureGachaProgress state = State;
            return DayKey(localNow) > state.LastObservedLocalDayKey ? 0 : state.DailyDrawCount;
        }

        public long PoolCount(int gachaType)
        {
            return State.PoolCounts.FirstOrDefault(value => value.GachaType == gachaType)?.Count ?? 0;
        }

        public int QualityMissCount(int gachaType, int quality)
        {
            TreasureGachaConfig gacha = catalog.Gacha(gachaType);
            TreasureChestConfig chest = gacha == null ? null : catalog.Chest(gacha.ChestId);
            TreasureRewardRule rule = chest == null || chest.RewardRules.Length == 0
                ? null : catalog.Rule(chest.RewardRules[0]);
            int recordId = SharedRecordId(rule);
            return State.PityCounters.FirstOrDefault(value => value.RecordId == recordId
                && value.Quality == quality)?.MissCount ?? 0;
        }

        public int RemainingGuaranteePulls(int gachaType, int quality)
        {
            TreasureGachaConfig gacha = catalog.Gacha(gachaType);
            TreasureChestConfig chest = gacha == null ? null : catalog.Chest(gacha.ChestId);
            TreasureRewardRule rule = chest == null || chest.RewardRules.Length == 0
                ? null : catalog.Rule(chest.RewardRules[0]);
            if (rule == null) return 0;
            int recordId = SharedRecordId(rule);
            int miss = State.PityCounters.FirstOrDefault(value => value.RecordId == recordId
                && value.Quality == quality)?.MissCount ?? 0;
            int threshold = GuaranteeThreshold(rule, quality);
            return threshold <= 0 ? 0 : Math.Max(1, threshold - miss);
        }

        public long PityProbability(int gachaType, int quality, int groupType)
        {
            TreasureGachaPityProbability value = catalog.PityProbabilities.FirstOrDefault(row =>
                row.GachaType == gachaType && row.Quality == quality && row.GroupType == groupType);
            return value == null ? -1 : value.PityProbability;
        }

        public IReadOnlyList<TreasureGachaProbabilityEntry> ProbabilityItems(int gachaType,
            int seriesId, long nowUnixSeconds)
        {
            TreasureGachaConfig gacha = catalog.Gacha(gachaType);
            TreasureChestConfig chest = gacha == null ? null : catalog.Chest(gacha.ChestId);
            if (chest == null || chest.RewardRules == null || chest.RewardRules.Length != 1
                || nowUnixSeconds < 0) return Array.Empty<TreasureGachaProbabilityEntry>();
            TreasureRewardRule rule = catalog.Rule(chest.RewardRules[0]);
            if (rule == null) return Array.Empty<TreasureGachaProbabilityEntry>();
            DateTime localNow = LocalDateTime(nowUnixSeconds);
            profile.Reload();
            TreasureGachaSeries selectedSeries = gachaType <= 7
                ? ResolveSeries(seriesId, profile.Save.Player, localNow, profile.Save.CreatedAtUnixSeconds)
                : null;
            var weighted = new List<(TreasureItemConfig Item, int Min, int Max, int Group, double Weight)>();
            int[] ids = rule.IdList ?? Array.Empty<int>();
            int[] weights = rule.WeightList ?? Array.Empty<int>();
            for (int index = 0; index < ids.Length && index < weights.Length; index++)
            {
                if (weights[index] <= 0) continue;
                TreasureRandomReward reward = catalog.RandomReward(ids[index]);
                int baseCount = rule.CountList != null && rule.CountList.Length > index
                    ? Math.Max(0, (int)Math.Round(rule.CountList[index], MidpointRounding.AwayFromZero)) : 1;
                if (reward == null)
                {
                    TreasureItemConfig item = catalog.Item(ids[index]);
                    if (item == null || !BelongsToSelectedSeries(item, selectedSeries)) continue;
                    AddProbability(weighted, item, baseCount, baseCount, weights[index]);
                    continue;
                }

                var candidates = new List<TreasureItemConfig>();
                var candidateWeights = new List<int>();
                if (reward.IdList != null && reward.IdList.Length > 0)
                {
                    for (int candidateIndex = 0; candidateIndex < reward.IdList.Length; candidateIndex++)
                    {
                        TreasureItemConfig candidate = catalog.Item(reward.IdList[candidateIndex]);
                        if (candidate == null || !BelongsToSelectedSeries(candidate, selectedSeries)
                            || reward.Quality > 0 && candidate.Quality != reward.Quality
                            || reward.ElementTypes != null && reward.ElementTypes.Length > 0
                                && !reward.ElementTypes.Contains(candidate.ElementType)) continue;
                        candidates.Add(candidate);
                        candidateWeights.Add(reward.WeightOfIdList != null
                            && reward.WeightOfIdList.Length > candidateIndex
                            ? Math.Max(0, reward.WeightOfIdList[candidateIndex]) : 100);
                    }
                }
                else
                {
                    candidates = RandomItems(reward, profile.Save.Player, localNow,
                        profile.Save.CreatedAtUnixSeconds, selectedSeries?.ChestGachaId ?? 0);
                    candidateWeights.AddRange(Enumerable.Repeat(100, candidates.Count));
                }

                int minCount = checked(baseCount * Math.Max(1, reward.MinCount));
                int maxCount = checked(baseCount * Math.Max(Math.Max(1, reward.MinCount), reward.MaxCount));
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                    if (candidateWeights[candidateIndex] > 0)
                        AddProbability(weighted, candidates[candidateIndex], minCount, maxCount,
                            (double)weights[index] * candidateWeights[candidateIndex]);
            }

            double totalWeight = weighted.Sum(value => value.Weight);
            if (totalWeight <= 0) return Array.Empty<TreasureGachaProbabilityEntry>();
            return weighted.GroupBy(value => (value.Item.ClassId, value.Min, value.Max, value.Group))
                .Select(group => new TreasureGachaProbabilityEntry
                {
                    ItemId = group.Key.ClassId,
                    Quality = group.First().Item.Quality,
                    GroupType = group.Key.Group,
                    MinCount = group.Key.Min,
                    MaxCount = group.Key.Max,
                    Probability = group.Sum(value => value.Weight) / totalWeight
                })
                .OrderByDescending(value => value.Quality).ThenBy(value => value.GroupType)
                .ThenBy(value => value.ItemId).ToArray();
        }

        private static void AddProbability(
            List<(TreasureItemConfig Item, int Min, int Max, int Group, double Weight)> rows,
            TreasureItemConfig item, int minCount, int maxCount, double weight)
        {
            int group = item.ItemType == 305 ? 6 : item.ItemType == 306 ? 7 : 0;
            rows.Add((item, minCount, maxCount, group, weight));
        }

        public bool ShouldConfirmHundred(int gachaType, DateTime localNow)
        {
            if (catalog.Gacha(gachaType) == null) return false;
            int today = Math.Max(DayKey(localNow), State.LastObservedLocalDayKey);
            OfflineTreasureHundredReminder reminder = State.HundredReminders.FirstOrDefault(value =>
                value.GachaType == gachaType);
            return reminder == null || reminder.DayKey < today;
        }

        public TreasureGachaResult ObserveLocalDay(long nowUnixSeconds, string receiptId)
        {
            if (nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(receiptId))
                return Result(TreasureGachaResultCode.InvalidArgument);
            profile.Reload();
            int day = DayKey(LocalDateTime(nowUnixSeconds));
            if (day <= State.LastObservedLocalDayKey) return Result(TreasureGachaResultCode.Unchanged);
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineTreasureGachaProgress state = save.Player.TreasureGacha;
                if (day > state.LastObservedLocalDayKey)
                {
                    state.LastObservedLocalDayKey = day;
                    state.DailyDrawCount = 0;
                }
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public TreasureGachaResult SelectPreciousType(int gachaType, string receiptId)
        {
            if (gachaType < 4 || gachaType > 7 || catalog.Gacha(gachaType) == null
                || string.IsNullOrWhiteSpace(receiptId)) return Result(TreasureGachaResultCode.InvalidArgument);
            profile.Reload();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
                save.Player.TreasureGacha.SelectedPreciousGachaType = gachaType);
            return Result(Map(transaction.Status), transaction.Message);
        }

        public TreasureGachaResult SelectElementType(int gachaType, string receiptId)
        {
            if (gachaType < 8 || gachaType > 12 || catalog.Gacha(gachaType) == null
                || string.IsNullOrWhiteSpace(receiptId)) return Result(TreasureGachaResultCode.InvalidArgument);
            profile.Reload();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
                save.Player.TreasureGacha.SelectedElementGachaType = gachaType);
            return Result(Map(transaction.Status), transaction.Message);
        }

        public TreasureGachaResult SelectSeries(int seriesId, long nowUnixSeconds, string receiptId)
        {
            if (seriesId <= 0 || nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(receiptId))
                return Result(TreasureGachaResultCode.InvalidArgument);
            profile.Reload();
            TreasureGachaSeries series = catalog.SeriesInfo(seriesId);
            DateTime now = LocalDateTime(nowUnixSeconds);
            if (!catalog.IsSeriesOpen(series, profile.Save.Player, now,
                    profile.Save.CreatedAtUnixSeconds, out _))
                return Result(TreasureGachaResultCode.ConditionNotMet, "该系列尚未开放");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineTreasureGachaProgress state = save.Player.TreasureGacha;
                state.SelectedSeriesId = seriesId;
                if (!state.ConfirmedSeriesIds.Contains(seriesId)) state.ConfirmedSeriesIds.Add(seriesId);
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public TreasureGachaResult SelectSourceSeries(int seriesId, long nowUnixSeconds, string receiptId)
        {
            if (seriesId <= 0 || nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(receiptId))
                return Result(TreasureGachaResultCode.InvalidArgument);
            profile.Reload();
            TreasureGachaSeries series = catalog.SeriesInfo(seriesId);
            DateTime now = LocalDateTime(nowUnixSeconds);
            if (!catalog.IsSeriesOpen(series, profile.Save.Player, now,
                    profile.Save.CreatedAtUnixSeconds, out _))
                return Result(TreasureGachaResultCode.ConditionNotMet, "该系列尚未开放");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
                save.Player.TreasureGacha.SelectedSeriesId = seriesId);
            return Result(Map(transaction.Status), transaction.Message);
        }

        public TreasureGachaSourceNavigation ResolveItemSource(int sourceItemId, long nowUnixSeconds)
        {
            if (sourceItemId <= 0 || nowUnixSeconds < 0) return null;
            profile.Reload();
            TreasureInfoConfig wholeTreasure = catalog.Treasure(sourceItemId);
            TreasureInfoConfig sourcePiece = catalog.Treasures.FirstOrDefault(value =>
                value.TreasurePieceId == sourceItemId);
            int treasureId = sourcePiece?.ClassId ?? (wholeTreasure == null ? 0 : sourceItemId);
            if (treasureId <= 0 || !catalog.SortedSeries().Any(series =>
                    (series.ChestGachaItems ?? Array.Empty<int>()).Contains(treasureId))) return null;
            DateTime localNow = LocalDateTime(nowUnixSeconds);
            TreasureGachaSeries selected = catalog.SortedSeries().FirstOrDefault(series =>
                (series.ChestGachaItems ?? Array.Empty<int>()).Contains(treasureId)
                && catalog.IsSeriesOpen(series, profile.Save.Player, localNow,
                    profile.Save.CreatedAtUnixSeconds, out _));
            return new TreasureGachaSourceNavigation
            {
                SourceItemId = sourceItemId,
                ResolvedTreasureItemId = treasureId,
                SelectedSeriesId = selected?.ChestGachaId ?? 0
            };
        }

        public TreasureGachaSourceNavigation ResolveElementItemSource(int sourceItemId, int sourceElementType,
            long nowUnixSeconds)
        {
            if (sourceItemId <= 0 || sourceElementType < 0 || nowUnixSeconds < 0) return null;
            TreasureItemConfig item = catalog.Item(sourceItemId);
            if (item == null || item.ElementType != sourceElementType) return null;
            if (!IsElementSourceItem(sourceItemId)) return null;
            int elementId = sourceElementType == 0 ? LightElementId : sourceElementType;
            TreasureElementGachaMapping mapping = catalog.Settings.ElementGachaMappings.FirstOrDefault(value =>
                value != null && value.ElementId == elementId && value.GachaType >= 8
                && value.GachaType <= 12);
            TreasureGachaConfig gacha = mapping == null ? null : catalog.Gacha(mapping.GachaType);
            if (gacha == null || gacha.CountAchievementType != ElementChestCountAchievementType) return null;
            return new TreasureGachaSourceNavigation
            {
                SourceItemId = sourceItemId,
                ResolvedTreasureItemId = sourceItemId,
                SelectedElementGachaType = gacha.GachaType
            };
        }

        private bool IsElementSourceItem(int sourceItemId)
        {
            foreach (TreasureGachaConfig gacha in catalog.Gachas.Where(value =>
                         value.CountAchievementType == ElementChestCountAchievementType))
            {
                TreasureChestConfig chest = catalog.Chest(gacha.ChestId);
                TreasureRewardRule rule = chest == null || chest.RewardRules == null || chest.RewardRules.Length == 0
                    ? null : catalog.Rule(chest.RewardRules[0]);
                if (rule == null) continue;
                int[] rewardIds = rule.IdList ?? Array.Empty<int>();
                int[] weights = rule.WeightList ?? Array.Empty<int>();
                for (int index = 0; index < rewardIds.Length && index < weights.Length; index++)
                {
                    if (weights[index] <= 0) continue;
                    TreasureRandomReward reward = catalog.RandomReward(rewardIds[index]);
                    if (reward == null)
                    {
                        if (rewardIds[index] == sourceItemId) return true;
                        continue;
                    }
                    if (reward.IdList != null && reward.IdList.Length > 0)
                    {
                        for (int candidateIndex = 0; candidateIndex < reward.IdList.Length; candidateIndex++)
                        {
                            TreasureItemConfig candidate = catalog.Item(reward.IdList[candidateIndex]);
                            int candidateWeight = reward.WeightOfIdList != null
                                && reward.WeightOfIdList.Length > candidateIndex
                                ? reward.WeightOfIdList[candidateIndex] : 100;
                            if (candidateWeight > 0 && candidate != null
                                && candidate.ClassId == sourceItemId
                                && (reward.Quality <= 0 || candidate.Quality == reward.Quality)
                                && (reward.ElementTypes == null || reward.ElementTypes.Length == 0
                                    || reward.ElementTypes.Contains(candidate.ElementType))) return true;
                        }
                        continue;
                    }

                    var candidates = new Dictionary<int, TreasureItemConfig>();
                    IEnumerable<TreasureGachaSeries> seriesRows = catalog.SortedSeries();
                    if (reward.MapClassId != -1)
                        seriesRows = seriesRows.Where(value => value.MapClassId == reward.MapClassId);
                    foreach (TreasureGachaSeries series in seriesRows)
                        AddSeriesCandidates(series, reward, candidates);
                    if (candidates.Count == 0)
                        foreach (int fallbackId in reward.DefaultGachaIds ?? Array.Empty<int>())
                        {
                            TreasureGachaSeries fallback = catalog.SeriesInfo(fallbackId);
                            if (fallback != null) AddSeriesCandidates(fallback, reward, candidates);
                        }
                    if (candidates.ContainsKey(sourceItemId)) return true;
                }
            }
            return false;
        }

        public TreasureGachaResult ConfirmOpenSeries(int selectedSeriesId, long nowUnixSeconds,
            string receiptId)
        {
            if (selectedSeriesId < 0 || nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(receiptId))
                return Result(TreasureGachaResultCode.InvalidArgument);
            profile.Reload();
            DateTime now = LocalDateTime(nowUnixSeconds);
            TreasureGachaSeries selected = selectedSeriesId > 0
                ? catalog.SeriesInfo(selectedSeriesId)
                : ResolveSeries(0, profile.Save.Player, now, profile.Save.CreatedAtUnixSeconds);
            if (selectedSeriesId > 0 && selected == null)
                return Result(TreasureGachaResultCode.InvalidArgument);
            if (selected != null && !catalog.IsSeriesOpen(selected, profile.Save.Player, now,
                    profile.Save.CreatedAtUnixSeconds, out _))
                return Result(TreasureGachaResultCode.ConditionNotMet, "该系列尚未开放");
            int[] openSeriesIds = catalog.SortedSeries().Where(series =>
                catalog.IsSeriesOpen(series, profile.Save.Player, now,
                    profile.Save.CreatedAtUnixSeconds, out _))
                .Select(series => series.ChestGachaId).ToArray();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineTreasureGachaProgress state = save.Player.TreasureGacha;
                state.SelectedSeriesId = selected?.ChestGachaId ?? 0;
                foreach (int seriesId in openSeriesIds)
                    if (!state.ConfirmedSeriesIds.Contains(seriesId)) state.ConfirmedSeriesIds.Add(seriesId);
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public IReadOnlyList<TreasureGachaSeriesEntry> SeriesEntries(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) return Array.Empty<TreasureGachaSeriesEntry>();
            profile.Reload();
            DateTime now = LocalDateTime(nowUnixSeconds);
            return catalog.SortedSeries().Select(series =>
            {
                bool isOpen = catalog.IsSeriesOpen(series, profile.Save.Player, now,
                    profile.Save.CreatedAtUnixSeconds, out _);
                bool isPreview = catalog.IsSeriesPreview(series, profile.Save.Player, now,
                    profile.Save.CreatedAtUnixSeconds, out DateTime opensAt);
                int[] items = series.ChestGachaItems ?? Array.Empty<int>();
                int owned = items.Count(itemId => profile.InventoryCount(itemId) > 0);
                return new TreasureGachaSeriesEntry
                {
                    SeriesId = series.ChestGachaId,
                    MainTitle = series.MainTitle,
                    Subtitle = series.Subtitle,
                    EntryBgSpriteFile = series.EntryBgSpriteFile,
                    EntryIconSpriteFile = series.EntryIconSpriteFile,
                    IconSpriteFile = series.IconSpriteFile,
                    IsOpen = isOpen,
                    IsPreview = isPreview,
                    IsNew = isOpen && !State.ConfirmedSeriesIds.Contains(series.ChestGachaId),
                    OpensAt = isPreview ? opensAt : default,
                    CollectionOwned = owned,
                    CollectionTotal = items.Length
                };
            }).ToArray();
        }

        public TreasureGachaResult Draw(int gachaType, int count, int seriesId, string receiptId,
            long nowUnixSeconds, bool confirmHundred = false, bool confirmQuickBuy = false,
            bool suppressHundredReminder = false)
        {
            TreasureGachaConfig gacha = catalog.Gacha(gachaType);
            TreasureChestConfig chest = gacha == null ? null : catalog.Chest(gacha.ChestId);
            if (gacha == null || chest == null || string.IsNullOrWhiteSpace(receiptId)
                || receiptId.Length > 160 || nowUnixSeconds < 0 || !chest.OpenCounts.Contains(count))
                return Result(TreasureGachaResultCode.InvalidArgument, "抽取次数或类型无效");

            DateTime localNow = LocalDateTime(nowUnixSeconds);
            profile.Reload();
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(receiptId))
            {
                IReadOnlyList<OfflineTreasureAward> previous = State.History.Where(value => value.ReceiptId == receiptId)
                    .OrderBy(value => value.DrawIndex).SelectMany(value => value.Awards)
                    .Select(CloneAward).ToArray();
                return new TreasureGachaResult(TreasureGachaResultCode.Unchanged,
                    awards: previous);
            }
            if (count == 100 && ShouldConfirmHundred(gachaType, localNow) && !confirmHundred)
                return Result(TreasureGachaResultCode.ConfirmationRequired, "百次抽取需要确认");

            ObserveLocalDay(nowUnixSeconds, "treasure-gacha:observe:" + DayKey(localNow));
            profile.Reload();
            OfflineMainlineSave current = profile.Save;
            TreasureGachaSeries selectedSeries = gachaType <= 7
                ? ResolveSeries(seriesId, current.Player, localNow, current.CreatedAtUnixSeconds)
                : null;
            if (gachaType <= 7 && selectedSeries == null)
                return Result(TreasureGachaResultCode.ConditionNotMet, "请先选择已开放的系列");
            if (gachaType >= 8 && AvailableElementCandidates(gachaType, current.Player, localNow,
                    current.CreatedAtUnixSeconds).Count == 0)
                return Result(TreasureGachaResultCode.ConditionNotMet, "当前元素没有已开放的奖池");

            int day = Math.Max(DayKey(localNow), current.Player.TreasureGacha.LastObservedLocalDayKey);
            int daily = current.Player.TreasureGacha.LastObservedLocalDayKey < day
                ? 0 : current.Player.TreasureGacha.DailyDrawCount;
            if (catalog.Settings.DailyLimit > 0 && daily + count > catalog.Settings.DailyLimit)
                return Result(TreasureGachaResultCode.DailyLimit, "今日抽取次数已达上限");

            long requiredCurrency = 0;
            foreach (TreasureAmount cost in chest.CostItems)
            {
                long required = checked(cost.Count * count);
                long available = profile.InventoryCount(cost.ItemId);
                if (available >= required) continue;
                long missing = required - available;
                if (!confirmQuickBuy || gachaType != 4 || cost.ItemId != QuickBuyItemId)
                    return Result(TreasureGachaResultCode.InsufficientItems, "抽取材料不足");
                requiredCurrency = checked(requiredCurrency + missing * QuickBuyCurrencyPerItem);
            }
            if (requiredCurrency > profile.InventoryCount(PremiumCurrencyItemId))
                return Result(TreasureGachaResultCode.InsufficientItems, "晨星不足，无法补足女神牌");

            var awards = new List<OfflineTreasureAward>(count);
            long scoreAdded = 0;
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflinePlayerProgress player = save.Player;
                OfflineTreasureGachaProgress state = player.TreasureGacha;
                int observedDay = Math.Max(day, state.LastObservedLocalDayKey);
                if (observedDay > state.LastObservedLocalDayKey)
                {
                    state.LastObservedLocalDayKey = observedDay;
                    state.DailyDrawCount = 0;
                }
                if (catalog.Settings.DailyLimit > 0
                    && (long)state.DailyDrawCount + count > catalog.Settings.DailyLimit)
                    throw new InvalidOperationException("今日抽取次数已达上限");

                foreach (TreasureAmount cost in chest.CostItems)
                {
                    long required = checked(cost.Count * count);
                    long available = InventoryCount(player, cost.ItemId);
                    if (available < required)
                    {
                        long missing = required - available;
                        if (!confirmQuickBuy || gachaType != 4 || cost.ItemId != QuickBuyItemId)
                            throw new InvalidOperationException("抽取材料不足");
                        Consume(player, PremiumCurrencyItemId,
                            checked(missing * QuickBuyCurrencyPerItem), nowUnixSeconds);
                        Grant(player, QuickBuyItemId, missing, nowUnixSeconds);
                    }
                    Consume(player, cost.ItemId, required, nowUnixSeconds);
                }

                EnsureRandomState(state, nowUnixSeconds, receiptId);
                int recordSeries = selectedSeries?.ChestGachaId ?? 0;
                for (int index = 0; index < count; index++)
                {
                    long poolCount = PoolCountIn(state, gachaType);
                    TreasureRewardRule rule = RuleFor(chest, poolCount);
                    if (rule == null) throw new InvalidOperationException("官方抽取规则缺失");
                    TreasureRewardRule pityRule = PityRuleFor(chest, poolCount);
                    if (pityRule == null || SharedRecordId(pityRule) != SharedRecordId(rule))
                        throw new InvalidOperationException("首次奖励规则与正式保底记录不匹配");
                    int recordId = SharedRecordId(pityRule);
                    EnsureGuarantees(state, pityRule);
                    long nextPoolPull = PityDrawCount(state, recordId) + 1;
                    OfflineTreasureScheduledGuarantee due = state.ScheduledGuarantees.FirstOrDefault(value =>
                        value.RecordId == recordId && value.TargetPull == nextPoolPull);
                    TreasureDrop drop = Roll(rule, due?.Quality ?? 0, selectedSeries, player, localNow,
                        save.CreatedAtUnixSeconds, ref state.RandomState);
                    OfflineTreasureAward award = GrantDrop(player, drop, nowUnixSeconds);
                    awards.Add(award);

                    long score = chest.Rewards?.FirstOrDefault(value =>
                        value.ItemId == gacha.RewardScoreId)?.Count ?? 0;
                    if (score > 0)
                    {
                        Grant(player, gacha.RewardScoreId, score, nowUnixSeconds);
                        scoreAdded = checked(scoreAdded + score);
                        state.PreciousScore = checked(state.PreciousScore + score);
                    }
                    UpdatePity(state, pityRule, recordId, drop.Quality);
                    state.DrawSequence = checked(state.DrawSequence + 1);
                    state.DailyDrawCount = checked(state.DailyDrawCount + 1);
                    FindOrCreatePool(state, gachaType).Count = checked(PoolCountIn(state, gachaType) + 1);
                    if (gachaType <= 7) state.PreciousDrawCount = checked(state.PreciousDrawCount + 1);
                    else state.ElementDrawCount = checked(state.ElementDrawCount + 1);
                    state.ScheduledGuarantees.RemoveAll(value => value.RecordId == recordId
                        && value.TargetPull <= nextPoolPull);
                    EnsureGuarantees(state, PityRuleFor(chest, PoolCountIn(state, gachaType)));
                    state.History.Add(new OfflineTreasureDrawRecord
                    {
                        ReceiptId = receiptId,
                        DrawIndex = index,
                        GachaType = gachaType,
                        SeriesId = recordSeries,
                        UnixSeconds = nowUnixSeconds,
                        Awards = new List<OfflineTreasureAward> { CloneAward(award) }
                    });
                }
                if (count == 100 && suppressHundredReminder)
                    SetReminder(state, gachaType, day, true);
                TrimHistory(state, nowUnixSeconds, catalog.Settings.HistoryDays,
                    catalog.Settings.HistoryLimit);
            });

            if (transaction.Status == ProgressionResultStatus.Unchanged)
            {
                profile.Reload();
                IReadOnlyList<OfflineTreasureAward> previous = State.History.Where(value => value.ReceiptId == receiptId)
                    .OrderBy(value => value.DrawIndex).SelectMany(value => value.Awards)
                    .Select(CloneAward).ToArray();
                return new TreasureGachaResult(TreasureGachaResultCode.Unchanged,
                    transaction.Message, previous);
            }
            if (!transaction.IsApplied) return Result(Map(transaction.Status), transaction.Message);
            return new TreasureGachaResult(TreasureGachaResultCode.Applied, awards: awards,
                scoreAdded: scoreAdded);
        }

        public TreasureGachaResult SetHundredReminder(int gachaType, DateTime localNow, bool suppress,
            string receiptId)
        {
            if (catalog.Gacha(gachaType) == null || string.IsNullOrWhiteSpace(receiptId))
                return Result(TreasureGachaResultCode.InvalidArgument);
            profile.Reload();
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
                SetReminder(save.Player.TreasureGacha, gachaType,
                    Math.Max(DayKey(localNow), save.Player.TreasureGacha.LastObservedLocalDayKey), suppress));
            return Result(Map(transaction.Status), transaction.Message);
        }

        public IReadOnlyList<OfflineTreasureDrawRecord> History(int gachaType, long nowUnixSeconds,
            int page = 0, int pageSize = 10)
        {
            if (gachaType < 4 || gachaType > 12 || nowUnixSeconds < 0 || page < 0
                || page > 100 || pageSize <= 0 || pageSize > 100)
                return Array.Empty<OfflineTreasureDrawRecord>();
            long cutoff = Math.Max(0, nowUnixSeconds - catalog.Settings.HistoryDays * 86400L);
            IEnumerable<OfflineTreasureDrawRecord> rows = State.History.Where(value => value.UnixSeconds >= cutoff);
            if (gachaType <= 7) rows = rows.Where(value => value.GachaType == gachaType);
            else rows = rows.Where(value => value.GachaType >= 8 && value.GachaType <= 12);
            return rows.OrderByDescending(value => value.UnixSeconds).ThenByDescending(value => value.DrawIndex)
                .Skip(page * pageSize).Take(pageSize).Select(CloneRecord).ToArray();
        }

        public IReadOnlyList<TreasureGachaAchievementEntry> Achievements(int group, int page = 0,
            int pageSize = 5)
        {
            if (group < 11 || group > 13 || page < 0 || pageSize <= 0 || pageSize > 100)
                return Array.Empty<TreasureGachaAchievementEntry>();
            return AchievementPage(group, page, pageSize, State);
        }

        public TreasureGachaResult ClaimAchievement(int group, int index, string receiptId,
            long nowUnixSeconds)
        {
            if (group < 11 || group > 13 || index < 0 || string.IsNullOrWhiteSpace(receiptId)
                || nowUnixSeconds < 0) return Result(TreasureGachaResultCode.InvalidArgument);
            TreasureGachaAchievementEntry entry = AchievementAt(group, index, State);
            if (entry == null || !entry.IsReady || entry.IsClaimed || !IsFirstUnclaimed(group, index, State))
                return Result(TreasureGachaResultCode.NotReady, "奖励尚未达成或已领取");
            profile.Reload();
            entry = AchievementAt(group, index, State);
            if (entry == null || !entry.IsReady || entry.IsClaimed || !IsFirstUnclaimed(group, index, State))
                return Result(TreasureGachaResultCode.NotReady, "奖励状态已变化");
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                TreasureGachaAchievementEntry current = AchievementAt(group, index,
                    save.Player.TreasureGacha);
                if (current == null || !current.IsReady || current.IsClaimed
                    || !IsFirstUnclaimed(group, index, save.Player.TreasureGacha))
                    throw new InvalidOperationException("奖励状态已变化");
                foreach (TreasureAmount reward in current.Rewards)
                    Grant(save.Player, reward.ItemId, reward.Count, nowUnixSeconds);
                OfflineTreasureAchievementProgress progress = save.Player.TreasureGacha.AchievementProgress
                    .FirstOrDefault(value => value.Group == group && value.TemplateId == current.TemplateId
                        && value.CycleIndex == current.CycleIndex);
                if (progress == null)
                    save.Player.TreasureGacha.AchievementProgress.Add(progress =
                        new OfflineTreasureAchievementProgress
                        { Group = group, TemplateId = current.TemplateId, CycleIndex = current.CycleIndex });
                progress.ClaimedCount = 1;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        private TreasureGachaSeries ResolveSeries(int seriesId, OfflinePlayerProgress player,
            DateTime localNow, long createdAt)
        {
            if (seriesId > 0)
            {
                TreasureGachaSeries row = catalog.SeriesInfo(seriesId);
                return catalog.IsSeriesOpen(row, player, localNow, createdAt, out _) ? row : null;
            }
            TreasureGachaSeries[] open = catalog.SortedSeries().Where(row =>
                catalog.IsSeriesOpen(row, player, localNow, createdAt, out _)).ToArray();
            TreasureGachaSeries newSeries = open.LastOrDefault(row =>
                !State.ConfirmedSeriesIds.Contains(row.ChestGachaId));
            if (newSeries != null) return newSeries;
            TreasureGachaSeries selected = open.FirstOrDefault(row => row.ChestGachaId == State.SelectedSeriesId);
            return selected ?? open.LastOrDefault();
        }

        private List<TreasureItemConfig> AvailableElementCandidates(int gachaType,
            OfflinePlayerProgress player, DateTime localNow, long createdAt)
        {
            TreasureChestConfig chest = catalog.Chest(catalog.Gacha(gachaType).ChestId);
            TreasureRewardRule rule = RuleFor(chest, PoolCount(gachaType));
            if (rule == null) return new List<TreasureItemConfig>();
            TreasureRandomReward package = rule.IdList.Select(catalog.RandomReward)
                .FirstOrDefault(value => value != null && value.Quality > 1);
            return package == null ? new List<TreasureItemConfig>()
                : RandomItems(package, player, localNow, createdAt);
        }

        private TreasureRewardRule RuleFor(TreasureChestConfig chest, long openCount)
        {
            if (chest == null) return null;
            if (openCount == 0 && chest.FirstRewardRuleIds != null && chest.FirstRewardRuleIds.Length > 0)
                return chest.FirstRewardRuleIds.Select(catalog.Rule).FirstOrDefault(value => value != null);
            return chest.RewardRules.Select(catalog.Rule).FirstOrDefault(value => value != null);
        }

        private TreasureRewardRule PityRuleFor(TreasureChestConfig chest, long openCount)
        {
            TreasureRewardRule active = RuleFor(chest, openCount);
            if (active?.IndexMininum != null && active.IndexMininum.Any(value => value > 0)) return active;
            return chest?.RewardRules?.Select(catalog.Rule).FirstOrDefault(value =>
                value?.IndexMininum != null && value.IndexMininum.Any(threshold => threshold > 0)) ?? active;
        }

        private TreasureDrop Roll(TreasureRewardRule rule, int forcedQuality, TreasureGachaSeries selectedSeries,
            OfflinePlayerProgress player, DateTime localNow, long createdAt, ref long randomState)
        {
            int[] ids = rule.IdList ?? Array.Empty<int>();
            int selected = -1;
            if (forcedQuality > 0)
            {
                for (int index = 0; index < ids.Length; index++)
                {
                    TreasureRandomReward package = catalog.RandomReward(ids[index]);
                    if (package != null && package.Quality == forcedQuality) { selected = index; break; }
                }
                if (selected < 0) throw new InvalidOperationException("保底品质没有对应的官方奖励包");
            }
            else selected = SelectWeighted(rule.WeightList, ref randomState);
            if (selected < 0 || selected >= ids.Length)
                throw new InvalidOperationException("官方宝具奖励池为空");
            TreasureRandomReward reward = catalog.RandomReward(ids[selected]);
            if (reward == null) throw new InvalidOperationException("官方宝具随机奖励包缺失");

            TreasureItemConfig item;
            if (reward.IdList != null && reward.IdList.Length > 0)
            {
                var candidates = new List<TreasureItemConfig>();
                var candidateWeights = new List<int>();
                for (int index = 0; index < reward.IdList.Length; index++)
                {
                    TreasureItemConfig candidate = catalog.Item(reward.IdList[index]);
                    if (candidate == null || !BelongsToSelectedSeries(candidate, selectedSeries)
                        || reward.Quality > 0 && candidate.Quality != reward.Quality
                        || reward.ElementTypes != null && reward.ElementTypes.Length > 0
                            && !reward.ElementTypes.Contains(candidate.ElementType)) continue;
                    candidates.Add(candidate);
                    candidateWeights.Add(reward.WeightOfIdList != null
                        && reward.WeightOfIdList.Length > index ? reward.WeightOfIdList[index] : 100);
                }
                if (candidates.Count == 0) throw new InvalidOperationException("官方宝具奖励包过滤后为空");
                item = candidates[SelectWeighted(candidateWeights.ToArray(), ref randomState)];
            }
            else
            {
                List<TreasureItemConfig> candidates = RandomItems(reward, player, localNow, createdAt,
                    selectedSeries?.ChestGachaId ?? 0);
                if (candidates.Count == 0) throw new InvalidOperationException("当前开放系列没有符合条件的宝具");
                item = candidates[NextInt(ref randomState, candidates.Count)];
            }
            int low = Math.Max(1, reward.MinCount);
            int high = Math.Max(low, reward.MaxCount);
            int count = low == high ? low : low + NextInt(ref randomState, high - low + 1);
            return new TreasureDrop
            {
                Item = item,
                Count = count,
                Quality = reward.Quality > 0 ? reward.Quality : item.Quality,
                IsPiece = reward.IsPiece || item.ItemType == 306
            };
        }

        private List<TreasureItemConfig> RandomItems(TreasureRandomReward reward,
            OfflinePlayerProgress player, DateTime localNow, long createdAt, int selectedSeriesId = 0)
        {
            var selected = new Dictionary<int, TreasureItemConfig>();
            IEnumerable<TreasureGachaSeries> seriesRows = catalog.SortedSeries();
            if (selectedSeriesId > 0)
                seriesRows = seriesRows.Where(value => value.ChestGachaId == selectedSeriesId);
            if (reward.MapClassId != -1)
                seriesRows = seriesRows.Where(value => value.MapClassId == reward.MapClassId);
            foreach (TreasureGachaSeries series in seriesRows)
            {
                if (reward.IsNeedMet && !catalog.IsSeriesOpen(series, player, localNow, createdAt, out _))
                    continue;
                AddSeriesCandidates(series, reward, selected);
            }
            if (selected.Count == 0)
            {
                foreach (int defaultId in reward.DefaultGachaIds ?? Array.Empty<int>())
                {
                    TreasureGachaSeries fallback = catalog.SeriesInfo(defaultId);
                    if (fallback != null) AddSeriesCandidates(fallback, reward, selected);
                }
            }
            if (selectedSeriesId > 0)
            {
                TreasureGachaSeries selectedSeries = catalog.SeriesInfo(selectedSeriesId);
                foreach (int itemId in selected.Where(value =>
                    !BelongsToSelectedSeries(value.Value, selectedSeries)).Select(value => value.Key).ToArray())
                    selected.Remove(itemId);
            }
            return selected.Values.OrderBy(value => value.ClassId).ToList();
        }

        private bool BelongsToSelectedSeries(TreasureItemConfig item, TreasureGachaSeries selectedSeries)
        {
            if (item == null || selectedSeries == null) return true;
            int treasureId = item.ClassId;
            if (catalog.Treasure(treasureId) == null)
                treasureId = catalog.Treasures.FirstOrDefault(value => value.TreasurePieceId == item.ClassId)?.ClassId ?? 0;
            if (treasureId == 0) return true;
            return (selectedSeries.ChestGachaItems ?? Array.Empty<int>()).Contains(treasureId);
        }

        private void AddSeriesCandidates(TreasureGachaSeries series, TreasureRandomReward reward,
            Dictionary<int, TreasureItemConfig> selected)
        {
            foreach (int treasureId in series.ChestGachaItems ?? Array.Empty<int>())
            {
                int itemId = treasureId;
                if (reward.IsPiece)
                {
                    TreasureInfoConfig info = catalog.Treasure(treasureId);
                    itemId = info == null ? 0 : info.TreasurePieceId;
                }
                TreasureItemConfig item = catalog.Item(itemId);
                if (item == null || reward.Quality > 0 && item.Quality != reward.Quality
                    || reward.ElementTypes != null && reward.ElementTypes.Length > 0
                        && !reward.ElementTypes.Contains(item.ElementType)) continue;
                if (!selected.ContainsKey(item.ClassId)) selected.Add(item.ClassId, item);
            }
        }

        private OfflineTreasureAward GrantDrop(OfflinePlayerProgress player, TreasureDrop drop, long now)
        {
            var award = new OfflineTreasureAward
            {
                ItemId = drop.Item.ClassId,
                Count = drop.Count,
                Quality = drop.Quality,
                IsPiece = drop.IsPiece
            };
            if (!drop.IsPiece && !drop.Item.CanRepeat && InventoryCount(player, drop.Item.ClassId) > 0)
            {
                if (drop.Item.AgioItemId <= 0 || drop.Item.AgioAmount <= 0)
                    throw new InvalidOperationException("重复宝具缺少官方折算规则");
                long amount = checked(drop.Count * drop.Item.AgioAmount);
                Grant(player, drop.Item.AgioItemId, amount, now);
                award.ConvertedItemId = drop.Item.AgioItemId;
                award.ConvertedCount = amount;
                return award;
            }
            Grant(player, drop.Item.ClassId, drop.Count, now);
            return award;
        }

        private void EnsureGuarantees(OfflineTreasureGachaProgress state, TreasureRewardRule rule)
        {
            if (rule == null) return;
            int recordId = SharedRecordId(rule);
            var plans = new List<GuaranteePlan>();
            int[] ids = rule.IdList ?? Array.Empty<int>();
            var tracked = new HashSet<int>();
            for (int index = 0; index < ids.Length; index++)
            {
                int threshold = rule.IndexMininum != null && rule.IndexMininum.Length > index
                    ? rule.IndexMininum[index] : 0;
                TreasureRandomReward reward = catalog.RandomReward(ids[index]);
                if (threshold <= 0 || reward == null || reward.Quality <= 1
                    || !tracked.Add(reward.Quality)) continue;
                OfflineTreasurePityCounter counter = FindPity(state, recordId, reward.Quality);
                long due = checked(counter.DrawCount + Math.Max(1, threshold - counter.MissCount));
                plans.Add(new GuaranteePlan(reward.Quality, due, index));
            }

            state.ScheduledGuarantees.RemoveAll(value => value.RecordId == recordId);
            long current = plans.Select(value => FindPity(state, recordId, value.Quality).DrawCount)
                .DefaultIfEmpty(0).Max();
            var occupied = new HashSet<long>();
            foreach (IGrouping<long, GuaranteePlan> group in plans.GroupBy(value => value.DuePull)
                .OrderByDescending(value => value.Key))
            {
                GuaranteePlan[] collisions = group.OrderBy(value => value.Order)
                    .ThenBy(value => value.Quality).ToArray();
                long first = Math.Max(current + 1, group.Key - collisions.Length + 1);
                while (first >= current + 1 && Enumerable.Range(0, collisions.Length)
                    .Any(index => occupied.Contains(first + index))) first--;
                if (first < current + 1)
                {
                    first = current + 1;
                    while (Enumerable.Range(0, collisions.Length)
                        .Any(index => occupied.Contains(first + index))) first++;
                }
                for (int index = 0; index < collisions.Length; index++)
                {
                    long target = first + index;
                    occupied.Add(target);
                    state.ScheduledGuarantees.Add(new OfflineTreasureScheduledGuarantee
                    {
                        RecordId = recordId,
                        TargetPull = target,
                        Quality = collisions[index].Quality
                    });
                }
            }
        }

        private void UpdatePity(OfflineTreasureGachaProgress state, TreasureRewardRule rule,
            int recordId, int quality)
        {
            int[] ids = rule.IdList ?? Array.Empty<int>();
            var tracked = new HashSet<int>();
            for (int index = 0; index < ids.Length; index++)
            {
                int threshold = rule.IndexMininum != null && rule.IndexMininum.Length > index
                    ? rule.IndexMininum[index] : 0;
                TreasureRandomReward reward = catalog.RandomReward(ids[index]);
                if (threshold <= 0 || reward == null || reward.Quality <= 1
                    || !tracked.Add(reward.Quality)) continue;
                OfflineTreasurePityCounter counter = FindPity(state, recordId, reward.Quality);
                counter.DrawCount = checked(counter.DrawCount + 1);
                if (quality == reward.Quality)
                {
                    counter.MissCount = 0;
                    counter.HitCount = checked(counter.HitCount + 1);
                }
                else counter.MissCount = checked(counter.MissCount + 1);
            }
        }

        private int GuaranteeThreshold(TreasureRewardRule rule, int quality)
        {
            for (int index = 0; index < (rule.IdList ?? Array.Empty<int>()).Length; index++)
            {
                TreasureRandomReward reward = catalog.RandomReward(rule.IdList[index]);
                if (reward != null && reward.Quality == quality)
                    return rule.IndexMininum != null && rule.IndexMininum.Length > index
                        ? rule.IndexMininum[index] : 0;
            }
            return 0;
        }

        private IReadOnlyList<TreasureGachaAchievementEntry> AchievementPage(int group, int page,
            int pageSize, OfflineTreasureGachaProgress state)
        {
            int start = checked(page * pageSize);
            int total = AchievementCount(group);
            var rows = new List<TreasureGachaAchievementEntry>(pageSize);
            for (int index = start; index < Math.Min(total, start + pageSize); index++)
            {
                TreasureGachaAchievementEntry value = AchievementAt(group, index, state);
                if (value != null)
                {
                    value.CanClaim = value.IsReady && !value.IsClaimed
                        && IsFirstUnclaimed(group, index, state);
                    rows.Add(value);
                }
            }
            return rows;
        }

        private TreasureGachaAchievementEntry AchievementAt(int group, int index,
            OfflineTreasureGachaProgress state)
        {
            if (state == null || group < 11 || group > 13 || index < 0) return null;
            TreasureAchievementConfig[] rows = catalog.Achievements.Where(value => value.Group == group)
                .OrderBy(value => value.Id).ToArray();
            TreasureAchievementConfig[] independent = rows.Where(value => value.CompleteCount > 0
                && value.CycleIncrementCount <= 0).ToArray();
            TreasureAchievementConfig[] cycle = rows.Where(value => value.CycleIncrementCount > 0).ToArray();
            int cycleCount = rows.Where(value => value.CycleCount > 0).Select(value => value.CycleCount)
                .DefaultIfEmpty(0).Max();
            if (index < independent.Length)
                return AchievementEntry(group, index, independent[index], 0,
                    independent[index].CompleteCount, GroupProgress(group, state), state);
            int cyclePosition = index - independent.Length;
            if (cycle.Length == 0 || cyclePosition >= cycle.Length * cycleCount) return null;
            int cycleIndex = cyclePosition / cycle.Length;
            int templateIndex = cyclePosition % cycle.Length;
            TreasureAchievementConfig anchor = rows.FirstOrDefault(value => value.CycleCount > 0);
            if (anchor == null) return null;
            long target = checked(anchor.CompleteCount + ((long)cycleIndex * cycle.Length
                + templateIndex + 1) * cycle[templateIndex].CycleIncrementCount);
            return AchievementEntry(group, independent.Length + cyclePosition, cycle[templateIndex],
                cycleIndex + 1, target, GroupProgress(group, state), state);
        }

        private int AchievementCount(int group)
        {
            TreasureAchievementConfig[] rows = catalog.Achievements.Where(value => value.Group == group).ToArray();
            int independent = rows.Count(value => value.CompleteCount > 0 && value.CycleIncrementCount <= 0);
            int cycle = rows.Count(value => value.CycleIncrementCount > 0);
            int cycles = rows.Select(value => value.CycleCount).DefaultIfEmpty(0).Max();
            return checked(independent + cycle * cycles);
        }

        private static TreasureGachaAchievementEntry AchievementEntry(int group, int index,
            TreasureAchievementConfig row, int cycleIndex, long target, long current,
            OfflineTreasureGachaProgress state)
        {
            OfflineTreasureAchievementProgress progress = state.AchievementProgress.FirstOrDefault(value =>
                value.Group == group && value.TemplateId == row.Id && value.CycleIndex == cycleIndex);
            return new TreasureGachaAchievementEntry
            {
                Group = group,
                Index = index,
                TemplateId = row.Id,
                CycleIndex = cycleIndex,
                CurrentValue = current,
                Target = target,
                ClaimedCount = progress?.ClaimedCount ?? 0,
                Rewards = row.Reward ?? Array.Empty<TreasureAmount>()
            };
        }

        private bool IsFirstUnclaimed(int group, int index, OfflineTreasureGachaProgress state)
        {
            for (int previous = 0; previous < index; previous++)
            {
                TreasureGachaAchievementEntry entry = AchievementAt(group, previous, state);
                if (entry != null && !entry.IsClaimed) return false;
            }
            return true;
        }

        private static long GroupProgress(int group, OfflineTreasureGachaProgress state)
        {
            switch (group)
            {
                case 11: return state.PreciousDrawCount;
                case 12: return state.PreciousScore;
                case 13: return state.ElementDrawCount;
                default: return 0;
            }
        }

        private static void SetReminder(OfflineTreasureGachaProgress state, int gachaType,
            int day, bool suppress)
        {
            OfflineTreasureHundredReminder reminder = state.HundredReminders.FirstOrDefault(value =>
                value.GachaType == gachaType);
            if (suppress)
            {
                if (reminder == null) state.HundredReminders.Add(reminder =
                    new OfflineTreasureHundredReminder { GachaType = gachaType });
                reminder.DayKey = day;
            }
            else if (reminder != null) state.HundredReminders.Remove(reminder);
        }

        private static void TrimHistory(OfflineTreasureGachaProgress state, long nowUnixSeconds,
            int retentionDays, int limit)
        {
            long cutoff = Math.Max(0, nowUnixSeconds - retentionDays * 86400L);
            state.History.RemoveAll(value => value.UnixSeconds < cutoff);
            foreach (IGrouping<int, OfflineTreasureDrawRecord> group in state.History.GroupBy(value =>
                value.GachaType <= 7 ? value.GachaType : 8))
            {
                OfflineTreasureDrawRecord[] old = group.OrderByDescending(value => value.UnixSeconds)
                    .ThenByDescending(value => value.DrawIndex).Skip(limit).ToArray();
                foreach (OfflineTreasureDrawRecord value in old) state.History.Remove(value);
            }
        }

        private static OfflineTreasurePityCounter FindPity(OfflineTreasureGachaProgress state,
            int recordId, int quality)
        {
            OfflineTreasurePityCounter counter = state.PityCounters.FirstOrDefault(value =>
                value.RecordId == recordId && value.Quality == quality);
            if (counter == null)
                state.PityCounters.Add(counter = new OfflineTreasurePityCounter
                { RecordId = recordId, Quality = quality });
            return counter;
        }

        private static OfflineTreasurePoolCount FindOrCreatePool(OfflineTreasureGachaProgress state,
            int gachaType)
        {
            OfflineTreasurePoolCount pool = state.PoolCounts.FirstOrDefault(value => value.GachaType == gachaType);
            if (pool == null) state.PoolCounts.Add(pool = new OfflineTreasurePoolCount { GachaType = gachaType });
            return pool;
        }

        private static long PoolCountIn(OfflineTreasureGachaProgress state, int gachaType)
        {
            return state.PoolCounts.FirstOrDefault(value => value.GachaType == gachaType)?.Count ?? 0;
        }

        private static long PityDrawCount(OfflineTreasureGachaProgress state, int recordId)
        {
            return state.PityCounters.Where(value => value.RecordId == recordId)
                .Select(value => value.DrawCount).DefaultIfEmpty(0).Max();
        }

        private static int SharedRecordId(TreasureRewardRule rule)
        {
            return rule != null && int.TryParse(rule.RecordSharedId, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int id) && id > 0 ? id : rule?.Id ?? 0;
        }

        private static int SelectWeighted(int[] weights, ref long randomState)
        {
            if (weights == null || weights.Length == 0) return -1;
            long total = weights.Where(value => value > 0)
                .Aggregate(0L, (sum, value) => checked(sum + value));
            if (total <= 0) return -1;
            long roll = NextBounded(ref randomState, total);
            for (int index = 0; index < weights.Length; index++)
            {
                int weight = Math.Max(0, weights[index]);
                if (roll < weight) return index;
                roll -= weight;
            }
            return -1;
        }

        private static int NextInt(ref long randomState, int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            return (int)NextBounded(ref randomState, exclusiveMaximum);
        }

        private static long NextBounded(ref long randomState, long exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            return (long)(NextUInt64(ref randomState) % (ulong)exclusiveMaximum);
        }

        private static ulong NextUInt64(ref long randomState)
        {
            ulong value = unchecked((ulong)randomState);
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            randomState = unchecked((long)value);
            return unchecked(value * 2685821657736338717UL);
        }

        private static void EnsureRandomState(OfflineTreasureGachaProgress state, long now, string receipt)
        {
            if (state.RandomState != 0) return;
            unchecked
            {
                ulong seed = 14695981039346656037UL ^ (ulong)now;
                foreach (char value in receipt ?? string.Empty)
                {
                    seed ^= value;
                    seed *= 1099511628211UL;
                }
                state.RandomState = (long)(seed == 0 ? 88172645463325252UL : seed);
            }
        }

        private static int DayKey(DateTime localNow)
        {
            return int.Parse(localNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
        }

        private static DateTime LocalDateTime(long unixSeconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().DateTime;
        }

        private static long InventoryCount(OfflinePlayerProgress player, int itemId)
        {
            return player.Inventory.FirstOrDefault(value => value.ItemId == itemId)?.Count ?? 0;
        }

        private static void Grant(OfflinePlayerProgress player, int itemId, long count, long now)
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
        }

        private static OfflineTreasureAward CloneAward(OfflineTreasureAward award)
        {
            return award == null ? null : new OfflineTreasureAward
            {
                ItemId = award.ItemId,
                Count = award.Count,
                Quality = award.Quality,
                IsPiece = award.IsPiece,
                ConvertedItemId = award.ConvertedItemId,
                ConvertedCount = award.ConvertedCount,
                RecycledItemId = award.RecycledItemId,
                RecycledCount = award.RecycledCount
            };
        }

        private static OfflineTreasureDrawRecord CloneRecord(OfflineTreasureDrawRecord record)
        {
            return new OfflineTreasureDrawRecord
            {
                ReceiptId = record.ReceiptId,
                DrawIndex = record.DrawIndex,
                GachaType = record.GachaType,
                SeriesId = record.SeriesId,
                UnixSeconds = record.UnixSeconds,
                Awards = record.Awards.Select(CloneAward).ToList()
            };
        }

        private static TreasureGachaResultCode Map(ProgressionResultStatus status)
        {
            switch (status)
            {
                case ProgressionResultStatus.Applied: return TreasureGachaResultCode.Applied;
                case ProgressionResultStatus.Unchanged: return TreasureGachaResultCode.Unchanged;
                case ProgressionResultStatus.NotReady: return TreasureGachaResultCode.NotReady;
                default: return TreasureGachaResultCode.InvalidState;
            }
        }

        private static TreasureGachaResult Result(TreasureGachaResultCode code, string message = null)
        {
            return new TreasureGachaResult(code, message);
        }

        private sealed class TreasureDrop
        {
            public TreasureItemConfig Item;
            public int Count;
            public int Quality;
            public bool IsPiece;
        }

        private sealed class GuaranteePlan
        {
            public readonly int Quality;
            public readonly long DuePull;
            public readonly int Order;
            public GuaranteePlan(int quality, long duePull, int order)
            {
                Quality = quality;
                DuePull = duePull;
                Order = order;
            }
        }
    }
}
