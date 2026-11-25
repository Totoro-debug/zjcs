using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.HomeActivityTipDots;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.ProfessionUI;

public static class HomeActivityGachaTipValidation
{
    private sealed class Manager : ITipDotNodeManager
    {
        public int Dirty;
        public Action OnAddScopeDataCollector { get; set; }
        public Action OnRemoveScopeDataCollector { get; set; }
        public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key) => null;
        public TipDotNodeBase FindNode(TipDotNodeKey key) => null;
        public void AddDirtyConditionNode(TipDotConditionNodeBase node) => Dirty++;
        public void RemoveDirtyConditionNode(TipDotConditionNodeBase node) { }
    }
    private sealed class Lease : IDisposable
    {
        private readonly Action dispose;
        public Lease(Action dispose) { this.dispose = dispose; }
        public void Dispose() => dispose();
    }
    private sealed class Services : IHomeActivityGachaTipServices
    {
        public bool HasPlayer => true;
        public bool HideActivity, HideGacha, Active = true;
        public Action<HomeTipActivity> Setting;
        public Action<IHomeActivityNoRedDotMessage> Message;
        public event Action<HomeTipActivity> OnActivityOpen;
        public event Action<HomeTipActivity> OnActivityClose;
        public event Action<ActivityType> OnActivitiesUpdate;
        public event Action<int> OnItemCountUpdate;
        public event Action<int> OnSpecialGachaEnd;
        public readonly Dictionary<int, long> Costs = new Dictionary<int, long> { [1] = 2, [2] = 5 };
        public readonly Dictionary<int, long> Items = new Dictionary<int, long> { [1] = 21, [2] = 31 };
        public IDictionary<int, long> GetCosts(int id) { Require(id == 7, "Original gacha identity must reach configuration."); return Costs; }
        public HashSet<int> GetCanAwardItems(int id) => new HashSet<int> { 3 };
        public IDisposable SubscribeNoRedDotSetting(Action<HomeTipActivity> callback) { Setting += callback; return new Lease(() => Setting -= callback); }
        public IDisposable SubscribeNoRedDotWithMessage(Action<IHomeActivityNoRedDotMessage> callback) { Message += callback; return new Lease(() => Message -= callback); }
        public bool IsActivityNoRedDotSet(ActivityType type, int id) => HideActivity;
        public bool IsNoRedDotWithGachaSet(HomeActivityNoRedDotGacha data)
        { Require(data.ActivityId == 9 && data.GachaId == 7 && data.ActivityType == ActivityType.ActivityGacha, "Original setting key must include activity and gacha identity."); return HideGacha; }
        public bool HasActivity(ActivityType type, int id) => Active;
        public long GetItemAmount(int id) => Items[id];
        public void Item(int id) => OnItemCountUpdate?.Invoke(id);
        public void End(int id) => OnSpecialGachaEnd?.Invoke(id);
        public void Open(ActivityType type, int id) => OnActivityOpen?.Invoke(new HomeTipActivity(type, id));
        public void Close(ActivityType type, int id) => OnActivityClose?.Invoke(new HomeTipActivity(type, id));
        public void Update(ActivityType type) => OnActivitiesUpdate?.Invoke(type);
        public int EventSubscribers => (OnItemCountUpdate?.GetInvocationList().Length ?? 0) + (OnSpecialGachaEnd?.GetInvocationList().Length ?? 0)
            + (OnActivityOpen?.GetInvocationList().Length ?? 0) + (OnActivityClose?.GetInvocationList().Length ?? 0) + (OnActivitiesUpdate?.GetInvocationList().Length ?? 0);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        try
        {
            var identity = new ActivityGachaNodeInstanceInfo(9, 7);
            Require(identity is ActivityNodeInstanceInfo && identity.Equals(new ActivityGachaNodeInstanceInfo(9, 7))
                && !identity.Equals(new ActivityGachaNodeInstanceInfo(9, 8)), "Original activity gacha instance identity and inheritance.");
            var services = new Services(); var manager = new Manager();
            var node = new TipDotActivityGachaRemainCostBlueDotConditionNode { Services = services, NodeManager = manager, Times = 2 };
            node.NodeKey = new TipDotNodeKey("FortuneTree", "Gacha", identity);
            node.Init(); node.IsActive = true; manager.Dirty = 0;
            Require(node.CheckShowTip() && node.TipDotInfo.Data == "3", "The minimum affordable count across both costs must be reported.");
            services.HideActivity = true; Require(!node.CheckShowTip(), "Activity-level mute hides the tip."); services.HideActivity = false;
            services.HideGacha = true; Require(!node.CheckShowTip(), "Gacha-level mute hides the tip."); services.HideGacha = false;
            services.Active = false; Require(!node.CheckShowTip(), "Closed activity hides the tip."); services.Active = true;
            services.Items[2] = 9; Require(!node.CheckShowTip() && node.TipDotInfo.Data == "0", "Unaffordable cost clears the tip and reports zero.");
            services.Costs.Clear(); Require(!node.CheckShowTip() && node.TipDotInfo.Data == "-1", "An empty original cost set reports minus one.");
            services.Costs[1] = 2; services.Costs[2] = 5;
            services.Item(8); services.Open(ActivityType.ActivityGacha, 8); services.Setting(new HomeTipActivity(ActivityType.BattlePass, 9));
            Require(manager.Dirty == 0, "Unrelated item, activity and setting changes must be ignored.");
            services.Item(1); services.Item(3); services.End(123); services.Open(ActivityType.ActivityGacha, 9); services.Close(ActivityType.ActivityGacha, 9);
            services.Update(ActivityType.ActivityGacha); services.Setting(new HomeTipActivity(ActivityType.ActivityGacha, 9));
            services.Message(new HomeActivityNoRedDotGacha { ActivityType = ActivityType.ActivityGacha, ActivityId = 9, GachaId = 7 });
            Require(manager.Dirty == 8, "Matching original subscriptions must dirty the node, including all gacha-end events.");
            Require(((TipDotActivityGachaRemainCostBlueDotConditionNode)node.Clone()).Times == 2, "Clone must preserve Times.");
            node.Release(); Require(services.Setting == null && services.Message == null && services.EventSubscribers == 0, "Release must remove all original subscriptions.");
            Debug.Log("HOME_ACTIVITY_GACHA_TIP_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
