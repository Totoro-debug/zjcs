using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class PetGachaRuntimeCatalog
    {
        public string Manifest;
        public PetGachaConfig Gacha;
        public PetGachaChest Chest;
        public PetGachaRewardRule[] RewardRules;
        public PetGachaRandomReward[] RandomRewards;
        public PetGachaPetProfile[] PetProfiles;
        public PetGachaItem[] Items;
        public PetGachaEntryDetail[] Entries;
        public PetGachaQualityBackground[] QualityBackgrounds;
        public PetGachaAchievement[] Achievements;
        public PetGachaPityProbability[] PityProbabilities;
        public PetGachaShop Shop;
        public PetGachaShop PetShop;
        public PetGachaSettings Settings;

        private Dictionary<int, PetGachaRewardRule> rulesById;
        private Dictionary<int, PetGachaRandomReward> rewardsById;
        private Dictionary<int, PetGachaPetProfile> petProfilesById;
        private Dictionary<int, PetGachaItem> itemsById;
        private Dictionary<int, PetGachaEntryDetail> entriesById;
        private Dictionary<int, PetGachaGoods> goodsById;

        public static PetGachaRuntimeCatalog Parse(string json)
        {
            PetGachaRuntimeCatalog catalog = JsonUtility.FromJson<PetGachaRuntimeCatalog>(json ?? string.Empty);
            if (catalog == null) throw new FormatException("The official pet gacha catalog is empty.");
            if (catalog.Shop == null || catalog.Shop.Goods == null) catalog.Shop = catalog.PetShop;
            if (catalog == null || catalog.Manifest != "854_156383" || catalog.Gacha == null
                || catalog.Chest == null || catalog.RewardRules == null || catalog.RandomRewards == null
                || catalog.PetProfiles == null || catalog.Items == null || catalog.Entries == null
                || catalog.QualityBackgrounds == null
                || catalog.Achievements == null || catalog.PityProbabilities == null
                || catalog.Shop == null || catalog.Settings == null)
                throw new FormatException("The official pet gacha catalog is incomplete or from an unsupported manifest.");
            catalog.BuildIndex();
            catalog.Validate();
            return catalog;
        }

        public PetGachaRewardRule Rule(int id) { EnsureIndex(); return rulesById.TryGetValue(id, out var value) ? value : null; }
        public PetGachaRandomReward RandomReward(int id) { EnsureIndex(); return rewardsById.TryGetValue(id, out var value) ? value : null; }
        public PetGachaPetProfile PetProfile(int id) { EnsureIndex(); return petProfilesById.TryGetValue(id, out var value) ? value : null; }
        public PetGachaItem Item(int id) { EnsureIndex(); return itemsById.TryGetValue(id, out var value) ? value : null; }
        public PetGachaEntryDetail EntryDetail(int id)
        {
            EnsureIndex();
            return entriesById.TryGetValue(id, out var value) ? value : null;
        }
        public PetGachaGoods Goods(int id) { EnsureIndex(); return goodsById.TryGetValue(id, out var value) ? value : null; }
        public string QualityBackground(int quality) => QualityBackgrounds
            .FirstOrDefault(value => value.Quality == quality)?.File ?? string.Empty;
        public PetGachaAchievement[] AchievementsFor(int group) => Achievements.Where(value => value.Group == group)
            .OrderBy(value => value.Id).ToArray();

        public bool HasDailyReminderSuppression(int dayKey, OfflinePetGachaProgress state) =>
            state != null && state.HundredReminderSuppressedDayKey >= dayKey && dayKey > 0;

        private void BuildIndex()
        {
            if (Shop == null || Shop.Goods == null)
                throw new FormatException("The official pet exchange goods table is missing.");
            rulesById = RewardRules.ToDictionary(value => value.Id);
            rewardsById = RandomRewards.ToDictionary(value => value.Id);
            petProfilesById = PetProfiles.ToDictionary(value => value.ClassId);
            itemsById = Items.ToDictionary(value => value.ClassId);
            entriesById = Entries.ToDictionary(value => value.Id);
            goodsById = Shop.Goods.ToDictionary(value => value.Id);
        }

        private void EnsureIndex()
        {
            if (rulesById == null) BuildIndex();
        }

        private void Validate()
        {
            if (Gacha.GachaType != 3 || Gacha.ChestId != 6003 || Gacha.CostItemId != 107
                || Gacha.RewardScoreId != 254 || Gacha.CountAchievementType != 65
                || Gacha.ScoreAchievementType != 66 || !Gacha.RecordHistory)
                throw new FormatException("The pinned pet gacha type row does not match the source.");
            if (Chest.ClassId != 6003 || !Chest.FirstRewardRuleIds.SequenceEqual(new[] { 20401 })
                || !Chest.FirstRewardCounts.SequenceEqual(new[] { 10 })
                || !Chest.RewardRules.SequenceEqual(new[] { 20400 })
                || !Chest.OpenCounts.SequenceEqual(new[] { 1, 10, 100 })
                || Chest.CostItems.Length != 1 || Chest.CostItems[0].ItemId != 107 || Chest.CostItems[0].Count != 1
                || Chest.Rewards.Length != 1 || Chest.Rewards[0].ItemId != 254 || Chest.Rewards[0].Count != 1)
                throw new FormatException("The pinned pet gacha chest row does not match the source.");
            if (Rule(20400) == null || Rule(20401) == null || RandomRewards.Length != 6
                || Rule(20400).ItemIds.Length != 7 || Rule(20401).ItemIds.Length != 7
                || Rule(20400).Weights.Sum() != 10000 || Rule(20401).Weights.Sum() != 10000
                || !Rule(20400).RecordSharedId.Equals("20400", StringComparison.Ordinal)
                || !Rule(20401).RecordSharedId.Equals("20400", StringComparison.Ordinal))
                throw new FormatException("The pet gacha pity or reward package tables are incomplete.");
            foreach (PetGachaRewardRule rule in RewardRules)
                if (rule == null || rule.ItemIds == null || rule.Weights == null || rule.Counts == null
                    || rule.ItemIds.Length != rule.Weights.Length || rule.ItemIds.Length != rule.Counts.Length
                    || rule.GuaranteedCounts == null || rule.GuaranteedCounts.Length != rule.ItemIds.Length
                    || rule.ItemIds.Any(id => RandomReward(id) == null && Item(id) == null))
                    throw new FormatException("The pet gacha reward rule contains an invalid package reference.");
            foreach (PetGachaRandomReward reward in RandomRewards)
                if (reward == null || reward.ItemIds == null || reward.Weights == null
                    || reward.ItemIds.Length != reward.Weights.Length || reward.ItemIds.Any(id => Item(id) == null)
                    || reward.Weights.Any(value => value <= 0) || reward.Weights.Sum() <= 0
                    || reward.MinCount <= 0 || reward.MaxCount < reward.MinCount)
                    throw new FormatException("The pet gacha candidate package contains an invalid item or weight.");
            int[] expectedPetIds = { 1600, 1601, 1602, 1603, 1604, 1605, 1607, 1608 };
            int[] expectedPetModels = { 9201, 9202, 9203, 9204, 9205, 9206, 9208, 9209 };
            if (PetProfiles.Any(value => value == null)
                || !PetProfiles.Select(value => value.ClassId).OrderBy(value => value).SequenceEqual(expectedPetIds)
                || !PetProfiles.OrderBy(value => value.ClassId).Select(value => value.UIModel).SequenceEqual(expectedPetModels)
                || PetProfiles.Any(value => value.SupportSkillIds == null
                    || value.SupportSkillIds.Length == 0 || string.IsNullOrWhiteSpace(value.TypeIconFile)
                    || value.ModelScale <= 0f
                    || Item(value.ClassId)?.ItemType != 400
                    || value.SupportSkillIds.Any(skill => Item(skill) == null
                        || string.IsNullOrWhiteSpace(Item(skill).FuncDesc)
                        || Item(skill).HyperlinkEntryIds == null
                        || Item(skill).HyperlinkEntryIds.Any(id => EntryDetail(id) == null))))
                throw new FormatException("The official gacha pet models or childhood support skills are incomplete.");
            foreach (int id in new[] { 1600, 1601, 1602, 1603, 1604, 1605, 1607, 1608 })
                if (Item(id) == null || Item(id).ItemType != 400 || Item(id).Quality < 3
                    || Item(id).AgioItemId <= 0 || Item(id).AgioAmount != (Item(id).Quality == 3 ? 30 : 60))
                    throw new FormatException("The pet duplicate conversion data does not match the confirmed rule.");
            if (Shop.Id != 7 || !Shop.RefreshByProfession || Shop.RefreshCycleType != 1
                || Shop.RefreshCountsLimit != 0 || Shop.GroupIds == null || Shop.GroupRandomCounts == null
                || Shop.GroupIds.Length != Shop.GroupRandomCounts.Length || Shop.Goods == null
                || Shop.GoodsGroups.Any(value => value.HasComplexWeights))
                throw new FormatException("The official pet shop row contains unsupported stock generation data.");
            if (Settings.DailyLimit != 10000 || Settings.HistoryDays != 180 || Settings.HistoryLimit != 1000
                || Settings.QuickBuyItemId != 107 || Settings.QuickBuyCurrencyItemId != 2
                || Settings.QuickBuyPrice != 160 || Settings.WeeklyStartDayOfWeek != 1
                || Settings.DailyResetHour != 8 || Settings.DailyResetMinute != 0)
                throw new FormatException("The pet gacha limits or local reset settings do not match the source.");
        }
    }

    [Serializable] public sealed class PetGachaConfig
    {
        public int GachaType, ChestId, CostItemId, RewardScoreId, CountAchievementType, ScoreAchievementType;
        public int SourceOperate, ShowRedDotMinCount, GachaClassType;
        public string ScoreRuleText;
        public bool RecordHistory;
    }
    [Serializable] public sealed class PetGachaChest
    {
        public int ClassId;
        public int[] FirstRewardRuleIds, FirstRewardCounts, RewardRules, OpenCounts;
        public PetGachaAmount[] CostItems, Rewards;
    }
    [Serializable] public sealed class PetGachaAmount { public int ItemId; public long Count; }
    [Serializable] public sealed class PetGachaRewardRule
    {
        public int Id;
        public int[] ItemIds, Weights, GuaranteedCounts;
        public float[] Counts;
        public string RecordSharedId;
    }
    [Serializable] public sealed class PetGachaRandomReward
    {
        public int Id, Quality, MinCount, MaxCount, WeightScaleCountLimit;
        public int[] ItemIds, Weights;
        public float WeightScaleFactor;
    }
    [Serializable] public sealed class PetGachaPetProfile
    {
        public int ClassId, Type, UIModel;
        public float ModelScale, ModelOffsetY;
        public string TypeIconFile;
        public int[] SupportSkillIds;
    }
    [Serializable] public sealed class PetGachaItem
    {
        public int ClassId, ItemType, Quality, AgioItemId, AgioAmount;
        public string Name, RawIcon, IconFile, FuncDesc;
        public int[] HyperlinkEntryIds;
        public bool CanRepeat;
    }
    [Serializable] public sealed class PetGachaEntryDetail
    {
        public int Id;
        public string Name, Description, IconFile, BackgroundFile;
    }
    [Serializable] public sealed class PetGachaQualityBackground { public int Quality; public string File; }
    [Serializable] public sealed class PetGachaAchievement
    {
        public int Id, Group, AchievementType, CycleCount, CompleteCount, CycleIncrementCount;
        public PetGachaAmount[] Reward;
    }
    [Serializable] public sealed class PetGachaPityProbability
    {
        public int GachaType, Quality, GroupType, PityProbability;
    }
    [Serializable] public sealed class PetGachaShop
    {
        public int Id, RefreshCycleType, RefreshCountsLimit;
        public bool RefreshByProfession, UserRefresh;
        public int[] GroupIds, GroupRandomCounts, FixedGoodsIds;
        public PetGachaGoodsGroup[] GoodsGroups;
        public PetGachaGoods[] Goods;
        public PetGachaCondition[] Conditions;
    }
    [Serializable] public sealed class PetGachaGoodsGroup
    {
        public int GroupId;
        public int[] GoodsIds, ConditionIds;
        public string ConditionType;
        public bool RefreshWhenConditionChanged, HasComplexWeights;
    }
    [Serializable] public sealed class PetGachaGoods
    {
        public int Id, ItemId, Count, CurrencyItemId, Price, Inventory, TotalBuyCountLimit, RefreshCycleType;
        public bool PopConfirmDialog;
    }
    [Serializable] public sealed class PetGachaCondition
    {
        public int ClassId, Season, Days;
        public int[] ReleaseTypes, Ids;
        public string Type, Op;
        public bool Not;
    }
    [Serializable] public sealed class PetGachaSettings
    {
        public int DailyLimit, HistoryDays, HistoryLimit, WeeklyStartDayOfWeek, DailyResetHour, DailyResetMinute;
        public int QuickBuyItemId, QuickBuyCurrencyItemId, QuickBuyPrice, PetPieceItemType;
    }

    public sealed class OfficialPetGachaAssets : MonoBehaviour
    {
        public const string DefaultResourcePath = "PetGacha/pet-gacha-runtime";
        public PetGachaRuntimeCatalog Catalog { get; private set; }

        public static PetGachaRuntimeCatalog LoadDefault()
        {
            TextAsset data = Resources.Load<TextAsset>(DefaultResourcePath);
            if (data == null) throw new InvalidOperationException("Official pet gacha rules are not installed.");
            return PetGachaRuntimeCatalog.Parse(data.text);
        }
    }

    [Serializable] public sealed class PetGachaNativeModel
    {
        public int PetClassId, UIModel;
        public GameObject Prefab;
    }

    public sealed class OfficialPetGachaAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "PetGacha/official-pet-gacha-assets";
        public string Manifest;
        public PetGachaNativeModel[] NativeModels;

        public static OfficialPetGachaAssetCatalog LoadDefault()
        {
            OfficialPetGachaAssetCatalog catalog = Resources.Load<OfficialPetGachaAssetCatalog>(DefaultResourcePath);
            if (catalog == null) throw new InvalidOperationException("Official pet gacha native models are not installed.");
            return catalog;
        }

        public GameObject NativeModel(int petClassId, int uiModel)
        {
            PetGachaNativeModel entry = (NativeModels ?? Array.Empty<PetGachaNativeModel>())
                .FirstOrDefault(value => value != null && value.PetClassId == petClassId && value.UIModel == uiModel);
            return entry == null ? null : entry.Prefab;
        }

        public void Validate(PetGachaRuntimeCatalog rules)
        {
            if (Manifest != "854_156383" || rules == null || NativeModels == null)
                throw new FormatException("The official pet gacha native model catalog is missing or unsupported.");
            foreach (PetGachaPetProfile profile in rules.PetProfiles)
                if (NativeModel(profile.ClassId, profile.UIModel) == null)
                    throw new FormatException("The official native pet model is not installed: " + profile.ClassId);
        }
    }
}
