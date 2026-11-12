using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomeTaskUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeTaskUI
{
    // The map owner supplies the migrated condition's navigation/trace methods;
    // the task model never substitutes an approximate target or path.
    public interface IOfflineHomeTaskNavigation
    {
        bool CanNavigate(int classId);
        void Navigate(int classId, IMapInputSupportHandlerProxy input, INavigateManager navigation, object context);
        ITaskTraceTarget TraceTarget(int classId);
    }

    public sealed class OfflineHomeTaskPlayer : IHomeTaskPlayer, IDisposable
    {
        readonly OfflineMainlineProgressSession profile;
        readonly OfflineHomeMainlineTaskData data;
        readonly OfflineHomeTaskConditions conditions;
        readonly OfflineHomeTaskRewardPresenter rewards;
        readonly IOfflineHomeTaskNavigation navigation;
        readonly Func<long> combatRating;
        readonly Func<SubRank> subRank;
        readonly Action<ProgressionResult> onSubmit;
        readonly Dictionary<int, Task> models = new Dictionary<int, Task>();
        Dictionary<int, OfflineMainlineTaskState> states;
        Dictionary<int, bool> visible;
        bool disposed;

        readonly HomeTaskPlayerEvent<PlayerTaskDataWrap> added, updated;
        readonly HomeTaskPlayerEvent<long> removed, debugTime;
        readonly HomeTaskPlayerEvent<PlayerItemDataWrap, long, AwardSourceData> items;
        readonly HomeTaskPlayerEvent<long, long> combat;
        readonly HomeTaskPlayerEvent<SubRank> rank;
        readonly HomeTaskPlayerEvent<int, int, MapRegionStatus, MapRegionStatus> mapStatus;
        readonly HomeTaskPlayerEvent<int> level;

        public OfflineHomeTaskPlayer(OfflineMainlineProgressSession profile, OfflineHomeMainlineTaskData data,
            OfflineHomeTaskConditionReader reader, OfflineHomeTaskConditions conditions,
            OfflineHomeTaskRewardPresenter rewards, IOfflineHomeTaskNavigation navigation,
            Func<long> combatRating, Func<SubRank> subRank, Action<ProgressionResult> onSubmit,
            Action<Exception> reportException)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            this.conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.combatRating = combatRating ?? throw new ArgumentNullException(nameof(combatRating));
            this.subRank = subRank ?? throw new ArgumentNullException(nameof(subRank));
            this.onSubmit = onSubmit ?? throw new ArgumentNullException(nameof(onSubmit));
            if (reportException == null) throw new ArgumentNullException(nameof(reportException));
            added = new HomeTaskPlayerEvent<PlayerTaskDataWrap>(reportException);
            updated = new HomeTaskPlayerEvent<PlayerTaskDataWrap>(reportException);
            removed = new HomeTaskPlayerEvent<long>(reportException);
            debugTime = new HomeTaskPlayerEvent<long>(reportException);
            items = new HomeTaskPlayerEvent<PlayerItemDataWrap, long, AwardSourceData>(reportException);
            combat = new HomeTaskPlayerEvent<long, long>(reportException);
            rank = new HomeTaskPlayerEvent<SubRank>(reportException);
            mapStatus = new HomeTaskPlayerEvent<int, int, MapRegionStatus, MapRegionStatus>(reportException);
            level = new HomeTaskPlayerEvent<int>(reportException);
            var settings = data.CopyTaskSettings();
            var autoPin = new HashSet<int>(settings["AutoPinTaskTypes"].Values<int>());
            var pinnable = new HashSet<int>(settings["CanBePinnedTaskTypes"].Values<int>());
            foreach (var row in data.OrderedTasks)
            {
                var original = row.CopyOriginalRow();
                if ((int)original["TaskType"] != 10 || (int)original["Duration"] != 0
                    || (int)original["CompletedTimes"] != 1 || (bool)original["IsUnlimit"]
                    || (bool)original["AcceptMultiple"] || (bool)original["RepeatAccept"]
                    || (int)original["ClaimType"] != 2)
                    throw new NotSupportedException("Review the newly selected task instance rules: " + row.ClassId);
                models.Add(row.ClassId, new Task(this, row, reader,
                    autoPin.Contains((int)original["TaskType"]) && pinnable.Contains((int)original["TaskType"])));
            }
            states = Snapshot();
            visible = VisibilitySnapshot();
        }

        public event Action<PlayerTaskDataWrap> OnTaskAdd { add => added.AddListener(value); remove => added.RemoveListener(value); }
        public event Action<PlayerTaskDataWrap> OnTaskUpdate { add => updated.AddListener(value); remove => updated.RemoveListener(value); }
        public event Action<long> OnTaskRemove { add => removed.AddListener(value); remove => removed.RemoveListener(value); }
        public event Action<long> OnDebugTimeOffset { add => debugTime.AddListener(value); remove => debugTime.RemoveListener(value); }
        public event Action<PlayerItemDataWrap, long, AwardSourceData> OnItemCountUpdate { add => items.AddListener(value); remove => items.RemoveListener(value); }
        public event Action<long, long> OnCombatRatingChanged { add => combat.AddListener(value); remove => combat.RemoveListener(value); }
        public event Action<SubRank> OnSubRankUpdate { add => rank.AddListener(value); remove => rank.RemoveListener(value); }
        public event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate { add => mapStatus.AddListener(value); remove => mapStatus.RemoveListener(value); }
        public event Action<int> OnLevelUpdate { add => level.AddListener(value); remove => level.RemoveListener(value); }

        public bool Gm => false;
        public int Level { get { EnsureAlive(); return profile.Save.Player.Level; } }
        public long CombatRating { get { EnsureAlive(); return combatRating(); } }
        public SubRank SubRank { get { EnsureAlive(); return subRank(); } }
        public long GetItemAmount(int classId) { EnsureAlive(); return profile.InventoryCount(classId); }
        public bool HasItems(Dictionary<int, long> required)
        { EnsureAlive(); return required == null || required.All(value => GetItemAmount(value.Key) >= value.Value); }
        public IEnumerable<PlayerTask> PinTasks()
        { EnsureAlive(); return models.Values.Where(value => value.Pinned && Active(State(value.TaskClassId))).Cast<PlayerTask>().ToArray(); }
        public bool IsPinnedTask(long id) => GetTaskData(id)?.DataWrap.Pinned ?? false;
        public PlayerTask GetTaskData(long id)
        {
            EnsureAlive();
            return id >= int.MinValue && id <= int.MaxValue && models.TryGetValue((int)id, out var task)
                && Active(State((int)id)) ? task : null;
        }

        static bool Active(OfflineMainlineTaskState state)
            => state == OfflineMainlineTaskState.Processing || state == OfflineMainlineTaskState.Completed;
        OfflineMainlineTaskState State(int id)
        {
            var result = profile.GetMainlineTaskState(id, out var state);
            if (result.Status != ProgressionResultStatus.Ready)
                throw new NotSupportedException(result.UnsupportedCondition);
            return state;
        }
        Dictionary<int, OfflineMainlineTaskState> Snapshot()
            => data.OrderedTasks.ToDictionary(value => value.ClassId, value => State(value.ClassId));
        Dictionary<int, bool> VisibilitySnapshot()
            => models.ToDictionary(value => value.Key, value => Active(states[value.Key])
                && (value.Value.Info().DisplayCondition?.IsMet(this) ?? true));

        // Called after the owner has committed/reloaded the corresponding local
        // transaction. Publish a coherent new snapshot before any UI callback.
        public void RefreshTasks(bool reload = true)
        {
            EnsureAlive();
            if (reload) profile.Reload();
            var previous = states;
            var previousVisibility = visible;
            states = Snapshot();
            visible = VisibilitySnapshot();
            foreach (var row in data.OrderedTasks)
            {
                var before = previous[row.ClassId]; var after = states[row.ClassId];
                if (before == after && previousVisibility[row.ClassId] == visible[row.ClassId]) continue;
                if (Active(before) && !Active(after)) { removed.Invoke(row.ClassId); conditions.NotifyTaskRemove(); }
                else if (!Active(before) && Active(after)) { added.Invoke(models[row.ClassId]); conditions.NotifyTaskAdd(); }
                else if (Active(after)) { updated.Invoke(models[row.ClassId]); conditions.NotifyTaskUpdate(); }
            }
        }

        public void NotifyItemCountUpdate(int classId, long previous, AwardSourceData source = null)
        { EnsureAlive(); items.Invoke(new Item(classId), previous, source); }
        public void NotifyCombatRatingChanged(long previous, long current) { EnsureAlive(); combat.Invoke(previous, current); }
        public void NotifySubRankUpdate(SubRank value) { EnsureAlive(); rank.Invoke(value); }
        public void NotifyMapStatusUpdate(int map, int region, MapRegionStatus before, MapRegionStatus after)
        { EnsureAlive(); mapStatus.Invoke(map, region, before, after); }
        public void NotifyLevelUpdate(int value) { EnsureAlive(); level.Invoke(value); }

        void Submit(Task task, IHomeTaskPlayer player, long count)
        {
            EnsureAlive();
            if (!ReferenceEquals(player, this)) throw new ArgumentException("The owning player is required.", nameof(player));
            if (count != 1) throw new NotSupportedException("Selected mainline tasks submit once.");
            profile.Reload();
            var before = profile.Save.Player.Inventory.ToDictionary(value => value.ItemId, value => value.Count);
            var result = profile.ClaimTask(task.TaskClassId);
            if (result.IsApplied)
            {
                // Original MsgPlayerTaskUpdate applies finished records before
                // removals and updated task instances. All reads see the commit.
                conditions.NotifyAddFinishedTask();
                RefreshTasks(false);
                foreach (var stack in profile.Save.Player.Inventory)
                {
                    before.TryGetValue(stack.ItemId, out long amount);
                    if (amount != stack.Count) NotifyItemCountUpdate(stack.ItemId, amount);
                }
            }
            onSubmit(result);
        }

        sealed class Item : PlayerItemDataWrap
        {
            readonly ItemInfo info;
            public Item(int id) { info = new ItemInfo { ClassId = id }; }
            public override ItemInfo Info => info;
        }
        sealed class TaskInfoProjection : TaskInfo
        {
            readonly Task task;
            readonly OfflineHomeMainlineTaskData.TaskConfig row;
            readonly ICondition display, finish;
            public TaskInfoProjection(Task task, OfflineHomeMainlineTaskData.TaskConfig row, OfflineHomeTaskConditionReader reader)
            { this.task = task; this.row = row; display = reader.Display(row.ClassId); finish = reader.Finish(row.ClassId); }
            public override ICondition DisplayCondition => display;
            public override ICondition FinishCondition => finish;
            public override bool IsAllowTrace => row.IsAllowTrace;
            public override bool ShowProgressBar => row.ShowProgressBar;
            public override int Order => row.Order;
            public override bool AutoTracing => row.AutoTracing;
            public override ITaskTraceTarget GetTaskTraceTarget() => task.owner.navigation.TraceTarget(row.ClassId);
        }
        sealed class Task : PlayerTaskDataWrap
        {
            internal readonly OfflineHomeTaskPlayer owner;
            readonly OfflineHomeMainlineTaskData.TaskConfig row;
            readonly TaskInfoProjection info;
            readonly bool pinned;
            public Task(OfflineHomeTaskPlayer owner, OfflineHomeMainlineTaskData.TaskConfig row,
                OfflineHomeTaskConditionReader reader, bool pinned)
            { this.owner = owner; this.row = row; this.pinned = pinned; info = new TaskInfoProjection(this, row, reader); }
            // Selected mainline tasks have one instance each. Use their local
            // class identity rather than fabricate an online server instance ID.
            public override long TaskId => row.ClassId;
            public override int TaskClassId => row.ClassId;
            public override PlayerTaskDataWrap DataWrap => this;
            public override bool Pinned => pinned;
            public override TaskInfo Info() => info;
            public override bool IsCompletedState() => owner.State(row.ClassId) == OfflineMainlineTaskState.Completed;
            public override bool IsHandInItemsTask() => false;
            public override Dictionary<int, long> GetNeedHandInItemsDict() => null;
            public override bool CanFinish(IHomeTaskPlayer player) => IsCompletedState();
            public override bool CanGetReward(IHomeTaskPlayer player)
                => IsCompletedState() && ((JObject)row.CopyOriginalRow()["Reward"]).Count > 0;
            public override void InitRewardIconWithProbability(IItemIconManager icons, Transform slot, List<ItemIcon> results)
                => owner.rewards.Populate(row.ClassId, icons, slot, results);
            public override bool CanNavigate() => owner.navigation.CanNavigate(row.ClassId);
            public override void Navigate(IMapInputSupportHandlerProxy input, INavigateManager navigation, object context)
                => owner.navigation.Navigate(row.ClassId, input, navigation, context);
            public override void Submit(IHomeTaskPlayer player, long count) => owner.Submit(this, player, count);
            public override bool ShouldShowCountdown() => false;
            public override TimeSpan? GetRemainTime() => null;
        }

        void EnsureAlive() { if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeTaskPlayer)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            added.ClearListener(); updated.ClearListener(); removed.ClearListener(); debugTime.ClearListener();
            items.ClearListener(); combat.ClearListener(); rank.ClearListener(); mapStatus.ClearListener(); level.ClearListener();
            models.Clear(); states.Clear(); visible.Clear();
        }
    }
}
