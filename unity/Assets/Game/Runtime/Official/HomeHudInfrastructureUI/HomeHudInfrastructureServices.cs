using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomePlayerPanelUI;
namespace Zjcs.Official.HomeHudInfrastructureUI
{
    public interface IHomeHudActivityData { ActivityType ActivityType { get; } int Id { get; } }
    public interface IHomeHudRightEntryServices
    {
        event Action<ActivityType> OnActivitiesUpdate;
        event Action<IHomeHudActivityData> OnActivityPreview;
        event Action<IHomeHudActivityData> OnActivityOpen;
        event Action<IHomeHudActivityData> OnActivityClose;
        IReadOnlyList<HomeRightEntryUIActivityInfo> GetSortedEntries();
        bool TryGetActivity(ActivityType type,int id,out IHomeHudActivityData activity);
        bool TryGetActivityByType(ActivityType type,out Dictionary<int,IHomeHudActivityData> activities);
        IHomeMenuPrefabHandle LoadPrefab(string path);
        // The host binds dependencies before Awake/OnEnable; original ResetUI
        // follows the scene-container Instantiate(prefab,parent,false).
        GameObject InstantiatePrefab(GameObject prefab,Transform parent);
    }
    public interface IHomeHudDownloadPlayer
    {
        bool HasCollectedOnceReward(string type);
        event Action<string> OnCollectOnceReward;
    }
    public interface IHomeHudDownloadHandler
    {
        PaydState CurrentState { get; }
        long CurrentDownloadBytes { get; }
        long TotalDownloadBytes { get; }
    }
    public interface IHomeHudDownloadProgressDialog { void Init(bool allowClickClose); }
    public interface IHomeHudDownloadServices
    {
        IHomeHudDownloadPlayer Player { get; }
        IDisposable SubscribeProgress(Action<PlayAsYouDownloadProgressUpdateEvent> callback);
        IDisposable SubscribeState(Action<PlayAsYouDownloadStateUpdateEvent> callback);
        IHomeHudDownloadHandler PaydHandler { get; }
        bool EnablePlayAsYouDownload { get; }
        int CheckConditionId { get; }
        IHomeHudCondition GetCondition(int id);
        IHomeHudConditionHandler ConditionHandler { get; }
        bool IsMet(IHomeHudCondition condition);
        UnityEngine.Object MapRootIdentity { get; }
        UniTask LeaveMap();
        bool IsSplashHidden { get; }
        void PopupProgress(Action<IHomeHudDownloadProgressDialog> initialize);
        UniTask PopupProgressGlobal(Action<IHomeHudDownloadProgressDialog> initialize,bool instant);
    }
    public static class HomeHudInfrastructureUtils
    {
        public static IHomeMenuPrefabHandle LoadPrefab(IHomeHudRightEntryServices services,string path,ref HomePanelDisposableBag bag)
        { var handle=services.LoadPrefab(path);bag.Add(handle);return handle; }
        public static GameObject Instantiate(IHomeHudRightEntryServices services,GameObject prefab,Transform parent)
        { var result=services.InstantiatePrefab(prefab,parent);HomeMenuUIUtils.ResetUI(result);return result; }
    }
}
