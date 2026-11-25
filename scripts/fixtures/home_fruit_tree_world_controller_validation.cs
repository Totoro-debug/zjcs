using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeFurnitureProxy;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.HomeFruitTreeNative;
using Zjcs.Official.HomeFruitTreeWorld;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

public static class HomeFruitTreeWorldControllerValidation
{
    private sealed class Input : IInputOperationModule
    {
        public bool Enable { get; set; }
        public void HookOnInput<T>(Action<T> callback) where T : IInputOperationResult { }
        public void UnhookOnInput<T>(Action<T> callback) where T : IInputOperationResult { }
        public long Block<T>() where T : IInputOperationResult => 0;
        public long Block(InputOperationLayer layer) => 0;
        public void Unblock(long signature) { }
    }
    private sealed class Player : IHomeFurniturePlayer
    {
        public readonly HashSet<EFurniture> Owned = new HashSet<EFurniture>();
        public bool HasFurniture(EFurniture furniture) => Owned.Contains(furniture);
        public bool IsMetFurnitureRepairCondition(FurnitureInfo furniture) => false;
        public bool IsMetFurnitureNoticeCondition(FurnitureInfo furniture) => false;
        public bool HasStartedStory(string name) => false;
        public event Action<EFurniture> OnRepairFurniture { add { } remove { } }
        public event Action<long> OnDebugTimeOffset { add { } remove { } }
    }
    private sealed class Services : IHomeFurnitureServices
    {
        public readonly Player Data = new Player();
        public readonly List<string> Calls = new List<string>();
        public bool InMyHome => true;
        public IHomeFurniturePlayer Player => Data;
        public IHomeFurniturePlayerData PlayerData => Data;
        public FurnitureInfo GetFurnitureInfo(EFurniture furniture) => throw new NotSupportedException();
        public IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action) => throw new NotSupportedException();
        public IDisposable SubscribePostHomeChange(Action action) => throw new NotSupportedException();
        public void RegisterAwardHandler(Func<HomeFurnitureAward, bool> handler) { }
        public void RemoveAwardHandler(Func<HomeFurnitureAward, bool> handler) { }
        public bool CheckFunctionDisabled(EFurniture furniture) => throw new NotSupportedException();
        public void ShowError(int id) => throw new NotSupportedException();
        public void PopupRepair(EFurniture furniture, bool build) => throw new NotSupportedException();
        public void PopupPlayerBag() => throw new NotSupportedException();
        public void PopupTreasurePanel() => throw new NotSupportedException();
        public void DoStoryAction(Action action) => throw new NotSupportedException();
        public IHomeFurnitureStoryContext CreateStoryContext(string owner) => throw new NotSupportedException();
        public void PlayStory(IHomeFurnitureStoryContext context, string name) => throw new NotSupportedException();
        public void DisposeStory(IHomeFurnitureStoryContext context) => throw new NotSupportedException();
        public IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition) => throw new NotSupportedException();
        public void SystemMessage(string message) => Calls.Add("message:" + message);
        public bool IsFunctionLimit(int type) => throw new NotSupportedException();
        public void PopupNpcPanel(int tab = 0) => throw new NotSupportedException();
        public DateTime GetDateTime() => throw new NotSupportedException();
        public DateTime GetDailyResetTimeFromDate(DateTime value) => throw new NotSupportedException();
        public void RefreshDailyDialogues(bool byPlayer) => throw new NotSupportedException();
    }
    private sealed class Sub : HomeFruitTreeSubFurniture
    {
        public FurnitureBase.EFurnitureState Value;
        public List<string> Calls;
        public string Label;
        public bool FunctionResult, TipResult;
        public void Configure(EFurniture furniture, List<string> calls, string label)
        { mFurniture = furniture; Calls = calls; Label = label; }
        public override EFurnitureState GetState()
            => GetStateIsLock != null && GetStateIsLock(this) ? EFurnitureState.Locked : Value;
        public override void UpdateState() { Calls.Add("update:" + Label); }
        public override bool TryCallFunction() { Calls.Add("function:" + Label); return FunctionResult; }
        public override bool TryCallTip() { Calls.Add("tip:" + Label); return TipResult; }
    }
    private sealed class State : IWorldStateConfig
    {
        public int Current = -1;
        public void SetState(WorldStateConfigValue state) => Current = state;
        public void Clear() { }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Set(object owner, string name, object value)
        => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Invoke(object owner, string name)
        => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    private static void Calls(Services services, string expected)
    {
        string actual = string.Join(",", services.Calls);
        Require(actual == expected, "Original routing: expected " + expected + "; got " + actual);
        services.Calls.Clear();
    }
    public static void Run()
    {
        GameObject owner = null, first = null, second = null;
        try
        {
            owner = new GameObject("Original fruit tree"); owner.SetActive(false);
            first = new GameObject("Production branch"); first.SetActive(false);
            second = new GameObject("Pet branch"); second.SetActive(false);
            var services = new Services(); var state = new State();
            var production = first.AddComponent<Sub>(); production.Configure(EFurniture.FruitTree, services.Calls, "production");
            var pet = second.AddComponent<Sub>(); pet.Configure(EFurniture.FruitTree2, services.Calls, "pet");
            var proxy = owner.AddComponent<HomeFurnitureProxy>();
            proxy.FurnitureProxyDict = new MonoDictionary<EFurniture, FurnitureBase>();
            proxy.FurnitureProxyDict.Add(EFurniture.FruitTree, production);
            proxy.FurnitureProxyDict.Add(EFurniture.FruitTree2, pet);
            IFurnitureProxy<FurnitureBase> proxyContract = proxy;
            Require(ReferenceEquals(proxyContract.GetComponent(EFurniture.FruitTree), production)
                && ReferenceEquals(proxyContract.GetComponent(EFurniture.FruitTree2), pet)
                && proxyContract.GetComponent(EFurniture.Desk) == null,
                "The original furniture proxy resolves both branches and returns null for absent furniture.");
            var tree = owner.AddComponent<HomeFruitTree>(); tree.InputOperationModule = new Input();
            tree.FurnitureServices = services; tree.Localize = key => "localized:" + key;
            Set(tree, "mSubFurnitureList", new List<HomeFruitTreeSubFurniture> { production, pet });
            Type enumType = tree.GetType().GetNestedType("FruitTreeState", BindingFlags.NonPublic);
            Type switchType = typeof(WorldStateSwitcher<>).MakeGenericType(enumType);
            object switcher = switchType.GetMethod("FromBindings").Invoke(null, new object[] { owner, new IWorldStateConfig[] { state } });
            Set(tree, "mStateSwitcher", switcher);
            Invoke(tree, "Awake"); Invoke(tree, "OnEnable");
            Require(state.Current == 0, "Both locked branches select the original None state.");
            Invoke(tree, "OnValidClick"); Calls(services, "message:localized:HomeFruitTree.InitialStateTip");
            production.Value = FurnitureBase.EFurnitureState.CanRepair; Invoke(tree, "OnEnable");
            Require(state.Current == 1, "The first repairable branch selects Repair1.");
            Invoke(tree, "OnValidClick"); Calls(services, "");
            production.Value = FurnitureBase.EFurnitureState.Functional; Invoke(tree, "OnEnable");
            Require(state.Current == 2, "The first functional branch selects Function1.");
            pet.FunctionResult = true; Invoke(tree, "OnValidClick"); Calls(services, "function:production,function:pet");
            pet.FunctionResult = false; production.TipResult = true; Invoke(tree, "OnValidClick");
            Calls(services, "function:production,function:pet,tip:production");
            production.Value = FurnitureBase.EFurnitureState.CanRepair; pet.Value = FurnitureBase.EFurnitureState.RepairNotice;
            Require(production.GetState() == FurnitureBase.EFurnitureState.Locked, "A later repairable branch locks the earlier unowned branch.");
            Invoke(tree, "OnEnable"); Require(state.Current == 3, "The later repair notice selects Repair2.");
            services.Data.Owned.Add(EFurniture.FruitTree); Require(production.GetState() == FurnitureBase.EFurnitureState.CanRepair, "An owned earlier branch is not forced locked.");
            pet.Value = FurnitureBase.EFurnitureState.Functional; Invoke(tree, "OnEnable");
            Require(state.Current == 4, "A functional later branch selects Function2.");
            production.OnStateChange(); Calls(services, "update:production,update:pet");
            Require(state.Current == 4, "A child state change refreshes the parent after updating both children.");
            Debug.Log("HOME_FRUIT_TREE_WORLD_CONTROLLER_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally { if (owner) Object.DestroyImmediate(owner); if (first) Object.DestroyImmediate(first); if (second) Object.DestroyImmediate(second); }
    }
}
