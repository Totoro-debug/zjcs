using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeFruitTreeNative;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

public static class HomeFruitTreeProduceControllerValidation
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
    private sealed class Handler : MonoBehaviour, IHomeFruitTreeHangUpHandler
    {
        public bool Ready, Tip;
        public readonly List<string> Calls = new List<string>();
        public void Init() => Calls.Add("init");
        public void Uninit() => Calls.Add("uninit");
        public bool HaveReward() { Calls.Add("have"); return Ready; }
        public void ResetData() => Calls.Add("reset");
        public bool TryInteractiveWithNotReward() { Calls.Add("tip"); return Tip; }
    }
    private sealed class Player : IHomeFurniturePlayer
    {
        public bool Owned;
        public bool HasFurniture(EFurniture furniture) => Owned && furniture == EFurniture.FruitTree;
        public bool IsMetFurnitureRepairCondition(FurnitureInfo furniture) => false;
        public bool IsMetFurnitureNoticeCondition(FurnitureInfo furniture) => false;
        public bool HasStartedStory(string name) => false;
        public event Action<EFurniture> OnRepairFurniture { add { } remove { } }
        public event Action<long> OnDebugTimeOffset { add { } remove { } }
    }
    private sealed class Services : IHomeFurnitureServices, IHomeFruitTreeProductionServices
    {
        public readonly Player Data = new Player();
        public bool Disabled;
        public bool AwardManagerAvailable { get; set; } = true;
        public int FruitId => 110;
        public int Error, Popups;
        public Func<HomeFruitTreeAward, bool> Award;
        public Action<object, int> Completion;
        public bool InMyHome => true;
        public IHomeFurniturePlayer Player => Data;
        public IHomeFurniturePlayerData PlayerData => Data;
        public FurnitureInfo GetFurnitureInfo(EFurniture furniture) => new FurnitureInfo();
        public bool CheckFunctionDisabled(EFurniture furniture) { if (furniture != EFurniture.FruitTree) throw new InvalidOperationException(); return Disabled; }
        public void ShowError(int id) => Error = id;
        public void RegisterAwardHandler(Func<HomeFruitTreeAward, bool> handler) { if (Award != null) throw new InvalidOperationException(); Award = handler; }
        public void RemoveAwardHandler(Func<HomeFruitTreeAward, bool> handler) { if (Award != handler) throw new InvalidOperationException(); Award = null; }
        public void CollectFruitTreeReward(Action<object, int> completed) => Completion = completed;
        public void PopupAward(HomeFruitTreeAward award) { if (award.Original == null) throw new InvalidOperationException(); Popups++; }
        public IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action) => throw new NotSupportedException();
        public IDisposable SubscribePostHomeChange(Action action) => throw new NotSupportedException();
        public void RegisterAwardHandler(Func<HomeFurnitureAward, bool> handler) { }
        public void RemoveAwardHandler(Func<HomeFurnitureAward, bool> handler) { }
        public void PopupRepair(EFurniture furniture, bool build) => throw new NotSupportedException();
        public void PopupPlayerBag() => throw new NotSupportedException();
        public void PopupTreasurePanel() => throw new NotSupportedException();
        public void DoStoryAction(Action action) => throw new NotSupportedException();
        public IHomeFurnitureStoryContext CreateStoryContext(string owner) => throw new NotSupportedException();
        public void PlayStory(IHomeFurnitureStoryContext context, string name) => throw new NotSupportedException();
        public void DisposeStory(IHomeFurnitureStoryContext context) => throw new NotSupportedException();
        public IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition) => throw new NotSupportedException();
        public void SystemMessage(string message) => throw new NotSupportedException();
        public bool IsFunctionLimit(int type) => throw new NotSupportedException();
        public void PopupNpcPanel(int tab = 0) => throw new NotSupportedException();
        public DateTime GetDateTime() => throw new NotSupportedException();
        public DateTime GetDailyResetTimeFromDate(DateTime value) => throw new NotSupportedException();
        public void RefreshDailyDialogues(bool byPlayer) => throw new NotSupportedException();
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Invoke(object owner, string name)
        => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    public static void Run()
    {
        GameObject owner = null;
        try
        {
            owner = new GameObject("Original production tree controller"); owner.SetActive(false);
            var services = new Services(); var handler = owner.AddComponent<Handler>();
            var controller = owner.AddComponent<HomeFruitTreeProduceFruit>();
            controller.InputOperationModule = new Input(); controller.ProductionServices = services;
            controller.GetType().GetField("mHomeFruitTreeHangUpHandlerFruitItem", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, handler);
            var switcher = WorldStateSwitcher<FurnitureBase.EFurnitureState>.FromBindings(owner, Array.Empty<IWorldStateConfig>());
            controller.Bind(EFurniture.FruitTree, switcher, null, null, .6f, 1.5f, services);
            services.AwardManagerAvailable = false; Invoke(controller, "OnEnable");
            Require(services.Award == null, "An unavailable award manager is skipped on enable.");
            services.AwardManagerAvailable = true; Invoke(controller, "OnEnable");
            Require(services.Award != null, "The original award handler registers on enable.");
            Require(!controller.TryCallFunction() && handler.Calls.Count == 0, "Unowned furniture cannot inspect or claim fruit.");
            services.Data.Owned = true; services.Disabled = true;
            Require(!controller.TryCallFunction() && services.Error == 53851 && handler.Calls.Count == 0, "Disabled furniture reports the original error before checking rewards.");
            services.Disabled = false;
            Require(!controller.TryCallFunction() && handler.Calls.Count == 1 && handler.Calls[0] == "have", "An empty tree does not claim rewards.");
            handler.Tip = true; Require(controller.TryCallTip() && handler.Calls[1] == "tip", "The no-reward interaction delegates to the original handler.");
            handler.Ready = true; Require(controller.TryCallFunction() && services.Completion != null, "Available fruit starts the local claim.");
            services.Completion(null, 3); Require(!handler.Calls.Contains("reset"), "A failed claim retains fruit effects.");
            services.Completion(null, 1243); services.Completion(null, 0);
            Require(handler.Calls.FindAll(value => value == "reset").Count == 2, "Both original completion codes reset fruit effects.");
            Require(!services.Award(new HomeFruitTreeAward { SourceOperate = 43, AwardIds = new[] { 5 } }), "Unrelated guide rewards are not intercepted.");
            Require(services.Award(new HomeFruitTreeAward { SourceOperate = 43, AwardIds = new[] { 5, 110 } }), "Guide rewards containing fruit are intercepted.");
            Require(services.Award(new HomeFruitTreeAward { SourceOperate = 22, Original = new object() }) && services.Popups == 1, "Fruit-tree awards open the original award route.");
            Require(!services.Award(new HomeFruitTreeAward { SourceOperate = 99 }), "Other award sources are not intercepted.");
            controller.UpdateState(); Require(handler.Calls.Contains("init"), "The functional state initializes the fruit handler.");
            services.AwardManagerAvailable = false; Invoke(controller, "OnDisable");
            Require(services.Award != null, "An unavailable award manager is skipped on disable.");
            services.AwardManagerAvailable = true; Invoke(controller, "OnDisable");
            Require(services.Award == null, "The award handler is removed on disable.");
            Invoke(controller, "OnDestroy"); Require(handler.Calls.Contains("uninit"), "Destroy releases the fruit handler after the base controller.");
            Debug.Log("HOME_FRUIT_TREE_PRODUCE_CONTROLLER_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally { if (owner) Object.DestroyImmediate(owner); }
    }
}
