using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFunctionMenuUI
{
    public sealed class HomeMenuFunctionLimitInfo { public IHomeHudCondition Condition; }
    public enum HomeMenuSettingStyle { Login, Normal }
    public interface IHomeMenuPlayer
    {
        bool IsFunctionLimit(FunctionType type);
        event Action<int> OnLevelUpdate;
        event Action<int> OnSubRankUpdate;
    }
    public interface IHomeMenuPanel { void Init(); }
    public interface IHomeMenuAchievementPanel { void Init(List<object> definitions); }
    public interface IHomeMenuCollectBookPanel { void Init(CollectionType type,CollectBookDataBase data); }
    public interface IHomeMenuSettingsDialog { void Init(HomeMenuSettingStyle style); }
    // Popup callbacks run at the same point as the original WindowManager
    // callbacks. Bind real feature hosts before any original Awake/OnEnable.
    public interface IHomeFunctionMenuServices
    {
        IHomeMenuPlayer Player { get; }
        IHomeHudConditionHandler ConditionHandler { get; }
        HomeMenuFunctionLimitInfo GetFunctionLimit(FunctionType type);
        bool IsMet(IHomeHudCondition condition);
        bool IsHomeScene { get; }
        IEnumerable<object> GetAllAchievementGroups();
        void ReportClick(string panel,string eventId);
        void PopupAchievementGroups(IWindowOperator owner,Action<IHomeMenuAchievementPanel> initialize);
        void PopupCollectBook(IWindowOperator owner,Action<IHomeMenuCollectBookPanel> initialize);
        void PopupHomeSkin(Action<IHomeMenuPanel> initialize);
        void PopupLocalFightRecord(Action<IHomeMenuPanel> initialize);
        void PopupSettings(IWindowOperator owner,Action<IHomeMenuSettingsDialog> initialize);
        void PopupShopActivityEntry(Action<IHomeMenuPanel> initialize);
        void PopupPlanesMerge(Action<IHomeMenuPanel> initialize);
        void PopupFriend(IWindowOperator owner);
        void PopupUser();
        void PopupPersonalTop(string eventId,string source);
        void NavigateTaskPanel(object argument,object completion);
        void OpenScreenShot();
        void PopAnnounce();
        void PopSocial();
        void RemoveNewFunction(FunctionType type);
        bool CheckFunctionDisabled(string type);
        void ShowError(int id,object args,bool translate);
    }
}
