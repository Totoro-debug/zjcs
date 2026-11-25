using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeCartWindowsUI;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using HomeViewMode = Zjcs.Official.HomeFurniture.HomeViewMode;
using AwardData = Zjcs.Official.HomeAwardUI.AwardData;
using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;
using PlayerData = Zjcs.Official.HomeCartWindowsUI.HomeCartUnlockData;

namespace Zjcs.Presentation.Home
{
    internal sealed class OfflineHomeCartServices : IHomeCartWindowServices, IHomeCartWindowPlayer,
        IHomeCartWindowResources, IProfessionSoundHost, IProfessionSoundObjectHost, IDisposable
    {
        private sealed class CartAwardMessage : IHomeHangUpAwardMessage
        {
            internal readonly List<OfflineHomeCollectedReward> Rewards = new List<OfflineHomeCollectedReward>();
            internal readonly ArrayList Data = new ArrayList();
            public IList DataList => Data;
            public object SourceData => this;
        }

        private sealed class ActionDisposable : IDisposable
        {
            private Action release;
            internal ActionDisposable(Action release) => this.release = release;
            public void Dispose() { Action action = release; release = null; action?.Invoke(); }
        }

        private sealed class CartConditionAdapter : IHomeFurnitureCondition
        {
            private readonly OfflineHomeCartServices owner;
            internal readonly HomeConditionNode Node;

            internal CartConditionAdapter(OfflineHomeCartServices owner, HomeConditionNode node)
            {
                this.owner = owner;
                Node = node;
            }

            public bool IsMet() => owner.home.Rules.IsMet(Node, owner.home.Context);
        }

        private sealed class CartConditionNoticeAdapter : IHomeFurnitureConditionNotice
        {
            private readonly OfficialHomeRules rules;
            private readonly IHomeFurnitureCondition condition;

            internal CartConditionNoticeAdapter(OfficialHomeRules rules, IHomeFurnitureCondition condition)
            {
                this.rules = rules;
                this.condition = condition;
            }

            public Action<string, object[]> OnNoticeTxtUpdate { get; set; }

            public void Refresh()
            {
                if (TryResolveLevelNotice(out int level))
                {
                    OnNoticeTxtUpdate?.Invoke("<size=50>{0}</size>级解锁", new object[] { level });
                    return;
                }
                OnNoticeTxtUpdate?.Invoke("满足条件后解锁", Array.Empty<object>());
            }

            public bool OnUpdate(float deltaTime) => false;

            public string MessageTip()
            {
                if (TryResolveLevelNotice(out int level))
                    return "等级达到" + level + "级后可解锁";
                return "尚未满足解锁条件";
            }

            private bool TryResolveLevelNotice(out int level)
            {
                level = 0;
                return condition is CartConditionAdapter adapter
                    && adapter.Node != null
                    && HomeFeatureRuntime.TryFindLevelTarget(rules, adapter.Node, out level);
            }
        }

        private sealed class CartFurnitureServicesAdapter : IHomeFurnitureServices, IHomeFurniturePlayer, IHomeFurniturePlayerData
        {
            private readonly OfflineHomeCartServices owner;

            internal CartFurnitureServicesAdapter(OfflineHomeCartServices owner) => this.owner = owner;

            public bool InMyHome => true;
            public IHomeFurniturePlayer Player => this;
            public IHomeFurniturePlayerData PlayerData => this;
            public HomeViewMode HomeViewMode => HomeViewMode.Owner;

            public IDisposable Subscribe(Action<HomeViewMode> action) => new ActionDisposable(null);
            public IDisposable Subscribe(Action<ActionEventData> action) => new ActionDisposable(null);
            public bool HasFurniture(EFurniture furniture) => owner.home.HasFurnitureForSkin((int)furniture);
            public bool CheckFunctionDisabled(EFurniture furniture) => false;
            public void PopupPlayerBag() { }
            public void PopupRepair(EFurniture furniture, bool isBuild) { }
            public void RegisterAwardHandler(Func<HomeFurnitureAward, bool> handler) { }
            public void RemoveAwardHandler(Func<HomeFurnitureAward, bool> handler) { }
            public IDisposable SubscribePostHomeChange(Action action) => new ActionDisposable(null);
            public void ShowError(int textId) => owner.SystemMessage(textId.ToString(CultureInfo.InvariantCulture));
            public void SystemMessage(string message) => owner.SystemMessage(message);
            public bool IsFunctionLimit(int functionType) => owner.home.Rules.IsFunctionLimited(functionType);
            public void PopupNpcPanel(int defaultTab = 0) { }
            public DateTime GetDateTime() => DateTime.UtcNow.AddHours(8);
            public DateTime GetDailyResetTimeFromDate(DateTime dateTime) => OfflineRefreshClock.DayStart(dateTime);
            public void RefreshDailyDialogues(bool invokeByPlayer) { }
            public bool HasStartedStory(string name) => owner.home.StoryStatus(name) > 0;
            public void DoStoryAction(Action action) => action?.Invoke();

            public FurnitureInfo GetFurnitureInfo(EFurniture furniture)
            {
                HomeFurnitureRecord row = owner.home.Rules.FurnitureOf((int)furniture);
                return new FurnitureInfo
                {
                    RepairNoticeCondition = new CartConditionAdapter(owner, row?.repairNotice),
                    UnlockCondition = new CartConditionAdapter(owner, row?.unlock),
                    RepairStoryName = row?.repairStory ?? string.Empty
                };
            }

            public bool IsMetFurnitureRepairCondition(FurnitureInfo furniture) => furniture?.UnlockCondition?.IsMet() ?? true;
            public bool IsMetFurnitureNoticeCondition(FurnitureInfo furniture) => furniture?.RepairNoticeCondition?.IsMet() ?? true;
            public IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action) => owner.SubscribeCondition(condition, action);

            public IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition)
                => new CartConditionNoticeAdapter(owner.home.Rules, condition);

            public IHomeFurnitureStoryContext CreateStoryContext(string ownerName) => null;
            public void PlayStory(IHomeFurnitureStoryContext context, string name) { }
            public void DisposeStory(IHomeFurnitureStoryContext context) { }

            public event Action<EFurniture> OnRepairFurniture
            {
                add { }
                remove { }
            }

            public event Action<long> OnDebugTimeOffset
            {
                add { }
                remove { }
            }

            public int GetFurnitureSkinId(EFurniture type) => 0;
            public int GetThemeSkinId(HomeThemeType type) => 0;
            public int GetEnvironmentSkinId(HomeEnvironmentType type) => 0;
        }

        private sealed class CartItemDataAdapter : IProfessionItemData
        {
            public int Id { get; }
            public long Count { get; }
            public IProfessionItemInfo Info { get; }

            internal CartItemDataAdapter(IProfessionItemInfo info, long count)
            {
                Info = info;
                Id = info?.ClassId ?? 0;
                Count = count;
            }

            public Quality GetQuality() => Info?.Quality ?? Quality.White;
            public bool CanExpire() => false;
        }

        private sealed class CartUpgradeBindingsAdapter : IProfessionUpgradeBindings, IProfessionUpgradePlayer
        {
            private readonly OfflineHomeCartServices owner;
            private readonly SafeAction playerUpdate = new SafeAction();

            internal CartUpgradeBindingsAdapter(OfflineHomeCartServices owner) => this.owner = owner;

            public IProfessionUpgradePlayer MainPlayer => this;
            public bool ManagerAvailable => true;
            public SafeAction OnPlayerUpdate => playerUpdate;
            public IProfessionTipDotSystem TipDotSystem => owner.home.PartnerTipDots?.System;
            public int Profession => owner.home.Profile.Save.Player.Wardrobe.Profession;
            public bool IsProfessionFunctionLimit => false;
            public bool IsProfessionUnlocked(int profession) => true;
            public int GetProfessionLevel(int profession) => 1;
            public long GetItemAmount(int itemId) => owner.home.Profile.InventoryCount(itemId);

            public event Action<IProfessionItemData, long, object> OnItemCountUpdate
            {
                add => owner.OnItemCountUpdate += value;
                remove => owner.OnItemCountUpdate -= value;
            }

#pragma warning disable 0067
            public event Action<int, int> OnProfessionUpgrade;
            public event Action<int, int> OnSkillRankRewardUpdate;
            public event Action<IProfessionItemData, int, int> OnSkillRankUp;
            public event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;
#pragma warning restore 0067

            public ProfessionDetailDefinition GetProfession(int profession) => throw new NotSupportedException();
            public int GetLevelPropId(int profession) => 0;
            public bool TryGetLevelInfo(int profession, int level, out ProfessionUpgradeCostDefinition info) { info = null; return false; }
            public IReadOnlyDictionary<int, long> GetProps(int levelPropId, int level) => new Dictionary<int, long>();
            public Dictionary<int, long> GetProfessionLevelPropDiff(int profession, int fromLevel, int toLevel) => new Dictionary<int, long>();
            public string GetPropNameText(int propType) => ProfessionPropertyConfig.GetPropTypeName((ProfessionPropertyType)propType);
            public string FormatPropValue(int propType, long value) => value.ToString(CultureInfo.InvariantCulture);
            public void InitPropItem(ProfessionPlayerPropItem item, int propType, long value, long nextValue) { }
            public int GetItemType(IProfessionItemInfo item)
                => item is ItemInfo info ? (int)info.ItemType : owner.home.Rules.Item(item?.ClassId ?? 0)?.type ?? 0;
            public long CalcAwardBadge(ProfessionDetailDefinition profession, IProfessionUpgradePlayer player) => 0L;

            public ItemIcon GetIcon(int itemId, Transform parent)
                => owner.bedAwards.GetIcon(owner.bedAwards.GetItemInfo(itemId), parent);

            public void RecycleIcon(ItemIcon icon) => owner.bedAwards.RecycleIcon(icon);
            public void Upgrade(int profession, Action onSuccess) { }
            public int MoneyId => 1;

            public void ShowLackOfItem(int itemId, long amount)
                => owner.SystemMessage(owner.home.ItemName(itemId) + "不足");

            public bool IsItemSourceFromMerge(IProfessionUpgradePlayer player, int itemId) => false;
            public long CalcCanMergeAmount(IProfessionUpgradePlayer player, int itemId, bool recursion) => 0L;
        }

        internal sealed class CartImageLoader : IProfessionImageResourceLoader
        {
            private sealed class Operation<T> : IProfessionAssetOperation<T> where T : Object
            {
                private T result;
                private bool disposed;

                internal Operation(T value) => result = value;
                public bool IsValid => !disposed;
                public int Status => result ? 2 : 3;
                public T Result => disposed ? null : result;
                public event Action<IProfessionAssetOperation<T>> Completed
                {
                    add { if (!disposed) value?.Invoke(this); }
                    remove { }
                }
                public void Dispose() { disposed = true; result = null; }
            }

            private readonly OfflineHomeCartServices owner;
            private readonly IProfessionImageResourceLoader primary;

            internal CartImageLoader(OfflineHomeCartServices owner, IProfessionImageResourceLoader primary)
            {
                this.owner = owner;
                this.primary = primary;
            }

            public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : Object
            {
                if (typeof(T) == typeof(Sprite))
                    return new Operation<T>(owner.ResolveSprite(path) as T);
                return primary != null ? primary.LoadAsset<T>(path) : new Operation<T>(null);
            }

            public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : Object => LoadAsset<T>(path);

            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : Object
                => primary != null ? primary.LoadAssetByGuid<T>(guid) : new Operation<T>(null);

            public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object => LoadAssetByGuid<T>(guid);
        }

        private readonly HomeFeatureRuntime home;
        private readonly OfficialHomeCartWindowAssetCatalog assets;
        private readonly OfflineHomeBedServices bedServices;
        private readonly OfflineHomeBedAwards bedAwards;
        private readonly IProfessionSoundHost fallbackSoundHost;
        private readonly CartFurnitureServicesAdapter furnitureServices;
        private readonly CartUpgradeBindingsAdapter upgradeBindings;
        private readonly CartImageLoader cartImageLoader;
        private readonly Dictionary<string, Sprite> fastSpritesByStem = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<EFurniture, List<HomeCartHangUpInfo>> infosByFurniture = new Dictionary<EFurniture, List<HomeCartHangUpInfo>>();
        private readonly Dictionary<int, HomeCartHangUpInfo> infosById = new Dictionary<int, HomeCartHangUpInfo>();
        private readonly List<OfflineHomeCollectedReward> pending = new List<OfflineHomeCollectedReward>();
        private readonly Dictionary<int, long> watchedCounts = new Dictionary<int, long>();
        private readonly Dictionary<int, int> observedLevels = new Dictionary<int, int>();
        private readonly HashSet<int> observedUnlocks = new HashSet<int>();
        private readonly HashSet<int> usableItems;
        private IProfessionSoundHost previousWwiseHost;
        private bool replacedWwiseHost;
        private bool disposed;

        internal OfflineHomeCartServices(HomeFeatureRuntime home, OfflineHomeBedServices bedServices,
            OfflineHomeBedAwards bedAwards, IProfessionSoundHost fallbackSoundHost)
        {
            this.home = home ?? throw new ArgumentNullException(nameof(home));
            this.bedServices = bedServices ?? throw new ArgumentNullException(nameof(bedServices));
            this.bedAwards = bedAwards ?? throw new ArgumentNullException(nameof(bedAwards));
            this.fallbackSoundHost = fallbackSoundHost;
            assets = home.CartAssets;

            bedServices.TryExtraText = TryExtraText;
            bedServices.EvaluateCondition = cond =>
                cond is IHomeFurnitureCondition c ? c.IsMet()
                : cond is HomeConditionNode node ? home.Rules.IsMet(node, home.Context)
                : true;

            var fastAssets = Zjcs.Official.HomeFastHangUpUI.OfficialHomeFastHangUpAssetCatalog.LoadDefault();
            if (fastAssets?.images != null)
            {
                foreach (var entry in fastAssets.images)
                {
                    if (entry == null || !entry.sprite || string.IsNullOrEmpty(entry.path)) continue;
                    string stem = Path.GetFileNameWithoutExtension(entry.path);
                    if (!string.IsNullOrEmpty(stem) && !fastSpritesByStem.ContainsKey(stem))
                        fastSpritesByStem[stem] = entry.sprite;
                }
            }

            this.bedAwards.TryExtraText = TryExtraText;
            this.bedAwards.ExtraSprite = ResolveSprite;
            this.bedAwards.ExtraAudioClip = ResolveClip;
            this.bedAwards.OpenItemDetail = (info, owner, required, callback) =>
            {
                string itemName = TryExtraText(GetItemNameKey(info.ClassId)) ?? home.ItemName(info.ClassId);
                string descKey = GetItemDescKey(info.ClassId);
                string itemDesc = TryExtraText(descKey);
                string message = !string.IsNullOrEmpty(itemDesc) && itemDesc != descKey
                    ? itemName + "：" + itemDesc
                    : itemName;
                SystemMessage(message);
                callback?.Invoke(null);
            };

            usableItems = new HashSet<int>();
            var bagCatalog = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
            foreach (var row in bagCatalog.Rows("item"))
            {
                if (row != null && row.TryGetValue("ClassId", out object rawId)
                    && row.TryGetValue("CanUse", out object rawCanUse)
                    && Convert.ToBoolean(rawCanUse, CultureInfo.InvariantCulture))
                {
                    usableItems.Add(Convert.ToInt32(rawId, CultureInfo.InvariantCulture));
                }
            }

            furnitureServices = new CartFurnitureServicesAdapter(this);
            upgradeBindings = new CartUpgradeBindingsAdapter(this);
            cartImageLoader = new CartImageLoader(this, bedAwards.Images);

            foreach (HomeHangUpRecord row in home.Rules.HangUps)
            {
                if (row == null) continue;
                var info = new HomeCartHangUpInfo
                {
                    Id = row.id,
                    CanLevelUp = row.canLevelUp,
                    SortOrder = row.sortOrder,
                    RewardId = row.rewardId,
                    UnlockCondition = new CartConditionAdapter(this, row.unlock),
                    NoticeCondition = new CartConditionAdapter(this, row.notice)
                };
                infosById[row.id] = info;
                var furniture = (EFurniture)row.bindFurniture;
                if (!infosByFurniture.TryGetValue(furniture, out var list))
                    infosByFurniture[furniture] = list = new List<HomeCartHangUpInfo>();
                list.Add(info);

                foreach (var upgrade in home.Rules.Upgrades(row.id))
                {
                    if (upgrade?.cost == null) continue;
                    foreach (var cost in upgrade.cost)
                        if (cost != null && cost.itemId > 0 && !watchedCounts.ContainsKey(cost.itemId))
                            watchedCounts[cost.itemId] = home.Profile.InventoryCount(cost.itemId);
                }
            }
            foreach (var list in infosByFurniture.Values)
                list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

            foreach (var def in home.Session.Definitions)
            {
                if (!def.Unlocked) continue;
                observedUnlocks.Add(def.HangUpId);
                observedLevels[def.HangUpId] = home.Session.HangUpLevel(def.HangUpId);
            }
        }

        internal OfflineHomeCartWindows Windows { get; set; }
        internal IHomeFurnitureServices FurnitureServices => furnitureServices;
        internal IProfessionUpgradeBindings UpgradeBindings => upgradeBindings;
        internal IProfessionImageResourceLoader ImageLoader => cartImageLoader;
        internal IProfessionItemIconContext ItemIconContext => bedAwards;
        internal IProfessionSoundHost SoundHost => this;

        internal void AttachGlobalSoundHost()
        {
            if (replacedWwiseHost) return;
            previousWwiseHost = OfficialWwiseEvent.Host;
            OfficialWwiseEvent.Host = this;
            replacedWwiseHost = true;
        }

        internal void DetachGlobalSoundHost()
        {
            if (!replacedWwiseHost) return;
            if (ReferenceEquals(OfficialWwiseEvent.Host, this))
                OfficialWwiseEvent.Host = previousWwiseHost;
            previousWwiseHost = null;
            replacedWwiseHost = false;
        }

        public void Tick()
        {
            if (disposed) return;
            bedServices.Tick();

            foreach (var def in home.Session.Definitions)
            {
                if (!def.Unlocked) continue;
                int level = home.Session.HangUpLevel(def.HangUpId);
                if (observedUnlocks.Add(def.HangUpId))
                {
                    observedLevels[def.HangUpId] = level;
                    OnHangUpUnlock?.Invoke(def.HangUpId);
                }
                else if (!observedLevels.TryGetValue(def.HangUpId, out int prev) || prev != level)
                {
                    observedLevels[def.HangUpId] = level;
                    OnHangUpLevelUp?.Invoke(def.HangUpId, level);
                }
            }

            foreach (int itemId in watchedCounts.Keys.ToArray())
            {
                long current = home.Profile.InventoryCount(itemId);
                long prev = watchedCounts[itemId];
                if (current == prev) continue;
                watchedCounts[itemId] = current;
                var info = bedAwards.GetItemInfo(itemId);
                OnItemCountUpdate?.Invoke(new CartItemDataAdapter(info, current), prev, null);
            }
        }

        internal string TryExtraText(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (assets.ContainsText(key)) return assets.Text(key);
            if (key.StartsWith("hang_up_", StringComparison.Ordinal) && key.EndsWith("_name", StringComparison.Ordinal))
            {
                string middle = key.Substring("hang_up_".Length, key.Length - "hang_up_".Length - "_name".Length);
                if (int.TryParse(middle, out int hangUpId))
                    return home.ItemNameForHangUp(hangUpId);
            }
            if (key.StartsWith("item_", StringComparison.Ordinal) && key.EndsWith("_name", StringComparison.Ordinal))
            {
                string middle = key.Substring("item_".Length, key.Length - "item_".Length - "_name".Length);
                if (int.TryParse(middle, out int itemId))
                {
                    string name = home.ItemName(itemId);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            return null;
        }

        private AudioClip ResolveClip(uint eventId)
        {
            if (assets != null && assets.TryGetSound(eventId, out var clip) && clip)
                return clip;
            return null;
        }

        public uint PostEvent(uint eventId, GameObject emitter)
        {
            var clip = ResolveClip(eventId);
            if (clip)
                return bedAwards.PostEvent(eventId, emitter);
            if (fallbackSoundHost != null)
                return fallbackSoundHost.PostEvent(eventId, emitter);
            return bedAwards.PostEvent(eventId, emitter);
        }

        private IProfessionSoundObjectHost SoundObjects
            => previousWwiseHost as IProfessionSoundObjectHost
                ?? fallbackSoundHost as IProfessionSoundObjectHost;

        public bool IsInitialized => SoundObjects?.IsInitialized ?? false;
        public float DefaultScalingFactor => SoundObjects?.DefaultScalingFactor ?? 1f;
        public int RegisterGameObject(GameObject emitter, string objectName)
            => SoundObjects?.RegisterGameObject(emitter, objectName) ?? 0;
        public void SetObjectPosition(GameObject emitter, Vector3 position, Vector3 forward, Vector3 up)
            => SoundObjects?.SetObjectPosition(emitter, position, forward, up);
        public void InitializeEmptyEnvironment(GameObject emitter, Collider sourceCollider)
            => SoundObjects?.InitializeEmptyEnvironment(emitter, sourceCollider);
        public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position)
            => SoundObjects?.UpdateEmptyEnvironment(emitter, position);
        public void SetDefaultListeners(GameObject emitter)
            => SoundObjects?.SetDefaultListeners(emitter);
        public void SetScalingFactor(GameObject emitter, float factor)
            => SoundObjects?.SetScalingFactor(emitter, factor);
        public void UnregisterGameObject(GameObject emitter)
            => SoundObjects?.UnregisterGameObject(emitter);

        internal Sprite ResolveSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (assets != null)
            {
                if (assets.TryGetSprite(path, out var sprite) && sprite)
                    return sprite;
                if (path == "ItemIcon/Home/item_4266" || path == "Assets/Image/ItemIcon/Home/item_4266")
                    return assets.cartSkin4266Icon;
            }

            if (bedAwards.Images != null)
            {
                string full = path.StartsWith("Assets/Image/", StringComparison.Ordinal) ? path : "Assets/Image/" + path;
                using var handle = bedAwards.Images.LoadAsset<Sprite>(full);
                if (handle.Result) return handle.Result;
            }

            string fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName))
            {
                Sprite catalogSprite = home.Catalog.FindSprite(fileName);
                if (catalogSprite) return catalogSprite;

                string stem = Path.GetFileNameWithoutExtension(fileName);
                int dash = stem.IndexOf('-');
                if (dash > 0) stem = stem.Substring(0, dash);
                if (fastSpritesByStem.TryGetValue(stem, out var stemSprite) && stemSprite)
                    return stemSprite;
            }

            return null;
        }

        internal List<AwardData> ToAwardDataList(IReadOnlyList<OfflineHomeCollectedReward> rewards)
        {
            var list = new List<AwardData>();
            if (rewards == null) return list;
            foreach (var reward in rewards)
            {
                if (reward == null || reward.Count <= 0) continue;
                list.Add(new AwardData
                {
                    Catalog = bedAwards,
                    AwardId = reward.ItemId,
                    Amount = reward.Count
                });
            }
            return list;
        }

        // IHomeCartWindowServices & IHomeCartWindowPlayer
        public IHomeCartWindowPlayer Player => this;
        public IHomeCartWindowResources Resources => this;
        public IHomeAwardItemServices AwardItemServices => bedAwards;
        public IProfessionRuleTipData RuleTips => assets;
        public LanguageType CurrentLanguage => LanguageType.zh_CN;
        public int HangUpCalcMaxLevelUp => 10;

        public int Level => home.Profile.Save.Player.Level;
        public int SubRank => home.Profile.Save.Player.SubRank;
        public event Action<int> OnHangUpUnlock;
        public event Action<int, int> OnHangUpLevelUp;
        public event Action<IProfessionItemData, long, object> OnItemCountUpdate;

        public bool TryGetUnlockSingleHangUpData(int hangUpId, out PlayerData data)
        {
            if (home.Session.IsHangUpUnlocked(hangUpId))
            {
                var saved = home.Session.Save.Income.FirstOrDefault(v => v.HangUpId == hangUpId);
                data = new PlayerData(hangUpId, home.Session.HangUpLevel(hangUpId), saved?.StoredSeconds ?? 0);
                return true;
            }
            data = default;
            return false;
        }

        public int GetHangUpLevel(int hangUpId) => bedServices.GetHangUpLevel(hangUpId);
        public int GetHangUpMaxLevel() => home.Rules.FurnitureLevelUpLimit(bedServices.GetBlessingLevel());
        public int GetBlessingLevel() => bedServices.GetBlessingLevel();
        public bool HasItems(Dictionary<int, long> items)
        {
            if (items == null) return true;
            foreach (KeyValuePair<int, long> pair in items)
                if (pair.Value > 0 && home.Profile.InventoryCount(pair.Key) < pair.Value) return false;
            return true;
        }
        public bool IsFunctionLimit(int functionType) => home.Rules.IsFunctionLimited(functionType);
        public int CalcLevelUpNum(int hangUpId, int maxLevel) => home.CalcCartLevelUpNum(hangUpId, maxLevel);

        public HomeCartWindowResourceHandle<T> LoadAsset<T>(string path) where T : Object
        {
            if (typeof(T) == typeof(Sprite))
                return new HomeCartWindowResourceHandle<T>(ResolveSprite(path) as T);
            using var op = bedServices.Resources.LoadAsset<T>(path);
            return new HomeCartWindowResourceHandle<T>(op.Result);
        }

        public string Text(string key) => bedServices.Text(key);
        public string TextFormatted(string key, params object[] args) => bedServices.TextFormatted(key, args);
        public string GetItemNameKey(int classId) => "item_" + classId + "_name";
        public string GetItemDescKey(int classId) => "item_" + classId + "_desc";
        public string GetHangUpName(int hangUpId) => "hang_up_" + hangUpId + "_name";
        public string GetCartHangUpIconPath(int hangUpId) => "UI/Furniture/ImgItem_Cart_" + hangUpId;
        public string GetCartItemBgPath(int hangUpId) => "UI/Furniture/ImgBgCost_" + hangUpId;
        public HomeWindowFurnitureInfo GetFurnitureInfo(EFurniture furniture) => bedServices.GetFurnitureInfo(furniture);

        public HomeCartHangUpInfo GetHangUpInfo(int hangUpId)
            => infosById.TryGetValue(hangUpId, out var info)
                ? info
                : throw new InvalidOperationException("Selected cart hang-up configuration is unavailable: " + hangUpId);

        public bool TryGetHangUpInfos(EFurniture furniture, out IReadOnlyList<HomeCartHangUpInfo> infos)
        {
            if (infosByFurniture.TryGetValue(furniture, out var list) && list.Count > 0)
            {
                infos = list;
                return true;
            }
            infos = Array.Empty<HomeCartHangUpInfo>();
            return false;
        }

        public HomeWindowItemInfo GetHangUpRewardItem(int hangUpId) => bedServices.GetHangUpRewardItem(hangUpId);
        public ItemInfo GetAwardItemInfo(int itemId) => bedAwards.GetItemInfo(itemId);
        public bool IsIgnoreShowAward(int itemId) => bedAwards.IsIgnoreShowAward(itemId);
        public bool CanUseItem(int itemId) => usableItems.Contains(itemId);
        public bool CanGetHangUpReward(int hangUpId) => bedServices.CanGetHangUpReward(hangUpId);
        public double GetHangUpReward(int hangUpId, double hours, int level) => bedServices.GetHangUpReward(hangUpId, hours, level);
        public double GetHangUpReward(int hangUpId, double hours) => bedServices.GetHangUpReward(hangUpId, hours);
        public long GetHangUpMaxRewardStore(int hangUpId) => bedServices.GetHangUpMaxRewardStore(hangUpId);
        public long GetHangUpAwardCount(int hangUpId, out double hangUpSeconds) => bedServices.GetHangUpAwardCount(hangUpId, out hangUpSeconds);
        public int GetUpgradeMaxLevel(int hangUpId) => home.Rules.MaxHangUpLevel(hangUpId);

        public Dictionary<int, long> GetUpgradeMaterialCost(int hangUpId, int targetLevel)
        {
            var cost = new Dictionary<int, long>();
            HomeHangUpUpgradeRecord record = home.Rules.Upgrade(hangUpId, targetLevel);
            if (record?.cost != null)
            {
                foreach (HomeCostEntry entry in record.cost)
                {
                    if (entry == null || entry.itemId <= 0 || entry.count <= 0) continue;
                    cost[entry.itemId] = cost.TryGetValue(entry.itemId, out long prev)
                        ? checked(prev + entry.count)
                        : entry.count;
                }
            }
            return cost;
        }

        public IReadOnlyList<HomeCartEquipPreviewItem> GetEquipPreviewItems(int hangUpId)
            => home.CalcCartEquipPreview(hangUpId);

        public bool IsConditionMet(IHomeFurnitureCondition condition) => condition?.IsMet() ?? true;
        public IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action)
        {
            bedServices.RegisterCondition(condition, action);
            return new ActionDisposable(() => bedServices.DeregisterCondition(condition, action));
        }

        public IDisposable SubscribeTips(Action<HangUpTipLevelChangeData> action) => bedServices.SubscribeTips(action);
        public int GetTipLevel(EFurniture furniture) => bedServices.GetTipLevel(furniture);

        public IDisposable SubscribeGlobalPointerDown(Action<Vector2> callback)
        {
            if (callback == null || Windows?.OutputDetails == null) return new ActionDisposable(null);
            Windows.OutputDetails.HookGlobalPointerDown(callback);
            return new ActionDisposable(() => Windows?.OutputDetails?.UnhookGlobalPointerDown(callback));
        }

        public Camera GetUiCamera() => Windows != null ? Windows.CanvasCamera : null;

        public FurnitureCartHangUpItem InstantiateCartHangUpItem(FurnitureCartHangUpItem prefab, Transform parent)
            => Windows.CloneObject(prefab.gameObject, parent).GetComponent<FurnitureCartHangUpItem>();

        public HangUpLevelUpSwitchItem InstantiateLevelUpSwitchItem(HangUpLevelUpSwitchItem prefab, Transform parent)
            => Windows.CloneObject(prefab.gameObject, parent).GetComponent<HangUpLevelUpSwitchItem>();

        public bool CheckFunctionDisabled(int serverFunctionType) => false;
        public void BroadcastErrCode(int errCode) => SystemMessage(errCode.ToString(CultureInfo.InvariantCulture));
        public void SystemMessage(string message) => bedServices.SystemMessage(message);

        public void UnlockHangUp(int hangUpId, Action onSuccess)
        {
            if (!home.TryUnlockHangUp(hangUpId)) return;
            observedUnlocks.Add(hangUpId);
            observedLevels[hangUpId] = home.Session.HangUpLevel(hangUpId);
            OnHangUpUnlock?.Invoke(hangUpId);
            onSuccess?.Invoke();
        }

        public void LevelUpHangUp(int hangUpId, int level)
        {
            HomeLevelUpResult result = home.TryLevelUpHangUp(hangUpId, level);
            if (!result.Success)
            {
                SystemMessage(result.Error ?? "upgrade_failed");
                return;
            }
            observedLevels[hangUpId] = result.Level;
            OnHangUpLevelUp?.Invoke(hangUpId, result.Level);
        }

        public void Collect(IReadOnlyList<int> hangUpIds, Action<IHomeHangUpAwardMessage> onSuccess)
        {
            var response = new CartAwardMessage();
            if (hangUpIds != null)
            {
                foreach (int id in hangUpIds)
                {
                    OfflineHomeCollectedReward reward = home.PrepareHomeCollection(id);
                    if (reward == null || reward.Count <= 0) continue;
                    pending.Add(reward);
                    response.Rewards.Add(reward);
                    response.Data.Add(new AwardData
                    {
                        Catalog = bedAwards,
                        AwardId = reward.ItemId,
                        Amount = reward.Count
                    });
                }
            }
            onSuccess?.Invoke(response);
        }

        public void AwardCollected(IList dataList, object sourceData)
        {
            if (!(sourceData is CartAwardMessage msg) || !ReferenceEquals(dataList, msg.Data))
                throw new InvalidOperationException("A committed local cart award response is required.");
            home.AwardPreparedHomeCollections();
        }

        public void MergeAward(IList dataList)
        {
            // AwardPanel merges AwardData entries during Presentation via MergeAwardData.
        }

        public void MergeAwardData(List<AwardData> awardList)
        {
            if (awardList == null || awardList.Count <= 1)
            {
                awardList?.Sort();
                return;
            }

            var merged = new List<AwardData>(awardList.Count);
            foreach (AwardData entry in awardList)
            {
                if (entry == null || entry.Amount <= 0) continue;
                bool combined = false;
                if (entry.IsCanMerge())
                {
                    for (int i = 0; i < merged.Count; i++)
                    {
                        if (merged[i].TryMerge(entry))
                        {
                            combined = true;
                            break;
                        }
                    }
                }
                if (!combined) merged.Add(entry);
            }
            merged.Sort();
            awardList.Clear();
            awardList.AddRange(merged);
        }

        public void PublishCollected()
        {
            OfflineHomeCollectedReward[] published = pending.ToArray();
            pending.Clear();
            foreach (OfflineHomeCollectedReward reward in published)
                home.PublishHomeCollection(reward);
            bedServices.Output.Refresh();
        }

        public void PopupAwardPanel(IHomeHangUpAwardMessage response)
            => Windows.ShowAwardPanel(response);

        public void PopupAwardList(IList dataList)
        {
            var rewards = new List<AwardData>();
            if (dataList != null)
            {
                foreach (object item in dataList)
                    if (item is AwardData data && data.Amount > 0)
                        rewards.Add(data);
            }
            if (rewards.Count > 0)
                Windows.ShowAwardPanel(rewards);
        }

        public void OpenCartItemDetail(int hangUpId, Vector3 mousePosition)
            => Windows.ShowCartDetail(hangUpId, mousePosition);

        public void OpenHangUpLevelUp(int hangUpId, EFurniture furniture)
            => Windows.ShowHangUpLevelUp(hangUpId, furniture);

        public void OpenCartRule() => Windows.ShowCartRule();
        public void EnableAwardItemDetailUseButton(GameObject detailWindow) { }
        public void Close(GameObject window) => Windows.Close(window);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DetachGlobalSoundHost();
            pending.Clear();
            watchedCounts.Clear();
            observedLevels.Clear();
            observedUnlocks.Clear();
            OnHangUpUnlock = null;
            OnHangUpLevelUp = null;
            OnItemCountUpdate = null;
        }
    }
}
