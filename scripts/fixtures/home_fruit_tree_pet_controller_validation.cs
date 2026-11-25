using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeFruitTreeNative;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

public static class HomeFruitTreePetControllerValidation
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
        public bool Owned, Repair, Notice;
        public readonly List<string> Calls;
        public Player(List<string> calls) { Calls = calls; }
        public bool HasFurniture(EFurniture furniture) { Calls.Add("has:" + (int)furniture); return Owned; }
        public bool IsMetFurnitureRepairCondition(FurnitureInfo furniture) => Repair;
        public bool IsMetFurnitureNoticeCondition(FurnitureInfo furniture) => Notice;
        public bool HasStartedStory(string name) => false;
        public event Action<EFurniture> OnRepairFurniture { add { } remove { } }
        public event Action<long> OnDebugTimeOffset { add { } remove { } }
    }
    private sealed class Services : IHomeFurnitureServices, IHomePetTreeFeatureServices
    {
        public readonly List<string> Calls = new List<string>();
        public readonly Player Data;
        public bool PetDisabled, FurnitureDisabled;
        public Services() { Data = new Player(Calls); }
        public bool InMyHome => true;
        public IHomeFurniturePlayer Player => Data;
        public IHomeFurniturePlayerData PlayerData => Data;
        public FurnitureInfo GetFurnitureInfo(EFurniture furniture) => new FurnitureInfo();
        public bool CheckFunctionDisabled(EFurniture furniture) { Calls.Add("furniture:" + (int)furniture); return FurnitureDisabled; }
        public bool IsPetGachaDisabled() { Calls.Add("pet"); return PetDisabled; }
        public void ShowError(int id) => Calls.Add("error:" + id);
        public void PopupPetGacha() => Calls.Add("popup");
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
    private sealed class State : IWorldStateConfig
    {
        private readonly List<string> calls;
        public State(List<string> calls) { this.calls = calls; }
        public void SetState(WorldStateConfigValue state) => calls.Add("state:" + (int)state);
        public void Clear() { }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Calls(Services services, string expected)
    {
        string actual = string.Join(",", services.Calls);
        Require(actual == expected, "Original branch order: expected " + expected + "; got " + actual);
        services.Calls.Clear();
    }
    public static void Run()
    {
        GameObject owner = null;
        try
        {
            owner = new GameObject("Original pet tree controller"); owner.SetActive(false);
            var services = new Services { PetDisabled = true, FurnitureDisabled = true };
            var controller = owner.AddComponent<HomeFruitTreePet>();
            controller.InputOperationModule = new Input();
            controller.PetTreeFeatureServices = services;
            var switcher = WorldStateSwitcher<FurnitureBase.EFurnitureState>.FromBindings(owner, new[] { new State(services.Calls) });
            controller.Bind(EFurniture.FruitTree2, switcher, null, null, .6f, 1.5f, services);
            Require(!controller.TryCallFunction(), "An unowned tree cannot open gacha."); Calls(services, "has:11");
            services.Data.Owned = true;
            Require(!controller.TryCallFunction(), "A disabled pet gacha is rejected."); Calls(services, "has:11,pet,error:53814");
            services.PetDisabled = false;
            Require(!controller.TryCallFunction(), "A disabled furniture function is rejected."); Calls(services, "has:11,pet,furniture:11,error:53851");
            services.FurnitureDisabled = false;
            Require(controller.TryCallFunction(), "An owned and enabled tree opens gacha."); Calls(services, "has:11,pet,furniture:11,popup");
            Require(!controller.TryCallTip(), "The original sub-furniture default has no secondary tip.");
            controller.GetStateIsLock = _ => true;
            Require(controller.GetState() == FurnitureBase.EFurnitureState.Locked, "Parent lock overrides ownership."); Calls(services, "");
            controller.GetStateIsLock = _ => false;
            controller.OnStateChange = () => services.Calls.Add("changed");
            controller.UpdateState(); Calls(services, "has:11,state:3,changed");
            controller.UpdateState(); Calls(services, "has:11,state:3");
            services.Data.Owned = false; services.Data.Repair = true;
            controller.UpdateState(); Calls(services, "has:11,state:2,changed");
            Debug.Log("HOME_FRUIT_TREE_PET_CONTROLLER_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally { if (owner) Object.DestroyImmediate(owner); }
    }
}
