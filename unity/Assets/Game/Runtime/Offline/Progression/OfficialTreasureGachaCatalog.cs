using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class TreasureGachaRuntimeCatalog
    {
        public string Manifest;
        public TreasureGachaConfig[] Gachas;
        public TreasureChestConfig[] Chests;
        public TreasureRewardRule[] RewardRules;
        public TreasureRandomReward[] RandomRewards;
        public TreasureGachaSeries[] Series;
        public TreasureCondition[] Conditions;
        public TreasureItemConfig[] Items;
        public TreasureInfoConfig[] Treasures;
        public TreasureAchievementConfig[] Achievements;
        public TreasureGachaPityProbability[] PityProbabilities;
        public TreasureGachaSettings Settings;

        private Dictionary<int, TreasureGachaConfig> gachasByType;
        private Dictionary<int, TreasureChestConfig> chestsById;
        private Dictionary<int, TreasureRewardRule> rulesById;
        private Dictionary<int, TreasureRandomReward> rewardsById;
        private Dictionary<int, TreasureGachaSeries> seriesById;
        private Dictionary<int, TreasureCondition> conditionsById;
        private Dictionary<int, TreasureItemConfig> itemsById;
        private Dictionary<int, TreasureInfoConfig> treasuresById;

        public static TreasureGachaRuntimeCatalog Parse(string json)
        {
            TreasureGachaRuntimeCatalog catalog = JsonUtility.FromJson<TreasureGachaRuntimeCatalog>(json ?? string.Empty);
            if (catalog == null || catalog.Manifest != "854_156383" || catalog.Gachas == null || catalog.Gachas.Length != 9
                || catalog.Chests == null || catalog.Chests.Length != 9 || catalog.RewardRules == null
                || catalog.RandomRewards == null || catalog.Series == null || catalog.Series.Length != 17
                || catalog.Conditions == null || catalog.Items == null || catalog.Treasures == null
                || catalog.Achievements == null || catalog.PityProbabilities == null || catalog.Settings == null
                || catalog.Settings.QualityDistributions == null || catalog.Settings.QualityDistributions.Length != 9
                || catalog.Settings.ElementGachaMappings == null || catalog.Settings.ElementGachaMappings.Length != 6)
                throw new FormatException("The official treasure gacha catalog is incomplete or from an unsupported manifest.");
            catalog.BuildIndex();
            catalog.Validate();
            return catalog;
        }

        public TreasureGachaConfig Gacha(int type) { EnsureIndex(); return gachasByType.TryGetValue(type, out TreasureGachaConfig row) ? row : null; }
        public TreasureChestConfig Chest(int id) { EnsureIndex(); return chestsById.TryGetValue(id, out TreasureChestConfig row) ? row : null; }
        public TreasureRewardRule Rule(int id) { EnsureIndex(); return rulesById.TryGetValue(id, out TreasureRewardRule row) ? row : null; }
        public TreasureRandomReward RandomReward(int id) { EnsureIndex(); return rewardsById.TryGetValue(id, out TreasureRandomReward row) ? row : null; }
        public TreasureGachaSeries SeriesInfo(int id) { EnsureIndex(); return seriesById.TryGetValue(id, out TreasureGachaSeries row) ? row : null; }
        public TreasureCondition Condition(int id) { EnsureIndex(); return conditionsById.TryGetValue(id, out TreasureCondition row) ? row : null; }
        public TreasureItemConfig Item(int id) { EnsureIndex(); return itemsById.TryGetValue(id, out TreasureItemConfig row) ? row : null; }
        public TreasureInfoConfig Treasure(int id) { EnsureIndex(); return treasuresById.TryGetValue(id, out TreasureInfoConfig row) ? row : null; }

        public int[] QualityDistribution(int chestId)
        {
            return Settings.QualityDistributions.FirstOrDefault(value => value.ChestId == chestId)?.Qualities;
        }

        public int ElementIdForGachaType(int gachaType)
        {
            if (gachaType >= 4 && gachaType <= 7) return 0;
            TreasureElementGachaMapping mapping = Settings.ElementGachaMappings
                .FirstOrDefault(value => value.GachaType == gachaType);
            return mapping == null ? -1 : mapping.ElementId;
        }

        public IEnumerable<TreasureGachaSeries> SortedSeries()
        {
            return (Series ?? Array.Empty<TreasureGachaSeries>()).OrderBy(value => value.Order).ThenBy(value => value.ChestGachaId);
        }

        public bool IsSeriesOpen(TreasureGachaSeries series, OfflinePlayerProgress player, DateTime localNow,
            long createdAtUnixSeconds, out DateTime? nextOpenTime)
        {
            nextOpenTime = null;
            if (series == null || player == null) return false;
            if (!HasMap(player, series.MapClassId)) return false;
            if (series.ConditionType == "MapUnlockCondition")
                return HasMap(player, series.ConditionMapClassId);
            if (series.ConditionType != "CompositeAndCondition") return false;

            DateTime? latestRankOpenTime = null;
            bool rankConditionsMet = true;
            foreach (int id in series.ConditionIds ?? Array.Empty<int>())
            {
                TreasureCondition condition = Condition(id);
                if (condition == null) return false;
                if (condition.Type == "MapUnlockCondition")
                {
                    if (!HasMap(player, condition.MapClassId)) return false;
                    continue;
                }
                if (condition.Type != "ServerRankUnlockDaysCondition" || player.Rank < condition.Rank
                    || condition.IsOnlyRank && player.Rank != condition.Rank)
                    return false;
                long unlockUnix = RankUnlockUnixSeconds(player, condition.Rank, createdAtUnixSeconds);
                if (unlockUnix <= 0) return false;
                DateTime rankTime = DateTimeOffset.FromUnixTimeSeconds(unlockUnix).ToLocalTime().DateTime;
                int rankDays = RankDayCount(rankTime, localNow);
                if (!CompareRankDays(rankDays, condition.Op, condition.Days))
                {
                    rankConditionsMet = false;
                    if (condition.Op == ">=" || condition.Op == ">")
                    {
                        DateTime opensAt = rankTime.Date.AddDays(condition.Days
                            + (condition.Op == ">" ? 1 : 0));
                        if (!latestRankOpenTime.HasValue || opensAt > latestRankOpenTime.Value)
                            latestRankOpenTime = opensAt;
                    }
                }
            }
            if (!rankConditionsMet)
            {
                nextOpenTime = latestRankOpenTime;
                return false;
            }
            return true;
        }

        public bool IsSeriesPreview(TreasureGachaSeries series, OfflinePlayerProgress player, DateTime localNow,
            long createdAtUnixSeconds, out DateTime opensAt)
        {
            opensAt = default;
            if (series == null || series.PreviewDuration <= 0 || player == null || !HasMap(player, series.MapClassId)
                || series.ConditionType != "CompositeAndCondition") return false;
            DateTime? candidate = null;
            foreach (int id in series.ConditionIds ?? Array.Empty<int>())
            {
                TreasureCondition condition = Condition(id);
                if (condition == null) return false;
                if (condition.Type == "MapUnlockCondition")
                {
                    if (!HasMap(player, condition.MapClassId)) return false;
                    continue;
                }
                if (condition.Type != "ServerRankUnlockDaysCondition" || player.Rank < condition.Rank
                    || condition.IsOnlyRank && player.Rank != condition.Rank) return false;
                long unlockUnix = RankUnlockUnixSeconds(player, condition.Rank, createdAtUnixSeconds);
                if (unlockUnix <= 0) return false;
                DateTime rankTime = DateTimeOffset.FromUnixTimeSeconds(unlockUnix).ToLocalTime().DateTime;
                int rankDays = RankDayCount(rankTime, localNow);
                if (CompareRankDays(rankDays, condition.Op, condition.Days)) return false;
                DateTime time = rankTime.Date.AddDays(condition.Days
                    + (condition.Op == ">" ? 1 : 0));
                if (!candidate.HasValue || time > candidate.Value) candidate = time;
            }
            if (!candidate.HasValue || localNow >= candidate.Value || localNow < candidate.Value.AddHours(-series.PreviewDuration))
                return false;
            opensAt = candidate.Value;
            return true;
        }

        public int RankDays(OfflinePlayerProgress player, int rank, DateTime localNow, long createdAtUnixSeconds)
        {
            if (player == null || player.Rank < rank || rank <= 0) return -1;
            long unlock = RankUnlockUnixSeconds(player, rank, createdAtUnixSeconds);
            if (unlock <= 0) return -1;
            DateTime start = DateTimeOffset.FromUnixTimeSeconds(unlock).ToLocalTime().DateTime;
            return Math.Max(0, RankDayCount(start, localNow));
        }

        private static int RankDayCount(DateTime rankUnlockTime, DateTime localNow)
        {
            return (localNow.Date - rankUnlockTime.Date).Days;
        }

        private static bool CompareRankDays(int value, string op, int target)
        {
            switch (op)
            {
                case ">": return value > target;
                case "<": return value < target;
                case "=": return value == target;
                case "<=": return value <= target;
                case "!=": return value != target;
                case ">=":
                default: return value >= target;
            }
        }

        private static bool HasMap(OfflinePlayerProgress player, int mapClassId)
        {
            return mapClassId > 0 && player.UnlockedMapClassIds != null && player.UnlockedMapClassIds.Contains(mapClassId);
        }

        private static long RankUnlockUnixSeconds(OfflinePlayerProgress player, int rank, long createdAtUnixSeconds)
        {
            OfflineRankUnlockTime value = player.RankUnlockTimes?.FirstOrDefault(item => item.Rank == rank);
            if (value != null) return value.UnixSeconds;
            return player.Rank >= rank ? createdAtUnixSeconds : 0;
        }

        private void BuildIndex()
        {
            gachasByType = Gachas.ToDictionary(value => value.GachaType);
            chestsById = Chests.ToDictionary(value => value.ClassId);
            rulesById = RewardRules.ToDictionary(value => value.Id);
            rewardsById = RandomRewards.ToDictionary(value => value.Id);
            seriesById = Series.ToDictionary(value => value.ChestGachaId);
            conditionsById = Conditions.ToDictionary(value => value.ClassId);
            itemsById = Items.ToDictionary(value => value.ClassId);
            treasuresById = Treasures.ToDictionary(value => value.ClassId);
        }

        private void EnsureIndex()
        {
            if (gachasByType == null) BuildIndex();
        }

        private void Validate()
        {
            foreach (TreasureGachaConfig gacha in Gachas)
            {
                if (gacha == null || gacha.GachaType < 4 || gacha.GachaType > 12 || Chest(gacha.ChestId) == null
                    || gacha.CostItemId <= 0 || gacha.GachaClassType != 2)
                    throw new FormatException("The official treasure gacha type table contains an invalid row.");
            }
            foreach (TreasureChestConfig chest in Chests)
            {
                if (chest == null || chest.ClassId <= 0 || chest.OpenCounts == null || chest.OpenCounts.Length == 0
                    || chest.CostItems == null || chest.CostItems.Length == 0 || chest.RewardRules == null
                    || chest.RewardRules.Length == 0 || chest.CostItems.Any(value => value == null || value.ItemId <= 0 || value.Count <= 0))
                    throw new FormatException("The official treasure gacha chest table contains an invalid row.");
            }
            foreach (TreasureGachaSeries series in Series)
                if (series == null || series.ChestGachaId <= 0 || series.Order <= 0 || series.MapClassId <= 0
                    || series.ChestGachaItems == null || series.ChestGachaItems.Length == 0)
                    throw new FormatException("The official treasure gacha series table contains an invalid row.");
            foreach (int type in Enumerable.Range(4, 9))
                if (!PityProbabilities.Any(value => value != null && value.GachaType == type))
                    throw new FormatException("The official treasure gacha pity probability table is incomplete.");
            if (Settings.DailyLimit != 10000 || Settings.ScoreItemId != 250 || Settings.HistoryDays != 180
                || Settings.HistoryLimit != 1000 || Settings.QualityDistributions.Length != Chests.Length
                || Settings.ElementGachaMappings.Count(value => value != null && value.GachaType >= 8
                    && value.GachaType <= 12) != 5)
                throw new FormatException("The official treasure gacha settings do not match the pinned source.");
            foreach (TreasureChestConfig chest in Chests)
            {
                int[] qualities = QualityDistribution(chest.ClassId);
                if (qualities == null || qualities.Length != 10 || qualities.Any(value => value < 1 || value > 5))
                    throw new FormatException("The official treasure gacha capsule quality distribution is invalid.");
            }
        }
    }

    [Serializable] public sealed class TreasureGachaConfig
    {
        public int GachaType, ChestId, CostItemId, RewardScoreId, SourceOperate;
        public string ScoreRuleText;
        public int CountAchievementType, ScoreAchievementType, ShowRedDotMinCount, GachaClassType;
        public bool RecordHistory;
    }
    [Serializable] public sealed class TreasureChestConfig
    {
        public int ClassId;
        public int[] FirstRewardRuleIds, RewardRules, OpenCounts;
        public TreasureAmount[] Rewards, CostItems;
    }
    [Serializable] public sealed class TreasureAmount { public int ItemId; public long Count; }
    [Serializable] public sealed class TreasureRewardRule
    {
        public int Id, Order;
        public int[] IdList, WeightList, IndexMininum;
        public float[] CountList;
        public string RecordSharedId;
    }
    [Serializable] public sealed class TreasureRandomReward
    {
        public int Id, Quality, MinCount, MaxCount, MapClassId;
        public int[] IdList, WeightOfIdList, DefaultGachaIds, ElementTypes;
        public bool IsNeedMet, IsPiece;
        public string SelectorType;
    }
    [Serializable] public sealed class TreasureGachaSeries
    {
        public int ChestGachaId, Order, MapClassId, PreviewDuration;
        public string MainTitle, Subtitle, BgSpritePath, ConditionType;
        public string EntryBgSpriteFile, EntryIconSpriteFile, IconSpriteFile;
        public int ConditionMapClassId;
        public int[] ConditionIds, ChestGachaItems;
    }
    [Serializable] public sealed class TreasureCondition
    {
        public int ClassId, MapClassId, Rank, Days;
        public string Type, Op;
        public bool IsOnlyRank;
    }
    [Serializable] public sealed class TreasureItemConfig
    {
        public int ClassId, ItemType, Quality, ElementType, AgioItemId, AgioAmount, RecycleGroupCount;
        public string Name, RawIcon, IconFile;
        public bool CanRepeat, CanDrop, CanDropWhenExist, ShowInBag;
        public TreasureAmount[] RecycleReward;
    }
    [Serializable] public sealed class TreasureInfoConfig { public int ClassId, TreasurePieceId; }
    [Serializable] public sealed class TreasureAchievementConfig
    {
        public int Id, Group, AchievementType, CycleCount, CompleteCount, CycleIncrementCount;
        public TreasureAmount[] Reward;
    }
    [Serializable] public sealed class TreasureGachaPityProbability
    {
        public int GachaType, Quality, GroupType, PityProbability;
    }
    [Serializable] public sealed class TreasureGachaSettings
    {
        public int DailyLimit, ScoreItemId, HistoryDays, HistoryLimit, DefaultGachaId;
        public bool BlockInactiveElementMaps;
        public TreasureQualityDistribution[] QualityDistributions;
        public TreasureElementGachaMapping[] ElementGachaMappings;
    }
    [Serializable] public sealed class TreasureQualityDistribution
    {
        public int ChestId;
        public int[] Qualities;
    }
    [Serializable] public sealed class TreasureElementGachaMapping
    {
        public int ElementId, GachaType;
    }

    public sealed class OfficialTreasureGachaAssets : MonoBehaviour
    {
        public const string DefaultResourcePath = "TreasureGacha/treasure-gacha-runtime";
        public TreasureGachaRuntimeCatalog Catalog { get; private set; }

        public static TreasureGachaRuntimeCatalog LoadDefault()
        {
            TextAsset data = Resources.Load<TextAsset>(DefaultResourcePath);
            if (data == null) throw new InvalidOperationException("Official treasure gacha rules are not installed.");
            return TreasureGachaRuntimeCatalog.Parse(data.text);
        }
    }
}
