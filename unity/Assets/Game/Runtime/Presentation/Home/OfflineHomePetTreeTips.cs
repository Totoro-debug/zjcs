using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomePetGachaUI;
using Zjcs.Official.HomeTipDots;
using Zjcs.Offline.Progression;
using Rules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;

namespace Zjcs.Presentation.Home
{
    // Supplies committed local progress to the original legacy indicator. The
    // indicator retains its group exclusion, visibility and subscription logic.
    internal sealed class OfflineHomePetTreeTips : IHomeAchievementRedDotServices, IHomeGachaBlueDotServices, IDisposable
    {
        private readonly OfflinePetGachaService service;
        private readonly Func<bool> groupLimited;
        private readonly Func<int, bool> chestLimited;
        private readonly Func<int, long> itemCount;
        private readonly HomeTipGachaInfo gacha;
        private readonly HashSet<Action> chestChanged = new HashSet<Action>();
        private readonly Dictionary<int, int> groups;
        private readonly HashSet<int> showAllTypes;
        private readonly List<int> groupedIds;
        private readonly HashSet<Action> conditionChanged = new HashSet<Action>();
        private HashSet<(int group, int index)> claims;
        private long draws, score;
        private bool limited, chestLimit, dailyLimit, disposed;
        private long material;

        internal OfflineHomePetTreeTips(OfflinePetGachaService service, Func<bool> groupLimited,
            Func<int, bool> chestLimited, Func<int, long> itemCount)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.groupLimited = groupLimited ?? throw new ArgumentNullException(nameof(groupLimited));
            this.chestLimited = chestLimited ?? throw new ArgumentNullException(nameof(chestLimited));
            this.itemCount = itemCount ?? throw new ArgumentNullException(nameof(itemCount));
            var config = service.Catalog.Gacha;
            gacha = new HomeTipGachaInfo { GachaType = (GachaType)config.GachaType,
                GachaClassType = config.GachaClassType, ChestId = config.ChestId,
                CostItemId = config.CostItemId, ShowRedDotMinCount = config.ShowRedDotMinCount };
            var asset = Resources.Load<TextAsset>("Home/pet-achievement-hints");
            if (!asset) throw new InvalidOperationException("Original pet achievement hint configuration is missing.");
            var data = JObject.Parse(asset.text);
            groups = ((JArray)data["keys"]).ToDictionary(row => (int)row["type"], row => (int)row["group"]);
            showAllTypes = new HashSet<int>(data["showAllTypes"].ToObject<int[]>());
            groupedIds = data["groupedAchievementIds"].ToObject<List<int>>();
            foreach (var key in groups)
                if (!service.Catalog.Achievements.Where(row => row.AchievementType == key.Key).Any()
                    || service.Catalog.Achievements.Any(row => row.AchievementType == key.Key && row.Group != key.Value))
                    throw new InvalidOperationException("Pet achievement key and local progress group differ.");
            draws = service.State.TotalDrawCount;
            score = service.State.Score;
            claims = ReadClaims();
            limited = groupLimited();
            chestLimit = chestLimited(gacha.ChestId);
            dailyLimit = IsGachaDailyCountLimited(gacha.GachaClassType);
            material = itemCount(gacha.CostItemId);
        }

        public bool HasPlayer => !disposed;
        public object CreateKey(int achievementType, string jsonParam)
        {
            if (!groups.ContainsKey(achievementType) || !string.IsNullOrEmpty(jsonParam))
                throw new NotSupportedException("Unbound original pet achievement key: " + achievementType);
            return new Rules.AchievementDataKeyNormal((Rules.AchievementType)achievementType);
        }
        private static int TypeOf(object key) => (int)((Rules.AchievementDataKey)key).AchievementType;
        public bool ShowAllForType(object key) => showAllTypes.Contains(TypeOf(key));
        public IEnumerable<object> GetKeysOfSameType(object key) => groups.Keys.Where(type => type == TypeOf(key))
            .Select(type => CreateKey(type, null));
        public bool KeysEqual(object left, object right) => Equals(left, right);
        public int GroupId(object key) => groups[TypeOf(key)];
        public List<int> GetAllAchievementGroupIds() => new List<int>(groupedIds);
        public bool IsAchievementGroupLimited() => groupLimited();
        public bool HasAvailableReward(object key)
        {
            return service.HasAvailableAchievementReward(GroupId(key));
        }
        public bool HasAvailableReward(List<object> keys) => keys.Any(HasAvailableReward);
        public IDisposable SubscribeAchievementGroupLimit(Action changed)
        {
            if (changed != null) conditionChanged.Add(changed);
            return new Subscription(() => conditionChanged.Remove(changed));
        }
        public event Action<int> OnRewardRecorded;
        public event Action<object> OnAchievementUpdated;
        public event Action OnRewardsRecorded;
        public HomeTipGachaInfo GetGacha(GachaType type) => type == gacha.GachaType ? gacha : null;
        public bool TryGetChestCost(int chestId, int itemId, out long cost)
        {
            var entry = chestId == gacha.ChestId ? service.Catalog.Chest.CostItems.FirstOrDefault(row => row.ItemId == itemId) : null;
            cost = entry?.Count ?? 0;
            return entry != null;
        }
        public bool IsGachaFunctionLimited(GachaType type) => GetGacha(type) == null || chestLimited(gacha.ChestId);
        public bool IsGachaDailyCountLimited(int classType) => classType != gacha.GachaClassType
            || service.DailyCount(DateTime.Now) >= service.Catalog.Settings.DailyLimit;
        public bool IsChestFunctionLimited(int chestId) => chestLimited(chestId);
        public long GetItemAmount(int itemId) => itemCount(itemId);
        public IDisposable SubscribeChestCondition(int chestId, Action changed)
        {
            if (chestId != gacha.ChestId) throw new NotSupportedException("Unbound native pet chest: " + chestId);
            if (changed != null) chestChanged.Add(changed);
            return new Subscription(() => chestChanged.Remove(changed));
        }
        public event Action OnGacha;
        public event Action<int> OnItemCountUpdate;

        internal void Synchronize()
        {
            if (disposed) return;
            if (draws != service.State.TotalDrawCount)
            {
                draws = service.State.TotalDrawCount;
                OnAchievementUpdated?.Invoke(CreateKey(service.Catalog.Gacha.CountAchievementType, null));
                OnGacha?.Invoke();
            }
            if (score != service.State.Score)
            {
                score = service.State.Score;
                OnAchievementUpdated?.Invoke(CreateKey(service.Catalog.Gacha.ScoreAchievementType, null));
            }
            var next = ReadClaims();
            foreach (var claim in next.Except(claims)) OnRewardRecorded?.Invoke(claim.group);
            if (claims.Except(next).Any()) OnRewardsRecorded?.Invoke();
            claims = next;
            bool currentLimit = groupLimited();
            if (limited != currentLimit)
            {
                limited = currentLimit;
                foreach (Action changed in conditionChanged.ToArray()) changed();
            }
            long currentMaterial = itemCount(gacha.CostItemId);
            if (material != currentMaterial)
            {
                material = currentMaterial;
                OnItemCountUpdate?.Invoke(gacha.CostItemId);
            }
            bool currentChest = chestLimited(gacha.ChestId);
            bool currentDaily = IsGachaDailyCountLimited(gacha.GachaClassType);
            if (chestLimit != currentChest || dailyLimit != currentDaily)
            {
                chestLimit = currentChest;
                dailyLimit = currentDaily;
                foreach (Action changed in chestChanged.ToArray()) changed();
                OnGacha?.Invoke();
            }
        }
        private HashSet<(int group, int index)> ReadClaims() => new HashSet<(int, int)>(
            service.State.AchievementClaims.Select(row => (row.Group, row.Index)));
        public void Dispose()
        {
            disposed = true;
            conditionChanged.Clear();
            chestChanged.Clear();
            OnRewardRecorded = null;
            OnAchievementUpdated = null;
            OnRewardsRecorded = null;
            OnGacha = null;
            OnItemCountUpdate = null;
        }
        private sealed class Subscription : IDisposable
        {
            private Action release;
            internal Subscription(Action release) => this.release = release;
            public void Dispose() { Action action = release; release = null; action?.Invoke(); }
        }
    }
}
