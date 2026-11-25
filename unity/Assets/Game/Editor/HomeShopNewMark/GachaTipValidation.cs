using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.HomeShopNewMark
{
    public static class GachaTipValidation
    {
        sealed class Manager : ITipDotNodeManager
        {
            public Action OnAddScopeDataCollector { get; set; }
            public Action OnRemoveScopeDataCollector { get; set; }
            public readonly HashSet<TipDotConditionNodeBase> Dirty = new HashSet<TipDotConditionNodeBase>();
            public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key) => null;
            public TipDotNodeBase FindNode(TipDotNodeKey key) => null;
            public void AddDirtyConditionNode(TipDotConditionNodeBase node) => Dirty.Add(node);
            public void RemoveDirtyConditionNode(TipDotConditionNodeBase node) => Dirty.Remove(node);
        }

        sealed class Subscription : IDisposable
        {
            readonly Action dispose;
            public Subscription(Action dispose) { this.dispose = dispose; }
            public void Dispose() => dispose();
        }

        sealed class Services : IHomeGachaTipServices
        {
            public readonly Dictionary<GachaType, HomeTipGachaInfo> Rows = new Dictionary<GachaType, HomeTipGachaInfo>();
            public readonly Dictionary<int, long> Amounts = new Dictionary<int, long>();
            public readonly Dictionary<int, long> Costs = new Dictionary<int, long>();
            public readonly HashSet<GachaType> Locked = new HashSet<GachaType>();
            public readonly HashSet<int> DailyLimited = new HashSet<int>(), ChestLimited = new HashSet<int>();
            public readonly Dictionary<int, Action> Conditions = new Dictionary<int, Action>();
            public event Action OnGacha;
            public event Action<int> OnItemCountUpdate;
            public HomeTipGachaInfo GetGacha(GachaType type) => Rows[type];
            public bool TryGetChestCost(int chestId, int itemId, out long cost) => Costs.TryGetValue(chestId, out cost);
            public bool IsGachaFunctionLimited(GachaType type) => Locked.Contains(type);
            public bool IsGachaDailyCountLimited(int type) => DailyLimited.Contains(type);
            public bool IsChestFunctionLimited(int chestId) => ChestLimited.Contains(chestId);
            public long GetItemAmount(int itemId) => Amounts.TryGetValue(itemId, out long count) ? count : 0;
            public IDisposable SubscribeChestCondition(int chestId, Action changed)
            { Conditions.Add(chestId, changed); return new Subscription(() => Conditions.Remove(chestId)); }
            public void Item(int item) => OnItemCountUpdate?.Invoke(item);
            public void Gacha() => OnGacha?.Invoke();
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        public static void Run()
        {
            var manager = new Manager();
            var services = new Services();
            services.Rows[GachaType.Pet] = new HomeTipGachaInfo { GachaType = GachaType.Pet, GachaClassType = 1, ChestId = 7, CostItemId = 8, ShowRedDotMinCount = 2 };
            services.Rows[GachaType.Friend] = new HomeTipGachaInfo { GachaType = GachaType.Friend, GachaClassType = 2, ChestId = 9, CostItemId = 10, ShowRedDotMinCount = 1 };
            services.Amounts[8] = 2; services.Amounts[10] = 100; services.Costs[7] = 2; services.Costs[9] = 2;
            var node = new TipDotGachaTipMinCountConditionNode { Services = services, NodeManager = manager,
                GachaTypes = new List<GachaType> { GachaType.Pet, GachaType.Friend } };
            node.Init();
            Require(!node.CheckShowTip(), "The first eligible pool below its minimum returns false immediately.");
            services.Locked.Add(GachaType.Pet);
            Require(node.CheckShowTip(), "A limited first pool is skipped and the next eligible pool is checked.");
            services.DailyLimited.Add(2);
            Require(!node.CheckShowTip(), "Daily limits suppress the eligible pool.");
            services.DailyLimited.Clear(); services.ChestLimited.Add(9);
            Require(!node.CheckShowTip(), "Chest limits suppress the eligible pool.");
            services.ChestLimited.Clear(); services.Locked.Clear(); services.Amounts[8] = 4;
            Require(node.CheckShowTip(), "Configured minimum includes an exact boundary.");
            node.OverriderMinCountForShow = 3;
            Require(!node.CheckShowTip(), "Positive minimum override replaces the configured threshold.");
            node.OverriderMinCountForShow = 1; node.OverriderMaxCountForShow = 1;
            Require(!node.CheckShowTip(), "Positive maximum override excludes counts above its boundary.");
            node.IsActive = true; manager.Dirty.Clear();
            services.Item(11); Require(manager.Dirty.Count == 0, "Unrelated items must not dirty the node.");
            services.Item(8); Require(manager.Dirty.Contains(node), "Cost changes must dirty the node.");
            manager.Dirty.Clear(); services.Conditions[7](); Require(manager.Dirty.Contains(node), "Chest condition updates must dirty the node.");
            manager.Dirty.Clear(); services.Gacha(); Require(manager.Dirty.Contains(node), "Gacha events must dirty the node.");
            var clone = (TipDotGachaTipMinCountConditionNode)node.Clone();
            clone.GachaTypes.Clear();
            Require(node.GachaTypes.Count == 2 && clone.Services == null, "Clone must copy the list and not bind live services.");
            node.Release(); manager.Dirty.Clear(); services.Gacha(); services.Item(8);
            Require(services.Conditions.Count == 0 && manager.Dirty.Count == 0, "Release must dispose all event subscriptions.");
            services.Costs[7] = 0; services.Locked.Add(GachaType.Friend);
            var zeroCost = new TipDotGachaTipMinCountConditionNode { Services = services, NodeManager = manager,
                GachaTypes = new List<GachaType> { GachaType.Pet, GachaType.Friend } };
            zeroCost.Init();
            Require(!zeroCost.CheckShowTip(), "A zero-cost pool must not divide by zero or show a tip.");
            services.Locked.Clear();
            Require(zeroCost.CheckShowTip(), "A zero-cost pool is skipped before checking the next pool.");
            zeroCost.Release();
            Debug.Log("HOME_GACHA_TIP_VALIDATION_PASSED");
        }
    }
}
