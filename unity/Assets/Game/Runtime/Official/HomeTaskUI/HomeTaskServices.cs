using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeTaskUI
{
    // Read-only projections of the original task model. The host owns all state
    // changes and the existing local task/reward/navigation transactions.
    public interface IHomeTaskServices
    {
        IHomeTaskPlayer Player { get; }
        UnityEvent OnTeamUpdate { get; }
        IHomeTaskConditions Conditions { get; }
        IMapInputSupportHandlerProxy mMapInput { get; }
        INavigateManager NavigateManager { get; }
        ITaskHandler TaskHandler { get; }
        IItemIconManager ItemIconManager { get; }
        IMyMapPositionHandlerProxy MyMapPositionHandler { get; }
        IHomeTaskGlobalColors GlobalColors { get; }
        IHomeTaskCombatColors CombatRatingConfig { get; }
        object PlanesRank { get; }
        DateTime GetDateTime();
        double CalcRemainSecond(DateTime endTime);
        DateTime GetDailyResetTimeFromDate(DateTime dateTime);
        MapRegionInfo GetMapRegion(int mapClassId, int region);
        int CalcRankSuppression(SubRank player, SubRank region);
        int GetCombatRatingLevel(long combatRating, IReadOnlyList<long> values);
        bool TryCheckTaskLimit(int taskClassId, IHomeTaskPlayer player, out TaskLimitResult result);
        string GetTaskTitleKey(int taskClassId);
        string GetFullName(GameObject go);
        // Original UIUtils.Instantiate(..., EUIInstantiateFlag.Transform): inject
        // before activation; reset local position/rotation/scale, then return.
        TaskListItem InstantiateTaskItem(GameObject template, Transform parent);
        void OpenTaskDetail(PlayerTask task);
        void DebugCompleteTask(List<long> taskIds);
        void RefreshAllTasks(bool invokeByPlayer);
        void RefreshLimitTimeTask(long taskId, bool invokeByPlayer);
        MapRegionLimitStatusRefreshForRankUnlockTime CreateTimedRegionLimitProvider();
    }
    public interface IHomeTaskPlayer
    {
        event Action<PlayerTaskDataWrap> OnTaskAdd;
        event Action<PlayerTaskDataWrap> OnTaskUpdate;
        event Action<long> OnTaskRemove;
        event Action<long> OnDebugTimeOffset;
        event Action<PlayerItemDataWrap, long, AwardSourceData> OnItemCountUpdate;
        event Action<long,long> OnCombatRatingChanged;
        event Action<SubRank> OnSubRankUpdate;
        event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        event Action<int> OnLevelUpdate;
        IEnumerable<PlayerTask> PinTasks();
        bool IsPinnedTask(long taskId);
        PlayerTask GetTaskData(long taskId);
        bool Gm { get; }
        int Level { get; }
        long CombatRating { get; }
        SubRank SubRank { get; }
        long GetItemAmount(int classId);
        bool HasItems(Dictionary<int,long> items);
    }
    public abstract class PlayerTask
    {
        public abstract long TaskId { get; }
        public abstract int TaskClassId { get; }
        public abstract PlayerTaskDataWrap DataWrap { get; }
        public abstract TaskInfo Info();
        public abstract bool IsCompletedState();
        public abstract bool IsHandInItemsTask();
        public abstract Dictionary<int,long> GetNeedHandInItemsDict();
        public abstract bool CanGetReward(IHomeTaskPlayer player);
        public abstract void InitRewardIconWithProbability(IItemIconManager icons, Transform slot, List<ItemIcon> results);
        public abstract bool CanFinish(IHomeTaskPlayer player);
        public abstract bool CanNavigate();
        public abstract void Navigate(IMapInputSupportHandlerProxy input, INavigateManager navigation, object context);
        public abstract void Submit(IHomeTaskPlayer player, long count);
        public abstract bool ShouldShowCountdown();
        public abstract TimeSpan? GetRemainTime();
    }
    public abstract class PlayerTaskDataWrap : PlayerTask { public abstract bool Pinned { get; } }
    public abstract class TaskInfo
    {
        public abstract ICondition DisplayCondition { get; }
        public abstract ICondition FinishCondition { get; }
        public abstract bool IsAllowTrace { get; }
        public abstract bool ShowProgressBar { get; }
        public abstract int Order { get; }
        public abstract bool AutoTracing { get; }
        public abstract ITaskTraceTarget GetTaskTraceTarget();
    }
    public interface ITaskTraceTarget { bool CanTrace(); ITaskTraceMapEntity StartTrace(long taskId); }
    public abstract class ICondition
    {
        public sealed class DataHub
        {
            readonly Dictionary<ConditionDataType,object> values = new Dictionary<ConditionDataType,object>();
            public void Supply(ConditionDataType type, object value) => values[type] = value;
            public object Get(ConditionDataType type) => values.TryGetValue(type,out var value) ? value : null;
        }
        public abstract ConditionType Type { get; }
        public abstract bool IsMet(DataHub data);
        public abstract bool IsMet(IHomeTaskPlayer player);
        public abstract (long,long) GetProgress(DataHub data);
        public abstract ConditionDescComponent GetDescComponent(DataHub data, object context, string metColor, string notMetColor);
    }
    // Only the two identifiers consumed by this UI; no serialized rule IDs.
    public enum ConditionDataType { Player, TaskData }
    public enum ConditionType { Other, WorldMapProgress }
    public abstract class WorldMapProgressCondition : ICondition
    { public abstract int MapClassId { get; } public abstract int Region { get; } }
    public abstract class SubRankCondition : ICondition { }
    public abstract class PreRequirement
    {
        public abstract List<ICondition> Conditions { get; }
        public abstract Dictionary<int,long> Items { get; }
        public abstract bool IsAchieved(IHomeTaskPlayer player, ICondition.DataHub data);
    }
    public sealed class ConditionDescComponent
    {
        public string Key;
        public IWorldTextProcessor TextProcessor;
        public object[] Args;
    }
    public interface IHomeTaskConditions
    { void Register(ICondition condition, Action refresh); void Deregister(ICondition condition, Action refresh); }
    public abstract class MapRegionInfo
    {
        public abstract int LimitLevel { get; }
        public abstract long CombatRating { get; }
        public abstract IReadOnlyList<long> CombatRatingLevelValues { get; }
        public abstract SubRank SubRank { get; }
        public abstract bool IsLimited(int level, object planesRank);
        public abstract bool ExistLimit();
        public abstract MapRegionLimitType IsLimitedByType(int level, object planesRank);
    }
    public sealed class TaskLimitResult { public DateTime? EndTime; public string LimitDesc; public bool BlockJump; }
    // The timed provider is supplied by the existing map host. Its UI caller and
    // disposal order remain original, and it must implement the recorded source.
    public abstract class MapRegionLimitStatusRefreshForRankUnlockTime : IMapRegionLimitStatusRefreshProvider
    {
        public MapRegionLimitType RegionLimitType => MapRegionLimitType.RegionUnlockTime;
        public abstract void Init(MapRegionInfo region, ShowRemainTimeInfo view, Action refresh);
        public abstract void Dispose();
    }
    public interface IHomeTaskGlobalColors { GlobalColors Value { get; } }
    public sealed class GlobalColors { public Color EnoughColor, LackColor; }
    public interface IHomeTaskCombatColors { CombatRatingConfig Value { get; } }
    public abstract class CombatRatingConfig
    { public abstract bool TryGetLevelColor(CombatRateingColorType type, int level, out Color color); }
    public abstract class TextMeshProColorProxy : MonoBehaviour { public abstract void SetColor(Color color); }
    public interface IMapInputSupportHandlerProxy { }
    public interface INavigateManager { }
    public interface IMyMapPositionHandlerProxy { event Action<int,int> OnMeRegionUpdate; }
    public interface ITaskHandler
    {
        HomeTaskTraceEvent OnTraceStart { get; }
        HomeTaskTraceEvent OnTraceEnd { get; }
        HomeTaskPlayerEvent<long> OnTraceCancel { get; }
        ITaskTraceMapEntity CurrentTraceData { get; }
        long? WaitTraceTaskId {get;}
        void StartTrace(long taskId, bool showTip=true);
        void CancelTrace(bool showTip=true);
        void StopTrace(long taskId);
        void TraceWhenEnterMap(long taskId);
        void CleanWaitTraceTask();
    }
    public abstract class PlayerItemDataWrap { public abstract ItemInfo Info { get; } }
    public sealed class ItemInfo { public int ClassId; }
    public abstract class AwardSourceData { }
    public enum ItemIconAttachmentType { GotTip }
    public abstract class ItemIcon : MonoBehaviour
    {
        public abstract ItemInfo myInfo { get; }
        public abstract ItemIcon SetClickPopupInfoDialog();
        public abstract ItemIcon SetCenterHintContent(bool show);
        public abstract void SetRequireCount(long count);
        public abstract void SetAttachment(ItemIconAttachmentType type, bool show);
        public abstract void ResetCountTextScale();
    }
    public interface IItemIconManager { ItemIcon GetIcon(int classId, Transform parent); void RecycleIcon(ItemIcon icon); }
}
