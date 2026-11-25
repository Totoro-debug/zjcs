using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePetGachaUI;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

public static class HomePetGachaBlueDotValidation
{
    private sealed class Tips : IProfessionTipDotSystem
    {
        public bool IsShowTipDot => true;
        public bool TempLimitShowTip => false;
        public Action OnTipDotShowChanged { get; set; }
        public SafeAction OnTipDotSystemInited { get; set; }
        public SafeAction OnTipDotSystemPreClear { get; set; }
        public TipDotGraphType? GetGraphType(string graphName) => null;
    }

    private sealed class PlayerUi : IHomePlayerUiServices
    {
        public bool HasPlayer => true;
        public bool ManagerAvailable => false;
        public UnityEvent OnPlayerUpdate { get; } = new UnityEvent();
        public IProfessionTipDotSystem TipDotSystem { get; } = new Tips();
    }

    private sealed class Gacha : IHomeGachaBlueDotServices
    {
        public bool PlayerAvailable = true;
        public bool HasPlayer => PlayerAvailable;
        public readonly Dictionary<GachaType, HomeTipGachaInfo> Rows = new Dictionary<GachaType, HomeTipGachaInfo>();
        public readonly Dictionary<int, long> Costs = new Dictionary<int, long>();
        public readonly Dictionary<int, long> Amounts = new Dictionary<int, long>();
        public readonly HashSet<GachaType> Limited = new HashSet<GachaType>();
        public readonly HashSet<int> Daily = new HashSet<int>(), Chest = new HashSet<int>();
        public event Action OnGacha;
        public event Action<int> OnItemCountUpdate;
        public int GachaSubscribers => OnGacha?.GetInvocationList().Length ?? 0;
        public HomeTipGachaInfo GetGacha(GachaType type) => Rows.TryGetValue(type, out var row) ? row : null;
        public bool TryGetChestCost(int chestId, int itemId, out long cost) => Costs.TryGetValue(chestId, out cost);
        public bool IsGachaFunctionLimited(GachaType type) => Limited.Contains(type);
        public bool IsGachaDailyCountLimited(int classType) => Daily.Contains(classType);
        public bool IsChestFunctionLimited(int chestId) => Chest.Contains(chestId);
        public long GetItemAmount(int itemId) => Amounts.TryGetValue(itemId, out long count) ? count : 0;
        public IDisposable SubscribeChestCondition(int chestId, Action changed) => throw new InvalidOperationException("Legacy component does not subscribe to chest conditions.");
        public void Item(int id) => OnItemCountUpdate?.Invoke(id);
        public void Draw() => OnGacha?.Invoke();
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static void Run()
    {
        GameObject owner = null, dot = null;
        try
        {
            owner = new GameObject("Official pet tree gacha indicator"); owner.SetActive(false);
            dot = new GameObject("Official indicator content");
            var services = new Gacha();
            services.Rows[GachaType.Pet] = new HomeTipGachaInfo { GachaType = GachaType.Pet, GachaClassType = 1, ChestId = 7, CostItemId = 8 };
            services.Rows[GachaType.Friend] = new HomeTipGachaInfo { GachaType = GachaType.Friend, GachaClassType = 2, ChestId = 9, CostItemId = 10 };
            services.Costs[7] = 2; services.Costs[9] = 2;
            services.Amounts[8] = 2; services.Amounts[10] = 20;
            var indicator = owner.AddComponent<GachaBlueDotForSetupMinCount>();
            indicator.PlayerServices = new PlayerUi(); indicator.GachaServices = services; indicator.myRedDotObj = dot;
            typeof(GachaBlueDotBase).GetField("mGachaTypes", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(indicator, new List<GachaType> { GachaType.Pet, GachaType.Friend, GachaType.Skill });
            typeof(GachaBlueDotBase).GetMethod("OnEnableAndInited", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(indicator, null);
            Require(indicator.ShowRedDot(), "A later eligible pool at the exact threshold must show the legacy indicator.");
            indicator.Refresh(); Require(dot.activeSelf, "The original red dot is shown.");
            services.Daily.Add(2); services.Draw(); Require(!dot.activeSelf, "The daily limit hides the only eligible pool.");
            services.Daily.Clear(); services.Chest.Add(9); services.Draw(); Require(!dot.activeSelf, "A chest function limit hides the eligible pool.");
            services.Chest.Clear();
            services.Amounts[10] = 18; services.Item(123); Require(!dot.activeSelf, "Unrelated item changes do not refresh the indicator.");
            services.Item(10); Require(!dot.activeSelf, "The amount below the threshold does not show the indicator.");
            services.Amounts[10] = 20; services.Item(10); Require(dot.activeSelf, "The tracked item update refreshes at the exact threshold.");
            typeof(GachaBlueDotBase).GetMethod("OnDisableAndInited", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(indicator, null);
            services.Amounts[10] = 0; services.Draw(); services.Item(10);
            Require(dot.activeSelf, "Disabled components release player event subscriptions.");
            services.Costs[7] = 0; services.Amounts[8] = 999;
            typeof(GachaBlueDotBase).GetMethod("OnEnableAndInited", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(indicator, null);
            Require(!indicator.ShowRedDot(), "A zero-cost pool is skipped without division by zero.");
            typeof(GachaBlueDotBase).GetMethod("OnDisableAndInited", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(indicator, null);
            typeof(GachaBlueDotBase).GetMethod("OnEnableAndInited", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(indicator, null);
            services.PlayerAvailable = false;
            typeof(GachaBlueDotBase).GetMethod("OnDisableAndInited", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(indicator, null);
            Require(services.GachaSubscribers == 1, "Original disable skips player event removal after the player disappears.");
            services.PlayerAvailable = true;
            typeof(GachaBlueDotBase).GetMethod("OnDisableAndInited", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(indicator, null);
            Require(services.GachaSubscribers == 0, "The remaining test subscription can be released when the player is present.");
            Debug.Log("HOME_PET_GACHA_BLUE_DOT_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally { if (owner) Object.DestroyImmediate(owner); if (dot) Object.DestroyImmediate(dot); }
    }
}
