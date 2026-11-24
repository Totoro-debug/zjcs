using System;
using Zjcs.Official.HomeUI;

namespace Zjcs.Official.HomeFurniture
{
    // Ports for the original furniture controllers. Conditions, configuration,
    // player events, notices and stories belong to their existing local owners.
    public interface IHomeFurnitureCondition
    {
        bool IsMet();
    }

    public sealed class FurnitureInfo
    {
        public IHomeFurnitureCondition RepairNoticeCondition;
        public IHomeFurnitureCondition UnlockCondition;
        public string RepairStoryName;
    }

    public interface IHomeFurniturePlayerData
    {
        bool HasFurniture(EFurniture furniture);
        bool IsMetFurnitureRepairCondition(FurnitureInfo furniture);
        bool IsMetFurnitureNoticeCondition(FurnitureInfo furniture);
    }

    public interface IHomeFurniturePlayer : IHomeFurniturePlayerData
    {
        bool HasStartedStory(string storyName);
        event Action<EFurniture> OnRepairFurniture;
        event Action<long> OnDebugTimeOffset;
    }

    public interface IHomeFurnitureConditionNotice
    {
        Action<string, object[]> OnNoticeTxtUpdate { get; set; }
        void Refresh();
        bool OnUpdate(float deltaTime);
        string MessageTip();
    }

    // Only the original SourceOperate == RepairFurniture test is used here.
    // The reward owner retains the complete award and routes this projection.
    public readonly struct HomeFurnitureAward
    {
        public readonly bool IsRepairFurniture;
        public HomeFurnitureAward(bool isRepairFurniture) => IsRepairFurniture = isRepairFurniture;
    }

    public interface IHomeFurnitureStoryContext { }

    public interface IHomeFurnitureServices
    {
        bool InMyHome { get; }
        IHomeFurniturePlayer Player { get; }
        IHomeFurniturePlayerData PlayerData { get; }
        FurnitureInfo GetFurnitureInfo(EFurniture furniture);
        IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action);
        IDisposable SubscribePostHomeChange(Action action);
        void RegisterAwardHandler(Func<HomeFurnitureAward, bool> handler);
        void RemoveAwardHandler(Func<HomeFurnitureAward, bool> handler);
        bool CheckFunctionDisabled(EFurniture furniture);
        void ShowError(int textId);
        void PopupRepair(EFurniture furniture, bool isBuild);
        void PopupPlayerBag();
        void DoStoryAction(Action action);
        IHomeFurnitureStoryContext CreateStoryContext(string owner);
        void PlayStory(IHomeFurnitureStoryContext context, string storyName);
        void DisposeStory(IHomeFurnitureStoryContext context);
        IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition);
        void SystemMessage(string message);
        bool IsFunctionLimit(int functionType);
        void PopupNpcPanel(int defaultTab = 0);
        DateTime GetDateTime();
        DateTime GetDailyResetTimeFromDate(DateTime dateTime);
        void RefreshDailyDialogues(bool invokeByPlayer);
    }

    public interface IHomeNpcBubbleTipServices
    {
        IDisposable SubscribeDialogueChanged(Action action);
        IDisposable SubscribePostHomeChange(Action action);
        bool TryFindActiveDialogueNpcClassId(out int npcClassId);
        string GetNpcIconPath(int npcClassId);
        UnityEngine.Sprite LoadNpcIcon(string iconPath);
        void UnloadNpcIcon(string iconPath);
    }
}
