using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeTipDots
{
    public sealed class OfflineHomeTipDotServices : IHomeTipDotServices, IHomeTipDotPlayer, IDisposable
    {
        private sealed class Subscription : IDisposable
        {
            private Action release;
            internal Subscription(Action release) => this.release = release;
            public void Dispose() { Action action = release; release = null; action?.Invoke(); }
        }

        private readonly Func<OfflineMainlineSave> readSave;
        private readonly OfflineHomeIncomeSession session;
        private readonly OfficialHomeRules rules;
        private readonly Func<DateTime> clock;
        private readonly Func<int> readBlessingLevel;
        private readonly Func<HomeConditionContext> readConditionContext;
        private readonly Dictionary<EFurniture, List<HomeTipFastHangUpInfo>> byFurniture =
            new Dictionary<EFurniture, List<HomeTipFastHangUpInfo>>();
        private readonly Dictionary<int, HomeTipHangUpInfo> hangUpInfosById =
            new Dictionary<int, HomeTipHangUpInfo>();
        private readonly Dictionary<EFurniture, List<HomeTipHangUpInfo>> hangUpInfosByFurniture =
            new Dictionary<EFurniture, List<HomeTipHangUpInfo>>();
        private readonly HashSet<int> fastHangUpItems = new HashSet<int>();
        private readonly HashSet<int> watchedItems = new HashSet<int>();
        private readonly Dictionary<int, long> observedItemCounts = new Dictionary<int, long>();
        private readonly HashSet<int> observedUnlockedHangUps = new HashSet<int>();
        private readonly Dictionary<int, int> observedHangUpLevels = new Dictionary<int, int>();
        private readonly List<Action> newDaySubscribers = new List<Action>();
        private readonly List<Action> treasureSettingSubscribers = new List<Action>();
        private bool autoMergeWhenLevelUp;
        private bool awakeAutoMergeEnable;
        private DateTime observedDay;
        private int observedPlayerLevel;
        private int observedBlessingLevel;
        private bool disposed;

        public OfflineHomeTipDotServices(Func<OfflineMainlineSave> readSave, OfflineHomeIncomeSession session,
            OfficialHomeRules rules, Func<DateTime> clock = null,
            Func<int> readBlessingLevel = null, Func<HomeConditionContext> readConditionContext = null)
        {
            this.readSave = readSave ?? throw new ArgumentNullException(nameof(readSave));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.clock = clock ?? (() => DateTime.Now);
            this.readBlessingLevel = readBlessingLevel ?? (() => 1);
            this.readConditionContext = readConditionContext ?? (() => this.rules.CurrentContext);
            observedDay = OfflineRefreshClock.DayStart(this.clock());
            observedPlayerLevel = this.readSave()?.Player?.Level ?? 1;
            observedBlessingLevel = this.readBlessingLevel();

            foreach (HomeFastHangUpRecord fast in rules.Data.fastHangUps ?? Array.Empty<HomeFastHangUpRecord>())
            {
                if (fast == null) continue;
                var furniture = (EFurniture)fast.bindFurniture;
                if (!byFurniture.TryGetValue(furniture, out List<HomeTipFastHangUpInfo> list))
                {
                    list = new List<HomeTipFastHangUpInfo>();
                    byFurniture.Add(furniture, list);
                }
                var itemDic = new Dictionary<int, int>();
                foreach (HomeFastHangUpItem item in fast.items ?? Array.Empty<HomeFastHangUpItem>())
                {
                    if (item == null || item.itemId <= 0) continue;
                    int hours = checked((int)Math.Round(item.hours));
                    itemDic[item.itemId] = hours;
                    fastHangUpItems.Add(item.itemId);
                    watchedItems.Add(item.itemId);
                }
                list.Add(new HomeTipFastHangUpInfo
                {
                    FastHangUpId = fast.fastHangUpId,
                    HangUpIdList = (fast.hangUpIdList ?? Array.Empty<int>()).ToList(),
                    FastHangUpItemDic = itemDic,
                });
            }

            foreach (HomeHangUpRecord row in rules.HangUps ?? Array.Empty<HomeHangUpRecord>())
            {
                if (row == null || row.levelRuleId <= 0) continue;
                var info = new HomeTipHangUpInfo { Id = row.id, CanLevelUp = row.canLevelUp };
                hangUpInfosById[row.id] = info;
                var furniture = (EFurniture)row.bindFurniture;
                if (!hangUpInfosByFurniture.TryGetValue(furniture, out List<HomeTipHangUpInfo> infoList))
                {
                    infoList = new List<HomeTipHangUpInfo>();
                    hangUpInfosByFurniture.Add(furniture, infoList);
                }
                infoList.Add(info);
                foreach (HomeHangUpUpgradeRecord upgrade in rules.Upgrades(row.id) ?? Array.Empty<HomeHangUpUpgradeRecord>())
                {
                    if (upgrade?.cost == null) continue;
                    foreach (HomeCostEntry cost in upgrade.cost)
                        if (cost != null && cost.itemId > 0) watchedItems.Add(cost.itemId);
                }
            }

            foreach (int itemId in watchedItems)
                observedItemCounts[itemId] = GetItemAmount(itemId);
            foreach (OfflineHomeIncomeDefinition definition in session.Definitions)
            {
                if (!definition.Unlocked) continue;
                observedUnlockedHangUps.Add(definition.HangUpId);
                observedHangUpLevels[definition.HangUpId] = session.HangUpLevel(definition.HangUpId);
            }
        }

        public IHomeTipDotPlayer Player => this;

        public event Action<int> OnHangUpUnlock;
        public event Action<int, int> OnHangUpLevelUp;
        public event Action<int> OnFastHangUp;
        public event Action<int> OnLevelUpdate;
        public event Action OnGoddessUpdate;
        public event Action<long> OnDebugTimeOffset;
        public event Action<HomeTipItemData, long, object> OnItemCountUpdate;
        public event Action<HomeTipItemData, bool> OnTreasureRefresh;

        public Func<ElementType, List<TreasureRedDotEnum>, bool, bool, bool> TreasureRedDotEvaluator { get; set; }
        public Func<List<TreasureRedDotEnum>, bool, bool, bool> AnyTreasureRedDotEvaluator { get; set; }
        public Func<ElementType, bool> TreasurePieceRedDotEvaluator { get; set; }
        public Func<int, bool> IsItemEffectTreasurePromoteEvaluator { get; set; }
        public bool AutoMergeWhenLevelUp
        {
            get => autoMergeWhenLevelUp;
            set { if (autoMergeWhenLevelUp == value) return; autoMergeWhenLevelUp = value; NotifyTreasureSettingUpdate(); }
        }
        public bool AwakeAutoMergeEnable
        {
            get => awakeAutoMergeEnable;
            set { if (awakeAutoMergeEnable == value) return; awakeAutoMergeEnable = value; NotifyTreasureSettingUpdate(); }
        }

        public bool TreasureRedDot(ElementType elementType, List<TreasureRedDotEnum> tipTypes, bool autoMergeWhenLevelUp, bool awakeAutoMergeEnable)
            => TreasureRedDotEvaluator != null && TreasureRedDotEvaluator(elementType, tipTypes, autoMergeWhenLevelUp, awakeAutoMergeEnable);

        public bool AnyTreasureRedDot(List<TreasureRedDotEnum> redDots, bool autoMergeWhenLevelUp, bool awakeAutoMergeEnable)
            => AnyTreasureRedDotEvaluator != null && AnyTreasureRedDotEvaluator(redDots, autoMergeWhenLevelUp, awakeAutoMergeEnable);

        public bool TreasurePieceRedDot(ElementType elementType)
            => TreasurePieceRedDotEvaluator != null && TreasurePieceRedDotEvaluator(elementType);

        public bool IsItemEffectTreasurePromote(HomeTipItemData itemData)
            => itemData?.Info != null && IsItemEffectTreasurePromoteEvaluator != null && IsItemEffectTreasurePromoteEvaluator(itemData.Info.ClassId);

        public IDisposable SubscribeTreasureSettingUpdate(Action callback)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeTipDotServices));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            treasureSettingSubscribers.Add(callback);
            return new Subscription(() => treasureSettingSubscribers.Remove(callback));
        }

        private void NotifyTreasureSettingUpdate()
        {
            if (disposed) return;
            foreach (Action callback in treasureSettingSubscribers.ToArray()) callback();
        }

        public void NotifyTreasureRefresh(int classId = 0, bool isNew = false)
        {
            if (disposed) return;
            OnTreasureRefresh?.Invoke(new HomeTipItemData { Info = new HomeTipItemInfo { ClassId = classId } }, isNew);
        }

        public void BindNode(TipDotNodeBase node)
        {
            if (node is IHomeTipDotNode homeNode)
                homeNode.Services = this;
        }

        public bool TryGetFastHangUpInfo(EFurniture furniture, out List<HomeTipFastHangUpInfo> list)
        {
            if (byFurniture.TryGetValue(furniture, out List<HomeTipFastHangUpInfo> found) && found.Count > 0)
            {
                list = found;
                return true;
            }
            list = null;
            return false;
        }

        public bool IsFastHangUpItem(int itemId) => fastHangUpItems.Contains(itemId);

        public EFurniture GetHangUpBindFurniture(int hangUpId)
        {
            HomeHangUpRecord row = rules.HangUp(hangUpId);
            return row == null ? (EFurniture)(-1) : (EFurniture)row.bindFurniture;
        }

        public IDisposable SubscribeEnterNewDay(Action callback)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeTipDotServices));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            newDaySubscribers.Add(callback);
            return new Subscription(() => newDaySubscribers.Remove(callback));
        }

        public bool CanFreeFastHangUp(int fastHangUpId) => session.CanFreeFastForward(fastHangUpId);

        public long GetItemAmount(int itemId)
        {
            OfflinePlayerProgress player = readSave()?.Player;
            if (player?.Inventory == null) return 0;
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value != null && value.ItemId == itemId);
            return OfflineItemAutoIncrease.Count(stack, DateTime.UtcNow);
        }

        public bool HasUnlockedHangUp(int hangUpId)
            => session.Definitions.Any(value => value.HangUpId == hangUpId && value.Unlocked);

        public bool TryGetUnlockSingleHangUpData(int hangUpId, out int level)
        {
            if (HasUnlockedHangUp(hangUpId))
            {
                level = session.HangUpLevel(hangUpId);
                return true;
            }
            level = 0;
            return false;
        }

        public int GetHangUpMaxLevel()
            => rules.FurnitureLevelUpLimit(readBlessingLevel());

        public bool HasItems(Dictionary<int, long> costs)
        {
            if (costs == null || costs.Count == 0) return true;
            foreach (KeyValuePair<int, long> pair in costs)
                if (pair.Value > 0 && GetItemAmount(pair.Key) < pair.Value) return false;
            return true;
        }

        public bool CheckHangUpUnlockMet(EFurniture furniture)
        {
            HomeConditionContext context = readConditionContext?.Invoke() ?? rules.CurrentContext;
            foreach (HomeHangUpRecord row in rules.HangUpsOf((int)furniture))
            {
                if (row == null || row.levelRuleId <= 0 || row.initAutoUnlock) continue;
                if (session.IsHangUpUnlocked(row.id) || HasUnlockedHangUp(row.id)) continue;
                if (rules.IsMet(row.unlock, context)) return true;
            }
            return false;
        }

        public bool TryGetHangUpInfo(int hangUpId, out HomeTipHangUpInfo info)
            => hangUpInfosById.TryGetValue(hangUpId, out info);

        public bool TryGetHangUpInfos(EFurniture furniture, out List<HomeTipHangUpInfo> list)
            => hangUpInfosByFurniture.TryGetValue(furniture, out list);

        public int GetHangUpUpgradeMaxLevel(int hangUpId)
            => rules.MaxHangUpLevel(hangUpId);

        public Dictionary<int, long> GetHangUpUpgradeCosts(int hangUpId, int nextLevel)
        {
            HomeHangUpUpgradeRecord upgrade = rules.Upgrade(hangUpId, nextLevel);
            if (upgrade?.cost == null) return null;
            var costs = new Dictionary<int, long>();
            foreach (HomeCostEntry entry in upgrade.cost)
            {
                if (entry == null || entry.itemId <= 0 || entry.count <= 0) continue;
                costs[entry.itemId] = costs.TryGetValue(entry.itemId, out long prev)
                    ? checked(prev + entry.count) : entry.count;
            }
            return costs;
        }

        public void NotifyFastHangUp(int fastHangUpId)
        {
            if (disposed) return;
            OnFastHangUp?.Invoke(fastHangUpId);
        }

        public void NotifyHangUpUnlock(int hangUpId)
        {
            if (disposed) return;
            if (observedUnlockedHangUps.Add(hangUpId) || HasUnlockedHangUp(hangUpId))
            {
                observedHangUpLevels[hangUpId] = session.HangUpLevel(hangUpId);
                OnHangUpUnlock?.Invoke(hangUpId);
            }
        }

        public void NotifyHangUpLevelUp(int hangUpId, int level)
        {
            if (disposed) return;
            observedHangUpLevels[hangUpId] = level;
            OnHangUpLevelUp?.Invoke(hangUpId, level);
        }

        public void NotifyDebugTimeOffset(long offset)
        {
            if (disposed) return;
            OnDebugTimeOffset?.Invoke(offset);
        }

        public void Synchronize()
        {
            if (disposed) return;
            foreach (OfflineHomeIncomeDefinition definition in session.Definitions)
            {
                if (!definition.Unlocked) continue;
                int currentLevel = session.HangUpLevel(definition.HangUpId);
                if (observedUnlockedHangUps.Add(definition.HangUpId))
                {
                    observedHangUpLevels[definition.HangUpId] = currentLevel;
                    OnHangUpUnlock?.Invoke(definition.HangUpId);
                }
                else if (!observedHangUpLevels.TryGetValue(definition.HangUpId, out int prevLevel) || prevLevel != currentLevel)
                {
                    observedHangUpLevels[definition.HangUpId] = currentLevel;
                    OnHangUpLevelUp?.Invoke(definition.HangUpId, currentLevel);
                }
            }
            int playerLevel = readSave()?.Player?.Level ?? 1;
            if (playerLevel != observedPlayerLevel)
            {
                observedPlayerLevel = playerLevel;
                OnLevelUpdate?.Invoke(playerLevel);
            }
            int blessingLevel = readBlessingLevel();
            if (blessingLevel != observedBlessingLevel)
            {
                observedBlessingLevel = blessingLevel;
                OnGoddessUpdate?.Invoke();
            }
            foreach (int itemId in watchedItems)
            {
                long current = GetItemAmount(itemId);
                long previous = observedItemCounts.TryGetValue(itemId, out long prior) ? prior : 0L;
                if (current != previous)
                {
                    observedItemCounts[itemId] = current;
                    OnItemCountUpdate?.Invoke(new HomeTipItemData
                    {
                        Info = new HomeTipItemInfo { ClassId = itemId }
                    }, previous, null);
                }
            }
            DateTime currentDay = OfflineRefreshClock.DayStart(clock());
            if (currentDay != observedDay)
            {
                observedDay = currentDay;
                foreach (Action callback in newDaySubscribers.ToArray()) callback?.Invoke();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            newDaySubscribers.Clear();
            treasureSettingSubscribers.Clear();
            OnHangUpUnlock = null;
            OnHangUpLevelUp = null;
            OnFastHangUp = null;
            OnLevelUpdate = null;
            OnGoddessUpdate = null;
            OnDebugTimeOffset = null;
            OnItemCountUpdate = null;
            OnTreasureRefresh = null;
        }
    }
}
