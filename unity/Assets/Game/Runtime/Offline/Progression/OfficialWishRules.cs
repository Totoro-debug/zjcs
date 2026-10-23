using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class WishRuntimeCatalog
    {
        public string manifest;
        public WishGacha[] gachas;
        public WishChest[] chests;
        public WishRewardRule[] rewardRules;
        public WishRandomReward[] randomRewards;
        public WishLevelNumber[] levelNumbers;
        public WishItem[] items;
        public WishProfession[] professions;
        public WishSkill[] skills;
        public WishAchievement[] achievements;
        public WishShop[] shops;
        public WishShopGood[] shopGoods;
        public WishShopGroup[] shopGroups;
        public WishPityProbability[] pityProbabilities;
        public WishQuickBuy[] quickBuys;
        public WishSettings settings;
        public WishFixedReward[] fixedRewards;
        public WishIcon[] icons;

        public static WishRuntimeCatalog Parse(string json)
        {
            WishRuntimeCatalog catalog = JsonUtility.FromJson<WishRuntimeCatalog>(json ?? string.Empty);
            if (catalog == null || catalog.manifest != "854_156383" || catalog.gachas == null
                || catalog.gachas.Length != 3 || catalog.chests == null || catalog.chests.Length != 3
                || catalog.rewardRules == null || catalog.items == null || catalog.skills == null
                || catalog.professions == null || catalog.achievements == null || catalog.shops == null
                || catalog.shopGoods == null || catalog.shopGroups == null || catalog.settings == null
                || catalog.quickBuys == null || catalog.fixedRewards == null || catalog.levelNumbers == null
                || catalog.FixedReward(catalog.settings.SkillGachaGuideFixedRewardId) == null)
                throw new FormatException("The official wish catalog is incomplete or from an unsupported manifest.");
            var levelNumberIds = new HashSet<int>();
            foreach (WishRewardRule rule in catalog.rewardRules)
            {
                foreach (int id in rule.LevelCountId ?? Array.Empty<int>()) if (id > 0) levelNumberIds.Add(id);
                foreach (int id in rule.SubRankCountId ?? Array.Empty<int>()) if (id > 0) levelNumberIds.Add(id);
            }
            foreach (WishRandomReward reward in catalog.randomRewards)
            {
                if (reward.LevelCountId > 0) levelNumberIds.Add(reward.LevelCountId);
                if (reward.SubRankCountId > 0) levelNumberIds.Add(reward.SubRankCountId);
            }
            foreach (int id in levelNumberIds)
            {
                WishLevelNumber row = catalog.LevelNumber(id);
                if (row == null || row.Nums == null || row.Nums.Length == 0)
                    throw new FormatException("The official wish catalog is missing level-number row " + id + ".");
            }
            return catalog;
        }

        public WishGacha Gacha(int type) => gachas.FirstOrDefault(value => value.GachaType == type);
        public WishChest Chest(int id) => chests.FirstOrDefault(value => value.ClassId == id);
        public WishItem Item(int id) => items.FirstOrDefault(value => value.ClassId == id);
        public WishSkill Skill(int id) => skills.FirstOrDefault(value => value.id == id);
        public WishProfession Profession(int id) => professions.FirstOrDefault(value => value.Profession == id);
        public WishRewardRule RewardRule(int id) => rewardRules.FirstOrDefault(value => value.Id == id);
        public WishRandomReward RandomReward(int id) => randomRewards.FirstOrDefault(value => value.Id == id);
        public WishShop Shop(int id) => shops.FirstOrDefault(value => value.Id == id);
        public WishShopGood ShopGood(int id) => shopGoods.FirstOrDefault(value => value.Id == id);
        public WishShopGroup ShopGroup(int id) => shopGroups.FirstOrDefault(value => value.GroupId == id);
        public WishFixedReward FixedReward(int id) => fixedRewards.FirstOrDefault(value => value.Id == id);
        public WishLevelNumber LevelNumber(int id) => (levelNumbers ?? Array.Empty<WishLevelNumber>())
            .FirstOrDefault(value => value.Id == id);

        public double LevelMultiplier(int id, int level)
        {
            if (id <= 0) return 1d;
            WishLevelNumber row = LevelNumber(id);
            if (row == null || row.Nums == null || row.Nums.Length == 0)
                throw new InvalidOperationException("Official level-number configuration is missing: " + id);
            return row.Nums[Math.Min(Math.Max(0, level), row.Nums.Length - 1)];
        }

        public bool SkillAvailable(int skillId, int profession)
        {
            WishSkill skill = Skill(skillId);
            if (skill == null) return false;
            if (profession <= 0)
            {
                WishProfession unlock = Profession(skill.unlockProfession);
                return unlock != null && unlock.Rank == 1;
            }
            WishProfession current = Profession(profession);
            for (int depth = 0; current != null && depth < 32; depth++)
            {
                if ((current.Skills ?? Array.Empty<int>()).Contains(skillId)) return true;
                if (current.PrePro <= 0 || current.PrePro == current.Profession) break;
                current = Profession(current.PrePro);
            }
            return false;
        }

        public int[] ProfessionChain(int profession)
        {
            var chain = new System.Collections.Generic.List<int>();
            WishProfession current = Profession(profession);
            for (int depth = 0; current != null && depth < 32; depth++)
            {
                chain.Add(current.Profession);
                if (current.PrePro <= 0 || current.PrePro == current.Profession) break;
                current = Profession(current.PrePro);
            }
            return chain.ToArray();
        }

        public WishSkillGachaSettings SettingsForProfession(int profession)
        {
            WishProfession row = Profession(profession);
            int rank = row == null ? 1 : Math.Max(1, row.Rank);
            WishRankSkillGachaSettings overrideRow = (settings.RankSkillGachaSettings ?? Array.Empty<WishRankSkillGachaSettings>())
                .FirstOrDefault(value => value.Rank == rank);
            return overrideRow == null ? settings.DefaultSkillGachaSettings
                : new WishSkillGachaSettings { EnsureHighRank = overrideRow.EnsureHighRank,
                    EnsureHighRankPieceCount = overrideRow.EnsureHighRankPieceCount };
        }
    }

    [Serializable] public sealed class WishGacha
    {
        public int GachaType, ChestId, CostItemId, RewardScoreId, SourceOperate;
        public string ScoreRuleText;
        public int CountAchievementType, ScoreAchievementType, ShowRedDotMinCount, GachaClassType;
        public bool RecordHistory;
    }
    [Serializable] public sealed class WishChest
    {
        public int ClassId;
        public int[] RewardRules, OpenCounts;
        public WishAmount[] Rewards, CostItems;
    }
    [Serializable] public sealed class WishAmount { public int ItemId; public long Count; }
    [Serializable] public sealed class WishRewardRule
    {
        public int Id, Order;
        public int[] IdList, WeightList, IndexMininum, LevelCountId, SubRankCountId;
        public float[] CountList;
        public string RecordSharedId;
    }
    [Serializable] public sealed class WishRandomReward
    {
        public int Id, Quality, MinCount, MaxCount, Rank, LevelCountId, SubRankCountId, WeightScaleCountLimit;
        public float WeightScaleFactor;
        public int[] IdList, WeightOfIdList, ItemTypes, ElementTypes, BlackList;
    }
    [Serializable] public sealed class WishLevelNumber { public int Id; public double[] Nums; }
    [Serializable] public sealed class WishItem
    {
        public int ClassId, ItemType, Quality, Rank, ElementType, AgioItemId, AgioAmount, RecycleGroupCount;
        public string Name, RawIcon;
        public bool CanRepeat, ShowInBag;
        public int[] AgioRuleIdList;
        public WishAmount[] RecycleAwards;
    }
    [Serializable] public sealed class WishProfession
    {
        public int Profession, Rank, PrePro;
        public int[] Skills, FightUsedSkills;
        public string Icon1, Icon2, Icon3, Icon1File, Icon3File, TextColor;
    }
    [Serializable] public sealed class WishSkill { public int id, unlockProfession, maxRank; }
    [Serializable] public sealed class WishAchievement
    {
        public int Id, Group, AchievementType, CycleCount;
        public string CompleteParams, CycleIncrementParams;
        public WishAmount[] Reward;
    }
    [Serializable] public sealed class WishShop
    {
        public int Id, Type, RefreshPriceFactor, RefreshCountsLimit, RefreshCycleType;
        public int[] GroupIdList, GroupRandomCounts, FixedGoodsList, RefreshCurrency, RefreshPriceBase;
        public bool RefreshByProfession, UserRefresh, Unique, SortByQuality;
    }
    [Serializable] public sealed class WishShopGood
    {
        public int Id, ItemId, Count, Currency, Inventory, TotalBuyCountLimit, RefreshCycleType;
        public int Weight;
        public float OriginalPrice;
        public bool SupportBuyMultiCount, PopConfirmDialog, ShowName;
        public WishCondition Condition, BuyCondition;
    }
    [Serializable] public sealed class WishCondition { public string _type; public int Profession; }
    [Serializable] public sealed class WishShopGroup
    {
        public int GroupId, Order, RefreshFilter;
        public int[] ShopGoodsList;
        public WishWeightedGroup[] ComplexGroupWeights;
    }
    [Serializable] public sealed class WishWeightedGroup { public int GroupId, Weight; }
    [Serializable] public sealed class WishPityProbability { public int GachaType, Quality, GroupType, PityProbability; }
    [Serializable] public sealed class WishIcon { public int id; public string file; }
    [Serializable] public sealed class WishFixedReward
    {
        public int Id, CanGetTimes, SourceOperate, Level;
        public WishAmount[] Reward;
    }
    [Serializable] public sealed class WishSettings
    {
        public WishSkillGachaSettings DefaultSkillGachaSettings;
        public WishRankSkillGachaSettings[] RankSkillGachaSettings;
        public WishCustomSelectionSettings SkillGachaCustomSelectionSettings;
        public int SkillGachaGuideFixedRewardId, SkillGachaNewbieTimes, AchievementPanelShowCount;
        public int[] SkillGachaOnceSupportTimes, ShopRefreshCoinNeedConfirm;
        public WishDailyLimit[] DailyGachaMaxCount;
    }
    [Serializable] public sealed class WishSkillGachaSettings
    {
        public WishQualityMiss[] EnsureHighRank;
        public int EnsureHighRankPieceCount;
    }
    [Serializable] public sealed class WishRankSkillGachaSettings
    {
        public int Rank;
        public WishQualityMiss[] EnsureHighRank;
        public int EnsureHighRankPieceCount;
    }
    [Serializable] public sealed class WishQualityMiss { public int Quality, MissCount; }
    [Serializable] public sealed class WishCustomSelectionSettings { public int RequiredProfessionRank, SelectionProfessionCount; }
    [Serializable] public sealed class WishDailyLimit { public int GachaClassType, Count; }
}
